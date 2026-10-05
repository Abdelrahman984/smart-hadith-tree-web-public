"""Map the Phase 4 records (data/shamela/<slug>/) to the current system's hadiths (current_chains*.json).

The current database was built from other record boundaries (and, for the 12 primary books, from
Itqan's text), so the bench can no longer pair a record with its current chain by the first 120
characters. Each new record is matched by word trigrams of its opening (the isnad and the matn's
start) against the current hadiths' heads (their first 400 characters). A head may begin with the
previous hadith's end (the shifted records of docs/shamela_migration.md 6.1), so the score is the
share of the record's trigrams found in the head, not a symmetric overlap.

Usage: python map_records.py <data/shamela/slug> <current_chains.json> <out.json>
Output: {"book": <current Hadiths.BookName>, "map": {key(record text): current id}, "stats": {...}}
compare_current.py reads it when CURRENT_MAP=<out.json> is set.
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict

BOOK_DIR, CURRENT, OUT = sys.argv[1:4]
MIN_SCORE, MIN_MARGIN = 0.5, 0.15

HARAKAT = re.compile(r'[ً-ْٰـ]')


def key(text: str) -> str:
    """The same key as compare_current.py."""
    return re.sub(r'\s+', ' ', HARAKAT.sub('', text or ''))[:120]


def words(text: str, n: int) -> list[str]:
    t = HARAKAT.sub('', text or '')
    t = re.sub('[‌-‏‪-‮﻿]', '', t)
    t = t.translate(str.maketrans('أإآىة', 'ااايه'))
    t = re.sub(r'[^ء-ي\s]', ' ', t)
    return t.split()[:n]


def grams(ws: list[str]) -> set[tuple[str, ...]]:
    return {tuple(ws[i:i + 3]) for i in range(len(ws) - 2)}


records = []
for f in glob.glob(os.path.join(BOOK_DIR, '*.json')):
    data = json.load(open(f, encoding='utf-8'))
    if isinstance(data, list):
        records += [r for r in data if isinstance(r, dict) and r.get('arabic')]

current = json.load(open(CURRENT, encoding='utf-8'))
index = defaultdict(set)
head_grams = []
for i, h in enumerate(current):
    g = grams(words(h['head'], 80))
    head_grams.append(g)
    for x in g:
        index[x].add(i)


def best(r: dict, allowed: set[int] | None) -> tuple[int | None, float, float]:
    g = grams(words(r['arabic'], 70))
    if len(g) < 5:
        return None, 0.0, 0.0
    hits = Counter()
    for x in g:
        for i in index.get(x, ()):
            if allowed is None or i in allowed:
                hits[i] += 1
    top = hits.most_common(2)
    if not top:
        return None, 0.0, 0.0
    s1 = top[0][1] / len(g)
    s2 = top[1][1] / len(g) if len(top) > 1 else 0.0
    return top[0][0], s1, s2


# The current file can hold several books: the book is the one most records match.
votes = Counter()
for r in records[::max(1, len(records) // 300)]:
    i, s1, s2 = best(r, None)
    if i is not None and s1 >= MIN_SCORE:
        votes[current[i]['book']] += 1
book = votes.most_common(1)[0][0] if votes else None
allowed = {i for i, h in enumerate(current) if h['book'] == book}

# For the 19 books built earlier from the same Shamela editions, the current hadith's number is the
# edition's: the "N -" marker inside its head (the isnad after it is the one the current chain was read
# from), else its own number. When most text matches agree with it, it is required: a shared matn and
# the end of an isnad («عن أبي إسحاق ...») can otherwise pair two different hadiths.
DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
MARK = re.compile(r'([٠-٩]+)\s*[-–]\s*(?:و?(?:أخبرنا|أخبرني|حدثنا|حدثني|أنبأنا|ثنا|نا|أنا)|وفي ذلك)')


def edition_number(h: dict) -> int | None:
    m = MARK.search(HARAKAT.sub('', h['head']))
    return int(m.group(1).translate(DIGITS)) if m else h.get('number')


checked = [(r, best(r, allowed)) for r in records]
same = [r.get('number') == edition_number(current[i]) for r, (i, s1, s2) in checked
        if i is not None and s1 >= MIN_SCORE and s1 - s2 >= MIN_MARGIN and r.get('number') is not None]
BY_NUMBER = len(same) >= 50 and sum(same) / len(same) >= 0.8

# One record per current hadith: an old record often held two hadiths, so two new records can match
# its head; the better-scoring one keeps it.
st, pick = Counter(), {}
for r, (i, s1, s2) in checked:
    if i is None or s1 < MIN_SCORE:
        st['no match'] += 1
    elif s1 - s2 < MIN_MARGIN:
        st['ambiguous'] += 1
    elif BY_NUMBER and r.get('number') != edition_number(current[i]):
        st['number differs'] += 1
    elif i not in pick or s1 > pick[i][0]:
        if i in pick:
            st['lost to a better record'] += 1
        pick[i] = (s1, key(r['arabic']))
    else:
        st['lost to a better record'] += 1
out = {k: current[i]['id'] for i, (_, k) in pick.items()}
st['matched'] = len(out)
st['records'] = len(records)
st['current hadiths of the book'] = len(allowed)
st['number check'] = f'{sum(same)}/{len(same)} agree, ' + ('required' if BY_NUMBER else 'not used')
json.dump({'book': book, 'map': out, 'stats': dict(st)}, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False)
print(book, dict(st))
