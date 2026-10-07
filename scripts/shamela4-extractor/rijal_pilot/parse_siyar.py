"""Siyar A'lam al-Nubala (Shamela 10906) as a fallback registry file: dump/10906_pages.tsv -> extra_siyar.json.

Run from data/shamela_rijal, after dumping the book with ShamelaLuceneDumper:
    python ../../scripts/shamela4-extractor/rijal_pilot/parse_siyar.py

The page text carries the table-of-contents marks (<span data-type="title" id=toc-N>٢٨٥ - ...</span>), so the book is
cut there. An entry is a numbered title; its first line is the full name, then come «حدّث عن:» (shaykhs) and
«حدّث عنه:» (students). Every entry is marked "fallback" (see link_tahdhib.py): the Siyar is mostly famous men
whom Tahdhib already has, so it may only name a candidate when nobody else matches.
"""
import csv
import json
import re
import sys

csv.field_size_limit(10 ** 9)
BOOK = 10906
HARAKAT = re.compile(r'[ً-ْٰـ]')
MARK = re.compile(r'<span data-type="title" id=toc-(\d+)>(.*?)</span>', re.S)
NUMBERED = re.compile(r'^\s*[٠-٩0-9]+\s*[-–]\s*')
# Titles of rank before the name: «الإمام، الحافظ، المعمر، الصادق، أبو عروبة الحسين…»
HONORIFICS = {'الامام', 'الحافظ', 'العلامه', 'الشيخ', 'الحجه', 'الثقه', 'المحدث', 'القاضي', 'الفقيه', 'المسند', 'الرحال', 'الصدوق',
              'الامين', 'المقري', 'المفتي', 'الزاهد', 'العابد', 'الاديب', 'المعمر', 'الصادق', 'الكبير', 'شيخ الاسلام', 'المجود',
              'الضرير', 'الاستاذ', 'العالم', 'الورع', 'الاوحد', 'الرئيس', 'الواعظ', 'النحوي', 'اللغوي', 'الخطيب', 'الصالح',
              'الناقد', 'المتقن', 'الحافظ الكبير', 'المحدث الرحال', 'الاثري', 'الثبت', 'الامام الحافظ', 'السيد', 'الامير',
              'الفاضل', 'الجليل', 'المقرئ', 'الصوفي', 'المؤرخ', 'الوزير', 'الملك', 'السلطان', 'القدوه', 'العمده', 'الاديب'}
NARRATING = re.compile(r'\s(?:عن|روى|يروي|ذكره|ذكر|قال|وقال|له|حدث|حدثنا|سمع|لقيه|أخذ|وثقه|ضعفه|ليس|كان|مات|توفي|ولد)\s')
LIST_LINE = re.compile(r'^\s*و?(?:حدث|روى|سمع|يروي|أخذ|تفقه|قرأ|تلا)\s*(عنه|عن|من)?\s*:\s*(.*)$')
NAMEISH = re.compile(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أبا|بنت|أم)\s')
SKIP_ITEM = re.compile(r'^(?:وآخرون|آخرون|وخلق|خلق|وعدة|عدة|وغيرهم|غيرهم|وجماعة|جماعة|وغير|ولده|وولده|وطائفة)')


def norm_hon(s):
    return re.sub(r'\s+', ' ', re.sub(r'[ً-ْٰـ]', '', s)).replace('ة', 'ه').replace('أ', 'ا').replace('إ', 'ا').replace('آ', 'ا').strip()


def clean_name(first):
    """The name line without its titles of rank, cut where the narrating starts."""
    first = re.sub(r'\(\s*[٠-٩0-9]+\s*\)', ' ', first)
    first = first.split('.')[0]
    parts = [p.strip() for p in first.split('،')]
    while parts and norm_hon(parts[0]) in HONORIFICS:
        parts.pop(0)
    name = '، '.join(parts)
    name = NARRATING.split(name + ' ')[0].strip(' ،.')
    return name


def title_header(tname):
    """«ابن طلاب أبو الجهم أحمد بن الحسين بن أحمد» -> «أحمد بن الحسين بن أحمد، أبو الجهم، ابن طلاب»: the name first, as
    the headers of the other books have it, the kunya and the laqab after it."""
    t = re.sub(r'\s*\*\s*$', '', tname).split()
    for i in range(len(t) - 1):
        if t[i + 1] == 'بن' and t[i] not in ('ابن', 'أبو', 'أبي', 'أبا', 'بن'):
            if i and (t[i - 1] == 'عبد' or (t[i] == 'الله' and t[i - 1] == 'عبيد')):   # «عبد الرحمن», «عبيد الله»: one name
                i -= 1
            name, prefix = ' '.join(t[i:]), ' '.join(t[:i])
            return f'{name}، {prefix}' if prefix else name
    return ' '.join(t)


def items(text):
    out = []
    text = re.sub(r'\([^)]{0,40}\)', ' ', text)
    for part in re.split(r'،|\sو(?=[ء-ي])', text):
        part = re.sub(r'^و(?=[ء-ي]{3})', '', part.strip(' .؛')).strip()
        if not part or SKIP_ITEM.match(part) or len(part.split()) > 9:
            continue
        out.append({'name': part, 'symbols': '', 'note': '' if NAMEISH.search(' ' + part + ' ') else 'short'})
    return out


def main():
    pages = {}
    for r in csv.reader(open(f'dump/{BOOK}_pages.tsv', encoding='utf-8'), delimiter='\t'):
        if len(r) >= 2:
            pages[int(r[0].split('-')[1])] = r[-1]
    text = '\n'.join(pages[k].replace('\\n', '\n') for k in sorted(pages))
    text = HARAKAT.sub('', text)
    marks = list(MARK.finditer(text))
    entries = []
    for i, m in enumerate(marks):
        title = re.sub(r'\s*\*\s*$', '', re.sub(r'<[^>]+>', '', m.group(2))).strip()
        if not NUMBERED.match(title):
            continue
        body = text[m.end(): marks[i + 1].start() if i + 1 < len(marks) else len(text)]
        body = re.sub(r'<[^>]+>', ' ', body)
        lines = [ln.strip() for ln in body.split('\n') if ln.strip()]
        if not lines:
            continue
        tname = NUMBERED.sub('', title).strip()
        name = clean_name(lines[0])
        header = title_header(tname)
        shuyukh, talamidh = [], []
        for ln in lines[:40]:
            lm = LIST_LINE.match(ln)
            if lm:
                (talamidh if lm.group(1) == 'عنه' else shuyukh).extend(items(lm.group(2)))
        entries.append({
            'kind': 'entry', 'num': m.group(1), 'num_suspect': False, 'symbols': '', 'source': 'siyar', 'fallback': True,
            'header': header, 'name': header, 'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': [], 'rawa_lahu': None,
            'verdict': None, 'aliases': [name] if len(name.split()) >= 3 and name != header else [],
        })
    json.dump(entries, open('extra_siyar.json', 'w', encoding='utf-8'), ensure_ascii=False)
    print('entries:', len(entries), '| with shuyukh:', sum(1 for e in entries if e['shuyukh']), '| with talamidh:',
          sum(1 for e in entries if e['talamidh']))
    if len(sys.argv) > 1:
        import random
        random.seed(int(sys.argv[1]))
        for e in random.sample(entries, 8):
            print('\n', e['header'][:90], '|', e['aliases'][:1], '| shuyukh', len(e['shuyukh']), [x['name'] for x in e['shuyukh'][:3]],
                  '| talamidh', len(e['talamidh']), [x['name'] for x in e['talamidh'][:3]])


if __name__ == '__main__':
    main()
