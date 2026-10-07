"""«الثقات ممن لم يقع في الكتب الستة» لابن قطلوبغا (Shamela 96165) as a fallback registry file:
dump/96165_pages.tsv -> extra_thiqat.json.

Run from data/shamela_rijal, after dumping the book with ShamelaLuceneDumper:
    python ../../scripts/shamela4-extractor/rijal_pilot/parse_thiqat.py [seed]

An entry starts a line with its number: «٣٢٠٦ - حماد بن هبة الله بن حماد، أبو الثناء التاجر.» (the title mark of the
table of contents may sit inside the line, and «[٤]» marks the page of the manuscript). The lines that follow give the
shaykhs («يروي عن …») and the students («روى عنه …»). Every entry is marked "fallback" (see link_tahdhib.py).
"""
import csv
import json
import re
import sys

from parse_siyar import HARAKAT, SKIP_ITEM, NAMEISH, items

csv.field_size_limit(10 ** 9)
BOOK = 96165
START = re.compile(r'^\s*([٠-٩0-9]{1,6})\s*[-–]\s*(.+)$')
SHUYUKH = re.compile(r'(?:^|[.\s])(?:و)?(?:يروي|روى|حدث|سمع)\s+عن\s+(.*?)(?=\s(?:روى|يروي)\s+عنه|\sوروى\s+عنه|$)')
TALAMIDH = re.compile(r'(?:روى|يروي|حدث)\s+عنه\s*:?\s*(.*)')
NOTES = re.compile(r'\([٠-٩0-9]+\)|\[[^\]]{0,12}\]')


def header_of(first):
    first = NOTES.sub(' ', re.sub(r'<[^>]+>', '', first))
    first = re.sub(r'\s+', ' ', first).strip(' .:')
    first = first.split('.')[0].strip(' .،')
    return first


def main():
    pages = {}
    for r in csv.reader(open(f'dump/{BOOK}_pages.tsv', encoding='utf-8'), delimiter='\t'):
        if len(r) >= 2:
            pages[int(r[0].split('-')[1])] = r[-1]
    bs = chr(92)   # the pages carry the line break as a doubled backslash-n in places
    text = HARAKAT.sub('', '\n'.join(pages[k].replace(bs + bs + 'n', '\n').replace(bs + 'n', '\n') for k in sorted(pages)))
    lines = [ln.strip() for ln in text.split('\n')]
    starts = []
    for i, ln in enumerate(lines):
        m = START.match(re.sub(r'<[^>]+>', '', ln))
        if m and len(m.group(2).split()) >= 2:
            starts.append((i, m.group(1), m.group(2)))
    entries = []
    for k, (i, num, first) in enumerate(starts):
        end = starts[k + 1][0] if k + 1 < len(starts) else len(lines)
        header = header_of(first)
        if len(header.split()) < 2 or len(header) > 140:
            continue
        body = ' . '.join(ln for ln in lines[i + 1:end][:6] if ln)
        body = NOTES.sub(' ', re.sub(r'<[^>]+>', ' ', body))
        body = re.sub(r'\s+', ' ', body)
        talamidh, shuyukh = [], []
        for sent in re.split(r'\.\s', body):
            sent = sent.strip()
            m = re.match(r'^(?:و)?(?:روى|يروي|حدثنا|حدث)\s+عنه\s*:?\s*(.*)$', sent)
            if m:
                talamidh += items(m.group(1))
                continue
            m = re.match(r'^(?:و)?(?:روى|يروي|حدث|سمع)\s+عن\s*:?\s*(.*)$', sent)
            if m:
                shuyukh += items(m.group(1))
        entries.append({
            'kind': 'entry', 'num': num, 'num_suspect': False, 'symbols': '', 'source': 'thiqat', 'fallback': True,
            'header': header, 'name': header, 'shuyukh': shuyukh, 'talamidh': talamidh, 'quotes': [], 'rawa_lahu': None,
            'verdict': None, 'aliases': [],
        })
    json.dump(entries, open('extra_thiqat.json', 'w', encoding='utf-8'), ensure_ascii=False)
    print('entries:', len(entries), '| with shuyukh:', sum(1 for e in entries if e['shuyukh']), '| with talamidh:',
          sum(1 for e in entries if e['talamidh']))
    if len(sys.argv) > 1:
        import random
        random.seed(int(sys.argv[1]))
        for e in random.sample(entries, 10):
            print('\n', e['num'], e['header'][:90], '| shuyukh', len(e['shuyukh']), [x['name'] for x in e['shuyukh'][:3]],
                  '| talamidh', len(e['talamidh']), [x['name'] for x in e['talamidh'][:3]])


if __name__ == '__main__':
    main()
