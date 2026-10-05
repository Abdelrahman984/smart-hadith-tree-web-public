"""Which rijal books cover the narrators missing from the Tahdhib/Taqrib registry?

Entry heads come from each book's title store plus numbered lines in its text.
Input is the missing-name list written by gap_test.py (gap_result.json in the working directory).
Usage: python gap_books.py tahdhib.json dump_dir id1,id2,...   (order = order in which books are added)
"""
import collections
import json
import os
import re
import sys

TAHDHIB, DUMP, book_arg = sys.argv[1:4]
BOOKS = [int(b) for b in book_arg.split(',')]
NAMES = {14463: 'الروض الباسم (شيوخ الحاكم)', 29742: 'رجال الحاكم في المستدرك', 736: 'تاريخ بغداد',
         10906: 'سير أعلام النبلاء', 96165: 'الثقات ممن لم يقع في الستة', 36357: 'لسان الميزان',
         29745: 'إرشاد القاصي (شيوخ الطبراني)'}
sys.argv = [sys.argv[0], TAHDHIB]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'link_tahdhib.py'),
          encoding='utf-8').read().split('stats = {k')[0].replace("print(f'entries", "0 and print(f'entries"))

HARAKAT_RE = re.compile(r'[\u064B-\u0652\u0670\u0640]')
ENTRY_LINE = re.compile(r'(?m)^\s*(?:\[[٠-٩0-9]+\]|\(?[٠-٩0-9]+\)?\s*[-ـ])\s*([^\n]{3,160})')
NUM_PREFIX = re.compile(r'^\s*(?:\[[٠-٩0-9]+\]|\(?[٠-٩0-9]+\)?\s*[-ـ])\s*')


def heads(bid: int) -> list[str]:
    out = []
    with open(f'{DUMP}/{bid}_titles.tsv', encoding='utf-8') as f:
        for line in f:
            out.append(NUM_PREFIX.sub('', HARAKAT_RE.sub('', line.rstrip('\n').split('\t', 1)[1])))
    pages = []
    with open(f'{DUMP}/{bid}_pages.tsv', encoding='utf-8') as f:
        for line in f:
            i, b = line.rstrip('\n').split('\t', 1)
            pages.append((int(i.split('-')[1]), b.replace('\\n', '\n')))
    pages.sort()
    text = HARAKAT_RE.sub('', re.sub(r'<span[^>]*>|</span>', '\n', '\n'.join(b for _, b in pages)))
    out += [m.group(1) for m in ENTRY_LINE.finditer(text)]
    return out


def keys(toks: list[str]) -> list[tuple]:
    """ism+father, plus the kunya+nisba form ("أبو زكريا العنبري") when the name starts with a kunya."""
    ks = []
    if len(toks) >= 2:
        ks.append(('n',) + tuple(toks[:2]))
    if toks and toks[0] == 'ابو' and len(toks) >= 2:
        if len(toks) >= 4:
            ks.append(('n',) + tuple(toks[2:4]))
        ks.append(('k', toks[1], toks[-1]))
    return ks


index = collections.defaultdict(set)
for b in BOOKS:
    h = heads(b)
    for line in h:
        t = tokens(line)
        for k in keys(t):
            index[k].add(b)
        # "X بن Y، أبو Z، النسبة" -> also index kunya + each later token
        kun = re.findall(r'ابو (\S+)', norm(line))
        for kn in kun[:1]:
            for w in t[2:8]:
                index[('k', kn, w)].add(b)
    print(f'{b:>6} {NAMES.get(b, ""):30} heads: {len(h)}')

missing = json.load(open('gap_result.json', encoding='utf-8'))['missing']
# Keep only segments shaped like a narrator name: no digits, quotes, Quran brackets, verdicts or
# sentence words, at most 9 words, and "بن"/"أبو"/"ابن" or a short (1-4 word) name.
NOT_NAME = re.compile(r'[٠-٩0-9"«»﴿﴾؟?!:]|هذا حديث|يخرجاه|صحيح|ﷺ|﷿|رضي|قال|كان|لما|قلت|في |إن |إذا|كنا|يحدث|وحدثنا|حدثناه|بجميع')
junk = {n: c for n, c in missing.items() if NOT_NAME.search(n) or len(n.split()) > 9
        or not (re.search(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أم|بنت)(?:\s|$)', n) or len(n.split()) <= 4)}
print(f'segments rejected as non-names: {sum(junk.values())} of {sum(missing.values())}')
missing = {n: c for n, c in missing.items() if n not in junk}
total = 0
covered_by = collections.Counter()
first_cover = collections.Counter()
for name, c in missing.items():
    t = tokens(name)
    if len(t) < 2:
        continue
    total += c
    hits = set()
    for k in keys(t):
        hits |= index.get(k, set())
    if hits:
        first_cover[min(hits, key=BOOKS.index)] += c
        for b in hits:
            covered_by[b] += c
    else:
        first_cover['none'] += c

print(f'\nmissing full-name occurrences in the Mustadrak sample: {total}')
print('book                                    covers (alone)   adds (in the given order)')
cum = 0
for b in BOOKS:
    cum += first_cover[b]
    print(f'{NAMES.get(b, b):38} {covered_by[b] / total:>8.0%}   {first_cover[b] / total:>8.0%}  (cumulative {cum / total:.0%})')
print(f'not covered by any: {first_cover["none"] / total:.0%}')
