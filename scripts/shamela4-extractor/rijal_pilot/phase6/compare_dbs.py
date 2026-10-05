"""Phase 6: compare the v2 database with the Shamela-built one, book by book, on the full sets.

Input (from export_db_chains.ps1): data/shamela_rijal/phase6/{v2,shamela}/NN.jsonl, one file per book.
Pairing: a Shamela hadith is matched to a v2 hadith by word trigrams of its opening (the same idea as
map_records.py; v2 heads may begin with the previous hadith's tail, so the score is the share of the
Shamela trigrams found in the v2 head). Narrators are compared as registry ids: a v2 narrator's Itqan id
is translated with review/itqan_id_map_all.json (exact and probable matches); the Shamela side already
carries the registry id (Narrators.SourceKey).

Usage: python compare_dbs.py [out.json]      (run from the repo root; PYTHONIOENCODING=utf-8 on Windows)
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = 'data/shamela_rijal'
OUT = sys.argv[1] if len(sys.argv) > 1 else f'{ROOT}/phase6/compare.json'
MIN_SCORE, MIN_MARGIN = 0.5, 0.15
HARAKAT = re.compile(r'[ً-ْٰـ]')


def words(text, n):
    t = HARAKAT.sub('', text or '')
    t = re.sub('[‌-‏‪-‮﻿]', '', t)
    t = t.translate(str.maketrans('أإآىة', 'ااايه'))
    t = re.sub(r'[^ء-ي\s]', ' ', t)
    return t.split()[:n]


def grams(ws):
    return {tuple(ws[i:i + 3]) for i in range(len(ws) - 2)}


registry = json.load(open(f'{ROOT}/registry.json', encoding='utf-8'))
imap = json.load(open(f'{ROOT}/review/itqan_id_map_all.json', encoding='utf-8'))['ids']
# registry_index was written against an older registry (63 entries were renumbered later), so an index
# is accepted only when the entry's header still starts with the recorded one; otherwise the header is
# looked up (kept only when it is unique).
by_header = defaultdict(list)
for i, r in enumerate(registry):
    by_header[r['header'][:60]].append(i)
ITQAN_TO_REG, stale = {}, 0
for e in imap:
    if e['match'] not in ('exact', 'probable') or e.get('registry_index') is None:
        continue
    idx, want = e['registry_index'], e['registry_header'][:60]
    if not (idx < len(registry) and registry[idx]['header'][:60] == want):
        stale += 1
        c = by_header.get(want, [])
        if len(c) != 1:
            continue
        idx = c[0]
    ITQAN_TO_REG[str(e['itqan_id'])] = registry[idx]['id']
print(f'{len(ITQAN_TO_REG)} Itqan ids mapped ({stale} re-resolved by header)')


def load(side):
    books = {}
    for f in sorted(glob.glob(f'{ROOT}/phase6/{side}/*.jsonl')):
        rows = [json.loads(l) for l in open(f, encoding='utf-8')]
        if rows:
            books[rows[0]['book']] = rows
    return books


def chain_ids(h, side):
    """Narrators of a hadith's chain, in order, as registry ids (None: unmapped / no key)."""
    out = []
    for step, sk, tk, term, sn, tn in sorted(h['links'], key=lambda l: l[0]):
        for k, n in ((tk, tn), (sk, sn)):
            rid = k if side == 'shamela' else ITQAN_TO_REG.get(k)
            out.append((rid, n))
    # keep the first occurrence order, drop repeats caused by shared endpoints
    seen, res = set(), []
    for rid, n in out:
        key = rid or ('?' + (n or ''))
        if key not in seen:
            seen.add(key)
            res.append((rid, n))
    return res


POS = 4
DIFFS = []
PAIRS = {}
v2, sh = load('v2'), load('shamela')
report, examples = {}, {}
tot = Counter()
for book in sorted(set(v2) | set(sh)):
    a, b = v2.get(book, []), sh.get(book, [])
    index = defaultdict(set)
    ag = []
    for i, h in enumerate(a):
        g = grams(words(h['head'], 80))
        ag.append(g)
        for x in g:
            index[x].add(i)
    pairs, taken = [], {}
    for j, h in enumerate(b):
        g = grams(words(h['head'], 70))
        if len(g) < 5:
            continue
        hits = Counter()
        for x in g:
            for i in index.get(x, ()):
                hits[i] += 1
        top = hits.most_common(2)
        if not top:
            continue
        s1 = top[0][1] / len(g)
        s2 = top[1][1] / len(g) if len(top) > 1 else 0.0
        if s1 >= MIN_SCORE and s1 - s2 >= MIN_MARGIN:
            i = top[0][0]
            if i not in taken or s1 > taken[i][0]:
                taken[i] = (s1, j)
    pairs = [(i, j) for i, (s, j) in taken.items()]

    def stats(rows, side):
        lens = [len(chain_ids(h, side)) for h in rows]
        n = len(rows) or 1
        return {'hadiths': len(rows), 'no_chain': sum(1 for x in lens if x == 0) / n,
                'short_le1': sum(1 for x in lens if x <= 1) / n,
                'mean_len': sum(lens) / n}

    agree = diff = only_sh = only_v2 = unmapped = 0
    ex = []
    for i, j in pairs:
        ca, cb = chain_ids(a[i], 'v2'), chain_ids(b[j], 'shamela')
        sa = {r for r, _ in ca if r}
        sb = {r for r, _ in cb if r}
        unmapped += sum(1 for r, _ in ca if not r)
        both = sa & sb
        agree += len(both)
        only_sh += len(sb - sa)
        only_v2 += len(sa - sb)
        if (sb - sa) or (sa - sb):
            if len(ex) < 12:
                ex.append({'v2': a[i]['id'], 'shamela': b[j]['id'], 'head': b[j]['head'][:120],
                           'only_v2': [n for r, n in ca if r in sa - sb],
                           'only_shamela': [n for r, n in cb if r in sb - sa]})
    # Position-aligned comparison of the first POS narrators after the compiler (both lists start at the
    # compiler), so a second isnad that v2 appends to the same chain does not count.
    PAIRS[book] = [(a[i]['id'], b[j]['id']) for i, j in pairs]
    pos = Counter()
    pos_diff = []
    for i, j in pairs:
        ca, cb = chain_ids(a[i], 'v2')[1:1 + POS + 1], chain_ids(b[j], 'shamela')[1:1 + POS + 1]

        def same_at(o):
            """Matches when v2's chain is shifted by o places (o=1: v2 has an extra leading narrator)."""
            return sum(1 for k in range(len(cb)) if 0 <= k + o < len(ca) and ca[k + o][0] and ca[k + o][0] == cb[k][0])
        best = max((0, 1, -1), key=same_at)
        if best == 1:
            pos['v2_extra_lead'] += 1
            ca = ca[1:]
        elif best == -1:
            pos['shamela_extra_lead'] += 1
            cb = cb[1:]
        ca, cb = ca[:POS], cb[:POS]
        for k in range(max(len(ca), len(cb))):
            if k >= len(cb):
                pos['v2_only'] += 1
            elif k >= len(ca):
                pos['shamela_only'] += 1
            elif ca[k][0] is None:
                pos['v2_unmapped'] += 1
            elif cb[k][0] is None:
                pos['shamela_unresolved'] += 1
            elif ca[k][0] == cb[k][0]:
                pos['same'] += 1
            else:
                pos['differ'] += 1
                if len(pos_diff) < 400:
                    pos_diff.append({'book': book, 'shamela': b[j]['id'], 'v2': a[i]['id'], 'k': k + 1,
                                     'head': b[j]['head'][:200], 'v2_name': ca[k][1], 'shamela_name': cb[k][1],
                                     'shamela_chain': [n for _, n in cb], 'v2_chain': [n for _, n in ca]})
    DIFFS.extend(pos_diff)
    report[book] = {'positions': dict(pos),'v2': stats(a, 'v2'), 'shamela': stats(b, 'shamela'), 'paired': len(pairs),
                    'narrators_both': agree, 'only_shamela': only_sh, 'only_v2': only_v2,
                    'v2_unmapped': unmapped}
    examples[book] = ex
    print(f"{book}: v2 {len(a)} / shamela {len(b)}, paired {len(pairs)}, both {agree}, "
          f"only-shamela {only_sh}, only-v2 {only_v2}", flush=True)

json.dump({'report': report, 'examples': examples, 'position_diffs': DIFFS},open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
json.dump(PAIRS, open(OUT.replace('compare.json', 'pairs.json'), 'w', encoding='utf-8'), ensure_ascii=False)
print('written', OUT)
