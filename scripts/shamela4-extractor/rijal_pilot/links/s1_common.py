"""Shared helpers for the S1 (Shamela narrator encyclopedia) <-> registry review scripts (draft)."""
import bisect
import json
import os
import re
import sqlite3
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..'))
RIJAL = os.path.join(ROOT, 'data', 'shamela_rijal')
LINKS = os.path.join(RIJAL, 'review', 'links')
SHAMELA = os.path.join(ROOT, 'data', 'shamela')
HERE_PILOT = os.path.join(ROOT, 'scripts', 'shamela4-extractor', 'rijal_pilot')
BOOK_DB = r'D:\Islamic\shamela4\database\book'

HARAKAT = re.compile(r'[ً-ْٰـ]')
AR_DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')


def nrm(s: str) -> str:
    """Harakat off, alif forms/ya/ta marbuta folded, article dropped, 'عبد X' / 'عبيد الله' one token."""
    s = HARAKAT.sub('', s or '')
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bابي\b(?!\s+بن\b)', 'ابو', s)
    s = re.sub(r'\bعبيد\s+الله\b', 'عبيد_الله', s)
    s = re.sub(r'\bال(?=\S)', '', s)
    s = re.sub(r'\bعبد\s+(\S+)', r'عبد_\1', s)
    return re.sub(r'[^ء-ي_ ]', ' ', s)


def toks(s: str) -> list[str]:
    return [w for w in nrm(s).split() if w not in ('بن', 'ابن') and len(w) > 1]


def nasab_chain(s: str) -> list[str]:
    """The ism and the fathers of the first name phrase: 'a بن b بن c' -> [a, b, c]."""
    s = nrm(s)
    parts = [p.split() for p in re.split(r'\s+(?:بن|ابن)\s+', ' ' + s + ' ') if p.strip()]
    return [p[0] if i == 0 else p[0] for i, p in enumerate(parts) if p]


def load_registry():
    """Registry rows in the order link_tahdhib.py loads them: Tahdhib entries, then extra_*.json."""
    sys.path.insert(0, HERE_PILOT)
    import glob
    data = json.load(open(os.path.join(RIJAL, 'tahdhib.json'), encoding='utf-8'))
    entries = [e for e in data if e['kind'] == 'entry']
    n_tahdhib = len(entries)
    for f in sorted(glob.glob(os.path.join(RIJAL, 'extra_*.json'))):
        entries += [e for e in json.load(open(f, encoding='utf-8')) if e['kind'] == 'entry']
    from pipeline import registry_ids
    ids = registry_ids(entries, n_tahdhib)
    taq = json.load(open(os.path.join(RIJAL, 'taqrib.json'), encoding='utf-8'))['entries']
    align = {p['tahdhib']: p['taqrib'] for p in json.load(open(os.path.join(RIJAL, 'align.json'), encoding='utf-8'))}
    return entries, n_tahdhib, ids, taq, align


# --- Arabic number words (Taqrib death years) ---
UNITS = {'واحد': 1, 'احدي': 1, 'إحدى': 1, 'اثنين': 2, 'اثنتين': 2, 'اثني': 2, 'اثنتي': 2, 'ثلاث': 3, 'ثلاثة': 3, 'اربع': 4, 'أربع': 4,
         'اربعة': 4, 'أربعة': 4, 'خمس': 5, 'خمسة': 5, 'ست': 6, 'ستة': 6, 'سبع': 7, 'سبعة': 7, 'ثمان': 8, 'ثماني': 8, 'ثمانية': 8,
         'ثمانى': 8, 'تسع': 9, 'تسعة': 9, 'عشر': 10, 'عشرة': 10}
TENS = {'عشرين': 20, 'ثلاثين': 30, 'اربعين': 40, 'أربعين': 40, 'خمسين': 50, 'ستين': 60, 'سبعين': 70, 'ثمانين': 80, 'تسعين': 90,
        'عشرون': 20, 'ثلاثون': 30}


def death_mod100(text: str):
    """'مات سنة ست وثلاثين' -> 36 (year mod 100); None when not readable."""
    if not text:
        return None
    m = re.search(r'سنة\s+(.*)', text)
    if not m:
        return None
    s = m.group(1)
    d = re.match(r'\s*([٠-٩0-9]+)', s)
    if d:
        return int(d.group(1).translate(AR_DIGITS)) % 100
    words = re.findall(r'[ء-ي]+', re.split(r'\s+(?:او|أو|وقيل|وله|وقد|بعدها|تقريبا|ايضا)\b', s)[0])
    total, seen = 0, False
    for w in words:
        w = w.lstrip('و')
        if w in TENS:
            total += TENS[w]; seen = True
        elif w in UNITS:
            total += UNITS[w]; seen = True
        elif w in ('مائة', 'مئة', 'ومائة', 'ومئة', 'مائتين', 'مئتين', 'ثلاثمائة', 'مائه'):
            seen = True
        elif seen:
            break
    if seen and total >= 100:
        total -= 100
    return total % 100 if seen else None


def page_index(book_id: int, db_rel: str):
    """Pages of a Shamela book: [(page_id, harakat-free text)] in order, and page_id -> (part, page)."""
    pages = []
    for line in open(os.path.join(RIJAL, 'dump', f'{book_id}_pages.tsv'), encoding='utf-8'):
        pid, body = line.rstrip('\n').split('\t', 1)
        pages.append((int(pid.split('-')[1]), HARAKAT.sub('', body.replace('\\n', '\n'))))
    pages.sort()
    c = sqlite3.connect(f'file:{BOOK_DB}/{db_rel}?mode=ro', uri=True)
    part_page = {i: (p, pg) for i, p, pg in c.execute('select id, part, page from page')}
    return pages, part_page
