"""Align Taqrib al-Tahdhib entries to Tahdhib al-Kamal entries.

Both books list narrators in the same order, so a monotonic walk with a small look-ahead
window is enough. Usage: python align_taqrib.py tahdhib.json taqrib.json out.json
"""
import json
import re
import sys
from collections import Counter

TAHDHIB, TAQRIB, OUT = sys.argv[1:4]
tahdhib = [e for e in json.load(open(TAHDHIB, encoding='utf-8')) if e['kind'] == 'entry']
taqrib = json.load(open(TAQRIB, encoding='utf-8'))['entries']
STOP = {'بن', 'ابن', 'بنت', 'ابو', 'ام'}


def name_tokens(s: str, n: int = 4) -> list[str]:
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub(r'\[[^\]]*\]|\([^)]*\)', ' ', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = re.sub(r'\bابي\b', 'ابو', s)
    s = re.sub(r'\bال(?=\S)', '', s)
    s = re.split(r'[،.:]| ويقال| وقيل| بفتح| بضم| بكسر| بالتصغير| مصغر', s)[0]
    return [w for w in re.sub(r'[^ء-ي ]', ' ', s).split() if w not in STOP][:n]


def sym_set(s: str) -> set[str]:
    return set(re.sub(r'[()]', ' ', s).split())


def score(a: dict, b: dict) -> float:
    ta, tb = name_tokens(a['header']), name_tokens(b['name'])
    if not ta or not tb or ta[0] != tb[0]:
        return 0.0
    common = 0
    for x, y in zip(ta, tb):            # nasab must agree position by position
        if x != y:
            break
        common += 1
    s = common / max(len(ta), len(tb))
    sa, sb = sym_set(a['symbols']), sym_set(b['symbols'])
    if sa and sb:
        s += 0.5 * len(sa & sb) / len(sa | sb)
    return s


THRESHOLD = 0.5

# 1. Anchors: a 3-token name (+ symbols) that is unique in both books.
def key(tokens: list[str], syms: str) -> tuple:
    return (*tokens[:3], frozenset(sym_set(syms)))


ka = Counter(key(name_tokens(a['header']), a['symbols']) for a in tahdhib)
kb = Counter(key(name_tokens(b['name']), b['symbols']) for b in taqrib)
kb_pos = {key(name_tokens(b['name']), b['symbols']): k for k, b in enumerate(taqrib)}
anchors = []
for i, a in enumerate(tahdhib):
    k = key(name_tokens(a['header']), a['symbols'])
    if len(k) >= 3 and ka[k] == 1 and kb.get(k) == 1:
        anchors.append((i, kb_pos[k]))
# Keep only anchors that are mutually in order (longest increasing run on the Taqrib side).
import bisect
tails, idx, parent = [], [], [-1] * len(anchors)
for n, (_, k) in enumerate(anchors):
    p = bisect.bisect_left(tails, k)
    if p == len(tails):
        tails.append(k); idx.append(n)
    else:
        tails[p] = k; idx[p] = n
    parent[n] = idx[p - 1] if p else -1
chain, n = [], idx[-1]
while n != -1:
    chain.append(anchors[n]); n = parent[n]
chain.reverse()
print(f'anchors: {len(anchors)}  in order: {len(chain)}')

# 2. Fill the gaps between consecutive anchors with a monotonic best-score walk.
pairs = [{'tahdhib': i, 'taqrib': k, 'score': 2.0} for i, k in chain]
bounds = [(-1, -1)] + chain + [(len(tahdhib), len(taqrib))]
for (i0, k0), (i1, k1) in zip(bounds, bounds[1:]):
    j = k0 + 1
    for i in range(i0 + 1, i1):
        best, best_k = 0.0, None
        for k in range(j, k1):
            sc = score(tahdhib[i], taqrib[k]) - 0.002 * (k - j)
            if sc > best:
                best, best_k = sc, k
        if best_k is not None and best >= THRESHOLD:
            pairs.append({'tahdhib': i, 'taqrib': best_k, 'score': round(best, 3)})
            j = best_k + 1
pairs.sort(key=lambda p: p['tahdhib'])

print(f'Tahdhib entries: {len(tahdhib)}  Taqrib entries: {len(taqrib)}')
print(f'aligned: {len(pairs)} ({len(pairs) / len(tahdhib):.1%} of Tahdhib)')
print('score buckets:', sorted(Counter(min(int(p['score'] * 4) / 4, 1.5) for p in pairs).items()))
sym_ok = sum(1 for p in pairs if sym_set(tahdhib[p['tahdhib']]['symbols']) == sym_set(taqrib[p['taqrib']]['symbols']))
print(f'identical book symbols on both sides: {sym_ok} ({sym_ok / len(pairs):.1%})')
json.dump(pairs, open(OUT, 'w', encoding='utf-8'), indent=0)
