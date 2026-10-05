"""Pilot parser for Tahdhib al-Kamal (Shamela 4 book 3722) dumped by ShamelaLuceneDumper.

Usage: python parse_tahdhib.py <dump_dir> <out_json>
"""
import json
import re
import sys
from collections import Counter

DUMP, OUT = sys.argv[1], sys.argv[2]
AR_DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
HARAKAT = re.compile(r'[ً-ْٰـ]')


def strip_harakat(s: str) -> str:
    return HARAKAT.sub('', s)


def normalize(s: str) -> str:
    s = strip_harakat(s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    return re.sub(r'\s+', ' ', s).strip()


# 1. Load body text in page order. Page breaks become '\x01' so an entry can
#    start at the top of a page, while lists/quotes that run across the break
#    are re-joined with a space afterwards.
pages = []
with open(f'{DUMP}/3722_pages.tsv', encoding='utf-8') as f:
    for line in f:
        pid, body = line.rstrip('\n').split('\t', 1)
        pages.append((int(pid.split('-')[1]), body.replace('\\n', '\n')))
pages.sort()
text = '\x01'.join(b for _, b in pages)
text = re.sub(r'<span[^>]*>|</span>', '', text)
# A few heads put the number in brackets ("(٢٢٥١) ع: سعيد بن أبي الحسن", "(٤٦٨) عخ م د س: إسماعيل بن عمر"):
# turn them into "N - " before footnote refs are removed, or the entry is lost.
# Only before real book symbols (1-3 letter groups or "تمييز"), not footnotes ("(٤) الجرح والتعديل: ٩ / ٢٤٠").
text = re.sub(r'([\n\x01])\s*\(([٠-٩]+)\)\s*(?=(?:(?:[ء-ي]{1,3}|تمييز)\s+)*(?:[ء-ي]{1,3}|تمييز)\s*:)', r'\1\2 - ', text)
text = re.sub(r'\s*\(\s*[٠-٩]+\s*\)', '', text)  # footnote refs "(١)"
text = strip_harakat(text)

# 2. Split into entries: "٣٩٣٤ - خ ٤: name ..." at the start of a line or page.
#    Numbers must increase by a small step; anything else (numbered lists inside
#    the front matter or inside an entry) stays part of the current entry.
# Some volumes drop the dash ("٩٣٣ ق: جعفر ..."); then the symbols block and colon are required.
# A few drop the dash ("١٠٢٥: الحارث بن عبد الله الأعور", "٨١٠: س: توبة أبو صدقة").
ENTRY = re.compile(
    r'(?:^|[\n\x01])\s*([٠-٩]+)(?:\s*-\s*(?:([^:\n\x01]{0,40}?)\s*:)?|\s+([ء-ي][ء-ي ٠-٩()]{0,30}?)\s*:|:(?:\s*([ء-ي][ء-ي ٠-٩]{0,15}?)\s*:)?)\s*(?=\S)')
SYMBOL_OK = re.compile(r'^[\u0621-\u064A ٠-٩]*$')
cands = [(int(m.group(1).translate(AR_DIGITS)), m) for m in ENTRY.finditer(text)]
first = next(i for i, (n, m) in enumerate(cands) if n == 1 and 'أحمد بن إبراهيم' in text[m.end():m.end() + 40])
# Longest strictly increasing subsequence of entry numbers: skips numbered lists
# inside entries and typos in the edition (e.g. "٤٥٦" printed for 1456).
import bisect
seq = cands[first:]
tails, tails_idx, parent = [], [], [-1] * len(seq)
for i, (n, _) in enumerate(seq):
    k = bisect.bisect_left(tails, n)
    if k == len(tails):
        tails.append(n); tails_idx.append(i)
    else:
        tails[k] = n; tails_idx[k] = i
    parent[i] = tails_idx[k - 1] if k else -1
accepted, i = [], tails_idx[-1]
while i != -1:
    accepted.append(seq[i]); i = parent[i]
accepted.reverse()
typo_like = len(seq) - len(accepted)

# Recover entries whose printed number is a typo or a repeat: a rejected candidate that has a
# proper symbols block and a "روى عن" before the next numbered head is a real entry. The search
# runs up to the next head, not a fixed width: Companions such as Abu Musa al-Ash'ari (a second
# "٣٤٩١") have a long biography before their lists.
in_lis = {id(m) for _, m in accepted}
starts = sorted(m.start() for _, m in cands)
recovered = 0
for n, m in seq:
    sym = (m.group(2) or m.group(3) or m.group(4) or '').strip()
    if id(m) in in_lis or not sym or not SYMBOL_OK.match(sym):
        continue
    k = bisect.bisect_right(starts, m.start())
    stop = starts[k] if k < len(starts) else len(text)
    # The fixed 500 characters are kept too: short cross-reference notes ("عبد الغفار بن داود
    # البخاري.") have no lists of their own but are useful as aliases.
    if (re.search(r'روى\s+عنه?\s*:', text[m.end():min(stop, m.end() + 6000)])
            or re.search(r'روى\s+عنه?\s*:', text[m.end():m.end() + 500])):
        accepted.append((n, m)); recovered += 1
# Entries whose number is missing from the text altogether: '... الإزار" . - س ق: إسماعيل بن ...'.
NO_NUMBER = re.compile(r'(?:[\n\x01]|\.\s*)\s*-\s*()([ء-ي][ء-ي ٠-٩]{0,30}?)\s*:\s*(?=\S)')
taken = {m.start() for _, m in accepted}
for m in NO_NUMBER.finditer(text, accepted[0][1].start()):
    if any(abs(m.start() - t) < 15 for t in taken):
        continue
    if re.search(r'روى\s+عنه?\s*:', text[m.end():m.end() + 500]):
        accepted.append((0, m)); recovered += 1
accepted.sort(key=lambda x: x[1].start())
for k in range(1, len(accepted)):                 # infer a number for unnumbered entries
    if accepted[k][0] == 0:
        accepted[k] = (accepted[k - 1][0] + 1, accepted[k][1])
suspect = {id(m) for _, m in accepted} - in_lis

_BOOK_SYMBOLS = sorted(['ع', 'خ', 'م', 'د', 'ت', 'س', 'ق', 'بخ', 'خت', 'خد', 'دت', 'دس', 'دسي', 'دعس', 'دفق', 'دق', 'ر',
                        'رد', 'رم', 'ز', 'سي', 'سن', 'ص', 'صد', 'عخ', 'عس', 'ف', 'فق', 'قد', 'قدس', 'كد', 'كن', 'ل',
                        'مد', 'مدت', 'مق', 'تم', 'تمييز', '٤'], key=len, reverse=True)
LEADING_SYMBOLS = re.compile(r'\s*((?:[\[(]?(?:' + '|'.join(_BOOK_SYMBOLS) + r')[\])]?(?=[\s.:\])])[\s.:]*)+)(?=\S)')
entries = []
for i, (n, m) in enumerate(accepted):
    end = accepted[i + 1][1].start() if i + 1 < len(accepted) else len(text)
    symbols = (m.group(2) or m.group(3) or m.group(4) or '').strip()
    body = text[m.end():end]
    if symbols and not SYMBOL_OK.match(symbols):  # the colon belonged to the text
        body, symbols = symbols + ': ' + body, ''
    if not symbols and (lead := LEADING_SYMBOLS.match(body)):
        # Symbols with no colon: "٢٧٥٦ - ر ٤ شعيب بن محمد ..." (Amr b. Shu'ayb's father: his nasab was read
        # from «ر», so «عن أبيه» after عمرو بن شعيب never found him).
        symbols, body = re.sub(r'[\[\]().:]', ' ', lead.group(1)).strip(), body[lead.end():]
    entries.append({'num': n, 'num_suspect': id(m) in suspect, 'symbols': symbols, 'raw': body})

# 3. Parse each entry.
# Women's entries say "روت عن:" / "روى عنها:" (Aisha, Umm Salama, Amra bint Abd al-Rahman).
SHUYUKH = re.compile(r'(?:روى|روت)\s+عن\s*(?::|ك(?=\s))')     # "روى عن ك سالم": عمرو بن شعيب's typo'd colon
TALAMIDH = re.compile(r'روى\s+عنها?\s*:')
SHUYUKH_BARE = re.compile(r'(?<=\n)\s*(?:روى|روت)\s+عن\s+(?=\S)')
# A quote starts at a line/page start: "وقال X، عن Y: ..." or "وقال عن Y أيضا: ...".
QUOTE = re.compile(
    r'(?:^|[\n\x01])\s*(?:و)?قال\s+(?:(عن\s+[^:،\n\x01]{2,60}?)|([^:،\n\x01]{2,60}?)\s*(?:،\s*عن\s+([^:،\n\x01]{2,60}?))?)\s*:\s*')
NOT_CRITICS = ('رسول الله', 'النبي', 'لي', 'لنا', 'له', 'لها')
RAWA_LAHU = re.compile(r'روى\s+له\s+([^.\n]+)')
ITEM_SYMBOLS = re.compile(r'\(([^)]*)\)')
# "X, عن أبيه" -> the father. Only the common cases are needed for the pilot.
FATHER_OF = {
    'عبد الله بن أحمد بن حنبل': 'أحمد بن حنبل', 'صالح بن أحمد بن حنبل': 'أحمد بن حنبل',
    'عبد الرحمن بن أبي حاتم': 'أبو حاتم', 'ابن أبي حاتم': 'أبو حاتم',
    'علي بن المديني': 'عبد الله بن جعفر المديني',
}
SAME_AS_PREVIOUS = {'في موضع آخر', 'أيضا', 'في رواية', 'مرة'}


# Items are joined by "، و". Some lists use a Latin comma or no comma after the symbols:
# "ومسلم بن أبي مريم (م كن) ، والمسيب (س) , ويحيى (م س) ويزيد" (Abu Salih al-Samman's students
# stopped at 17); not before a note on the item ("(خ) وهو ابن ...", "(س) وقيل: ...").
ITEM_SEP = re.compile(r'\s*[،,]\s*(?=و)|(?<=\))\s*(?=و(?!(?:هو|هي|قيل|يقال|كان|لم|ليس|له|لها|في|قد|إن|ان|لا|ما|ذلك|غيره)[\s:،]))')


def split_list(seg: str) -> list[dict]:
    """Split 'A (د) ، وB (س) ، وقيل: C' into items; 'قيل: C' becomes an alias of the previous item."""
    seg = seg.strip().rstrip('.').strip()
    items = []
    for p in ITEM_SEP.split(seg):
        p = re.sub(r'^و', '', p.strip())
        syms = ITEM_SYMBOLS.findall(p)
        name = ITEM_SYMBOLS.split(p)[0].strip(' ،')
        note = p[p.rfind(')') + 1:].strip(' ،') if syms else ''
        alias = re.match(r'(?:قيل|يقال)\s*:\s*(.+)', name)
        if alias and items:
            items[-1].setdefault('aliases', []).append(alias.group(1))
            continue
        if name:
            items.append({'name': name, 'symbols': ' '.join(syms).strip(), 'note': note})
    return items


LIST_GOES_ON = re.compile(
    r'\s*(?!و?(?:قال|ذكر|كان|قيل|سمع|مات|توفي|روى|روت|حدث|أخرج|أخبر|أنبأ|اختلف|ولد|يقال|لها|له)\S*\s)\S')


def section(raw: str, start: re.Match, stop: re.Match | None) -> str:
    if stop and stop.start() > start.end():
        return raw[start.end():stop.start()]
    # A list ends at the end of its paragraph. A long one can be broken by a line break in the
    # middle ("وثابت بن قيس الزرقي\n(بخ د سي ق) ، وثور ..." cut Abu Hurayra's students at 17):
    # it goes on when the line does not end with a full stop and the next line is not a remark
    # ("وقال ...", "وذكره ابن حبان"). Ibn Uyayna's breaks inside a name ("وإبراهيم بن دينار\nالتمار").
    end = raw.find('\n', start.end())
    while end > 0 and not raw[:end].rstrip().endswith('.') and LIST_GOES_ON.match(raw, end + 1):
        end = raw.find('\n', end + 1)
    return raw[start.end():end if end > 0 else len(raw)].replace('\n', ' ')


# '- ت: أحمد بن بكار الدمشقي، هو: أحمد بن عبد الرحمن ...' is a redirect, not an entry.
CROSSREF = re.compile(r'(?:(?:،\s*|\s)(?:هو|هي)\s*:?\s*|(?:في ترجمة|يأتي في|تقدم في)\s+)([^.\n]{3,120})')

parsed = []
for e in entries:
    raw = e['raw']
    flat = raw.replace('\x01', ' ')   # lists and the header ignore page breaks
    s, t = SHUYUKH.search(flat), TALAMIDH.search(flat)
    if s is None:
        # No colon: "\nروى عن إسماعيل بن أبي خالد، وأبي ..." (أحمد بن بشير, أبي بن كعب): only at a line start
        # and before the students, so a quote ("روى عن ابن عباس أنه قال") is not taken for the list.
        s = SHUYUKH_BARE.search(flat, 0, t.start() if t else len(flat))
    header_end = min(x.start() for x in (s, t) if x) if (s or t) else (flat.find('\n') % (len(flat) + 1))
    header = flat[:header_end].strip().lstrip('-').strip()     # "- عبد الله بن مسعود ..."
    shuyukh = split_list(section(flat, s, t)) if s else []
    talamidh = split_list(section(flat, t, None)) if t else []
    after_lists = flat.find('\n', t.end()) if t else len(header)
    body = raw[max(after_lists, 0):]
    quotes, prev = [], None
    starts_q = list(QUOTE.finditer(body))
    for k, q in enumerate(starts_q):
        stop = starts_q[k + 1].start() if k + 1 < len(starts_q) else len(body)
        nl = body.find('\n', q.end())
        said = body[q.end():min(stop, nl if nl > 0 else stop)].replace('\x01', ' ').strip()
        if q.group(1):                                  # "وقال عن يحيى أيضا:"
            critic, via = re.sub(r'^عن\s+|\s+أيضا$', '', q.group(1)).strip(), None
            if prev and prev.split()[0] == critic.split()[0]:
                critic = prev
        else:
            speaker, source = q.group(2).strip(), (q.group(3) or '').strip()
            critic, via = (source, speaker) if source else (speaker, None)
        if critic == 'أبيه':
            critic = FATHER_OF.get(via, f'والد {via}')
        elif critic in SAME_AS_PREVIOUS or critic.startswith('في موضع'):
            critic = prev or critic
        if critic.startswith(NOT_CRITICS) or not said:
            continue
        quotes.append({'critic': critic, 'via': via, 'text': said})
        prev = critic
    raw = flat
    rl = RAWA_LAHU.search(raw)
    xref = CROSSREF.search(header[:160])
    no_lists = len(shuyukh) + len(talamidh) == 0
    kind = 'crossref' if no_lists and (xref or e['num_suspect']) else 'entry'
    parsed.append({
        'kind': kind, 'target': xref.group(1).strip(' .') if kind == 'crossref' and xref else None,
        'num': e['num'], 'num_suspect': e['num_suspect'], 'symbols': e['symbols'], 'header': header[:400],
        'name': normalize(re.split(r'[،.]', header)[0])[:200],
        'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': quotes,
        'rawa_lahu': rl.group(1).strip() if rl else None,
    })

# 4. Stats.
xrefs = [p for p in parsed if p['kind'] == 'crossref']
print(f'crossrefs (aliases pointing to another entry): {len(xrefs)}')
parsed_all, parsed = parsed, [p for p in parsed if p['kind'] == 'entry']
nums = [p['num'] for p in parsed]
print(f'entries: {len(parsed)}  num range {min(nums)}..{max(nums)}  '
      f'not increasing: {sum(1 for a, b in zip(nums, nums[1:]) if b <= a)}  '
      f'gaps: {sum(1 for a, b in zip(nums, nums[1:]) if b > a + 1)}')
for k in ('shuyukh', 'talamidh', 'quotes'):
    have = sum(1 for p in parsed if p[k])
    tot = sum(len(p[k]) for p in parsed)
    print(f'{k:9} entries with: {have:5} ({have / len(parsed):.0%})  total items: {tot}')
print('candidates rejected by LIS:', typo_like, ' recovered as typo entries:', recovered)
print('rawa_lahu:', sum(1 for p in parsed if p['rawa_lahu']))
print('top critics:', Counter(normalize(q['critic']) for p in parsed for q in p['quotes']).most_common(20))

json.dump(parsed_all, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
