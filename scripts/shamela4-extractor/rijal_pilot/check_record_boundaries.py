"""Check hadith record boundaries in data/itqan/sunni/<book>/.

Classifies each record by where the numbered isnad marker ("١٠٨٣٨ - أخبرنا") sits:
- clean:         the record starts with its isnad (optionally ". N -")
- prefix tail:   some text, then "N - <isnad>" early in the record (first 30%): the isnad is the
                 record's own, but the previous hadith's last part (verdict, matn end) sits here
- shifted:       a matn first, and "N - <isnad>" late in the record (after 30%): the isnad
                 belongs to the next hadith, so matn and isnad of the record do not match
- no marker:     no numbered isnad at all (books without numbers in the text)
Usage: python check_record_boundaries.py data/itqan/sunni
"""
import glob
import json
import os
import re
import sys
from collections import Counter

ROOT = sys.argv[1]
HARAKAT = re.compile(r'[ً-ْٰـ]')
VERB = r'و?(?:أخبرنا|أخبرني|حدثنا|حدثني|أنبأنا|أنبأ|ثنا|أنا)'
OPENING = re.compile(rf'^[\s.\[\]]*(?:[٠-٩]+\s*م?\s*[-\]]\s*)?{VERB}')
MARKER = re.compile(rf'[\[(]?[٠-٩]+\s*م?\s*[-\])]\s*{VERB}')

print(f'{"book":28} {"records":>8} {"clean":>14} {"prefix tail":>14} {"SHIFTED":>14} {"no marker":>14}')
for book in sorted(os.listdir(ROOT)):
    texts = []
    for f in glob.glob(os.path.join(ROOT, book, '*.json')):
        if f.endswith('index.json'):
            continue
        data = json.load(open(f, encoding='utf-8'))
        texts += [h.get('arabic', '') for h in (data if isinstance(data, list) else [])]
    if not texts:
        continue
    c = Counter()
    for t in texts:
        t = HARAKAT.sub('', t or '')
        if OPENING.match(t):
            c['clean'] += 1
            continue
        marks = list(MARKER.finditer(t))
        if not marks:
            c['no marker'] += 1
        elif marks[0].start() <= 0.3 * len(t):
            c['prefix tail'] += 1
        else:
            c['shifted'] += 1
    n = len(texts)
    print(f'{book:28} {n:>8} ' + ' '.join(f'{c[k]:>6} ({c[k] / n:5.1%})' for k in ('clean', 'prefix tail', 'shifted', 'no marker')))
