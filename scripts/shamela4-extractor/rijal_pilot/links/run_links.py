"""Run our resolver (compare_current.py, SAVE) on a bigger sample of the 7 Shamela-linked books (draft).

Unlike bench.py, every sampled record is kept (bench pairs records with the current DB's hadiths and drops
the ~10-35% without a pair): a dummy 'current' file and map are written next to the results.
Output: data/shamela_rijal/review/links/<book>.json (SAVE format) and <book>_cur.json / <book>_map.json.
Usage (any dir): python run_links.py [book ...]
"""
import json
import os
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

from s1_common import *

COMPARE = os.path.join(HERE_PILOT, 'compare_current.py')
KEY = lambda t: re.sub(r'\s+', ' ', HARAKAT.sub('', t or ''))[:120]
BOOKS = {
    'bukhari': ('محمد بن إسماعيل بن إبراهيم بن المغيرة', 2000),
    'muslim': ('مسلم بن الحجاج بن مسلم', 2000),
    'abudawud': ('سليمان بن الأشعث بن شداد', 2000),
    'tirmidhi': ('محمد بن عيسى بن سورة', 2000),
    'nasai': ('أحمد بن شعيب بن علي', 2000),
    'malik': ('مالك بن أنس بن مالك', 3000),            # all 2,946 records
    'musnad_tayalisi': ('سليمان بن داود بن الجارود', 3000),   # all 2,937 records
}


def run(book):
    compiler, n = BOOKS[book]
    d = os.path.join(SHAMELA, book)
    recs = []
    for f in os.listdir(d):
        if f.endswith('.json') and f not in ('book.json', 'index.json'):
            recs += [r for r in json.load(open(os.path.join(d, f), encoding='utf-8')) if r.get('arabic')]
    cur = [{'id': r['id'], 'head': r['arabic'][:300], 'links': []} for r in recs]
    cur_f = os.path.join(LINKS, f'{book}_cur.json')
    map_f = os.path.join(LINKS, f'{book}_map.json')
    json.dump(cur, open(cur_f, 'w', encoding='utf-8'), ensure_ascii=False)
    json.dump({'book': book, 'map': {KEY(r['arabic']): r['id'] for r in recs}}, open(map_f, 'w', encoding='utf-8'),
              ensure_ascii=False)
    out = os.path.join(LINKS, f'{book}.json')
    env = dict(os.environ, SAVE=out, CURRENT_MAP=map_f, PYTHONIOENCODING='utf-8')
    p = subprocess.run([sys.executable, COMPARE, 'tahdhib.json', d, cur_f, compiler, str(n), '0'], cwd=RIJAL,
                       capture_output=True, text=True, encoding='utf-8', env=env)
    return book, (p.stdout[:600] if not p.returncode else p.stderr[-800:])


if __name__ == '__main__':
    os.makedirs(LINKS, exist_ok=True)
    books = sys.argv[1:] or list(BOOKS)
    print(run(books[0]))
    with ThreadPoolExecutor(max_workers=3) as pool:
        for r in pool.map(run, books[1:]):
            print(r)
