"""Compare the Shamela registry's isnad resolution with the current (Itqan-based) system.

Same hadith sample as gap_test.py. For each hadith the current system's chain comes from the
Transmissions table (exported by export_chains.ps1). Prints coverage for both systems and
side-by-side chains for manual review.
Usage: python compare_current.py tahdhib.json book_dir current_chains.json compiler_prefix sample [show]
"""
import json
import os
import re
import sys
from collections import Counter

TAHDHIB, BOOK_DIR, CURRENT, COMPILER_PREFIX, SAMPLE, *rest = sys.argv[1:]
SHOW = int(rest[0]) if rest else 15
sys.argv = [sys.argv[0], TAHDHIB, BOOK_DIR, SAMPLE, COMPILER_PREFIX]
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'gap_test.py'), encoding='utf-8').read()
src = src.replace("exec(open(__file__.replace('gap_test.py', 'link_tahdhib.py')",
                  "exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'link_tahdhib.py')")
exec(src.split('MAX_DEPTH = 8')[0])
# CHAIN_MODE=greedy keeps the old link-by-link walk; the default resolves each chain jointly.
JOINT = os.environ.get('CHAIN_MODE', 'joint') != 'greedy'
if JOINT:
    exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'chain_resolver.py'), encoding='utf-8').read())

HARAKAT = re.compile(r'[ً-ْٰـ]')


def key(text: str) -> str:
    return re.sub(r'\s+', ' ', HARAKAT.sub('', text or ''))[:120]


current = {}
_current = json.load(open(CURRENT, encoding='utf-8'))
for h in _current:
    current.setdefault(key(h['head']), h)
# The Phase 4 texts (data/shamela/) have other record boundaries: map_records.py pairs them with the
# current hadiths by text, and CURRENT_MAP replaces the 120-character key.
if os.environ.get('CURRENT_MAP'):
    _by_id = {h['id']: h for h in _current}
    current = {k: _by_id[i] for k, i in json.load(open(os.environ['CURRENT_MAP'], encoding='utf-8'))['map'].items()
               if i in _by_id}

# Names in the first two positions of at least 20% of the sample's chains.
_open = Counter(s for t in sample for s in [x for x in chain_segments(t) if is_name(x)][:2])
openers = {s for s, n in _open.items() if n >= 0.2 * len(sample)}


verbs_of: dict[str, list[list[str]]] = {}   # text -> the verbs after each name of our_chain(text), for SAVE
ties_of: dict[str, dict[int, list[int]]] = {}  # text -> position -> tied candidates of an undecided name
# CHAIN_BRANCHES=0 keeps one chain per record (the flattened isnad); the default splits tahwil (tahwil.py).
BRANCHES = JOINT and os.environ.get('CHAIN_BRANCHES', '1') != '0'
if BRANCHES:
    exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'tahwil.py'), encoding='utf-8').read())
branch_data: dict[str, list[dict]] = {}     # text -> every branch: {'chain', 'verbs', 'ties', 'part', 'complete'}
BRANCH_CAP = 14                             # names per branch when a record has several (head + shared tail)


def _name_pairs(raw: list[tuple[str, list[str]]]) -> list[tuple[str, list[str]]]:
    pairs: list[tuple[str, list[str]]] = []
    for s, verbs in raw:
        # «حدثني أبي» (the speaker's own father; is_name rejects the bare kunya), «عن أبيه», «عن جده».
        kin = kin_form(s) if JOINT else None
        if kin and not pairs:
            continue                        # no narrator before it (a long name was dropped): nobody's father
        if kin:
            s = kin
        if kin or is_name(s):
            pairs.append((s, list(verbs)))
        elif pairs:                         # a segment that is not a name: its verbs go on from the last name
            pairs[-1][1].extend(verbs)
    return pairs


def _resolve_pairs(pairs: list[tuple[str, list[str]]], cap: int = 8):
    """(chain, verbs, ties) of one chain: the compiler's own name is dropped, then the names are resolved jointly."""
    segs = [s for s, _ in pairs]
    # The book's transmitter and the compiler himself open some chains: "حدثني يحيى، عن مالك،
    # عن نافع" (al-Muwatta), "حدثنا الحميدي، ثنا سفيان" (Musnad al-Humaydi). The chain starts
    # after the compiler's own name: a name whose candidates include the compiler and that is
    # either his most-cited candidate or one of the book's usual openers ("الحميدي"), so
    # "محمد" in a Bukhari isnad is not taken for al-Bukhari.
    if compiler is not None:
        for i, s in enumerate(segs[:2]):
            c = candidates(s)
            if compiler in c and (max(c, key=lambda j: fame[j]) == compiler or s in openers):
                segs, pairs = segs[i + 1:], pairs[i + 1:]
                break
    # «حدثنا عبد الله، حدثني أبي، حدثنا X» (Musnad Ahmad's Zawa'id): the son is the book's transmitter and
    # «أبي» is the compiler himself, so the chain starts after the pair.
    if compiler is not None and JOINT:
        for i, s in enumerate(segs[:3]):
            if (s == 'أبيه' and i > 0 and segs[i - 1] in openers
                    and any(compiler in father_of(j, True) for j in candidates(segs[i - 1]))):
                segs, pairs = segs[i + 1:], pairs[i + 1:]
                break
    segs = segs[:cap]
    verbs = [v for _, v in pairs[:cap]]
    out = resolve(segs, compiler)
    return out, verbs, dict(last_ties)


def join_heads(items: list[dict]) -> None:
    """A head that stops short of the Prophet («ح وحدثنا ...») takes the tail of the next complete part, from the
    narrator its last resolved narrator is listed with (edge > 0), so a wrong join is not made. Done in place."""
    for it in items:
        if it['complete']:
            continue
        chain = it['chain']
        last = max((k for k, (_, j, _) in enumerate(chain) if j is not None), default=None)
        if last is None:
            continue
        head = chain[last][1]
        for other in items:
            if not other['complete'] or other['part'] <= it['part']:
                continue
            tail = other['chain']
            for k in range(1, len(tail)):
                j = tail[k][1]
                if j is None:
                    continue
                if j == head:
                    start = k + 1
                elif edge(head, j) >= 1:
                    start = k
                else:
                    continue
                shift = last + 1 - start
                it['chain'] = chain[:last + 1] + tail[start:]
                it['verbs'] = it['verbs'][:last + 1] + other['verbs'][start:]
                it['ties'] = {**{p: v for p, v in it['ties'].items() if p <= last},
                              **{p + shift: v for p, v in other['ties'].items() if p >= start}}
                it['complete'] = True
                break
            if it['complete']:
                break


def our_branches(text: str) -> list[dict]:
    """Every chain of the isnad, resolved: [{'chain', 'verbs', 'ties', 'part', 'complete'}, ...]."""
    if text in branch_data:
        return branch_data[text]
    raws = chain_branches(text) if BRANCHES else [{'pairs': chain_segments_verbs(text), 'part': 0, 'complete': True}]
    items = []
    for raw in raws:
        pairs = _name_pairs(raw['pairs'])
        chain, verbs, ties = _resolve_pairs(pairs, BRANCH_CAP if len(raws) > 1 else 8)
        items.append({'chain': chain, 'verbs': verbs, 'ties': ties, 'part': raw['part'], 'complete': raw['complete']})
    if len(items) > 1:
        join_heads(items)
        # the same chain twice (a head joined to a tail another part already gave)
        seen, unique = set(), []
        for it in items:
            k = tuple((s, j) for s, j, _ in it['chain'])
            if k not in seen:
                seen.add(k)
                unique.append(it)
        items = unique
    branch_data[text] = items
    verbs_of[text], ties_of[text] = items[0]['verbs'], items[0]['ties']
    return items


def our_chain(text: str) -> list[tuple[str, int | None, str]]:
    """The first chain of the isnad (the one the benchmark measures)."""
    if JOINT:
        return our_branches(text)[0]['chain']
    pairs = _name_pairs(chain_segments_verbs(text))
    segs = [s for s, _ in pairs]
    if compiler is not None:
        for i, s in enumerate(segs[:2]):
            c = candidates(s)
            if compiler in c and (max(c, key=lambda j: fame[j]) == compiler or s in openers):
                segs, pairs = segs[i + 1:], pairs[i + 1:]
                break
    segs = segs[:8]
    verbs_of[text] = [v for _, v in pairs[:8]]
    out, prev = [], compiler
    for depth, seg in enumerate(segs):
        c = lookup(seg, prev)
        within = c & shuyukh_of[prev] if prev is not None else set()
        if len(within) > 1 and depth + 1 < len(segs):
            within = {j for j in within if lookup(segs[depth + 1], j) & shuyukh_of[j]} or within
        if len(within) > 1 and (p := fame_pick(within)) is not None:
            within = {p}
        if len(within) == 1:
            prev = next(iter(within)); out.append((seg, prev, 'teacher'))
        elif len(c) == 1:
            prev = next(iter(c)); out.append((seg, prev, 'unique'))
        else:
            prev = None; out.append((seg, None, 'ambiguous' if c else 'missing'))
    return out


matched, rows = 0, []
tot = Counter()
for text in sample:
    cur = current.get(key(text))
    if cur is None:
        continue
    matched += 1
    ours = our_chain(text)
    # The current system's first chain (until the first tahwil reset of StepOrder).
    links, last = [], 0
    for l in sorted(cur['links'], key=lambda l: 0):
        if l['step'] <= last:
            break
        links.append(l); last = l['step']
    tot['name segments'] += len(ours)
    tot['ours resolved'] += sum(1 for _, j, _ in ours if j is not None)
    tot['current links'] += len(links)
    tot['current links (first chain only)'] += len(links)
    tot['hadiths with no current link'] += not cur['links']
    tot['hadiths with nothing resolved by ours'] += not any(j is not None for _, j, _ in ours)
    rows.append((text, ours, links))

print(f'sample: {len(sample)}  matched in DB: {matched}')
n = tot['name segments']
print(f'name segments (first 8 per chain): {n}')
print(f'  ours   : resolved {tot["ours resolved"]} ({tot["ours resolved"] / n:.0%})')
print(f'  current: links    {tot["current links"]} ({tot["current links"] / n:.0%})  '
      f'[a link = one narrator identified]')
print(f'  hadiths with no narrator identified: ours {tot["hadiths with nothing resolved by ours"]}, '
      f'current {tot["hadiths with no current link"]}')

# Agreement: for every narrator we resolved, does the current chain contain the same person
# (same ism + father)? Agreement between two independent systems is strong evidence of a
# correct link; disagreements are listed for manual review.
def ism_father(name: str) -> tuple:
    return tuple(nasab_chain(re.split(r'[،:.\n]', name.lstrip('- '))[0])[:2])


def named_in_kunya_entry(j: int, sheikh: str) -> bool:
    """A Companion known by his kunya has a kunya-only Tahdhib entry ("أبو هريرة الدوسي") whose
    header gives the names proposed for him ("اسمه عبد الرحمن بن صخر"); the current system uses one."""
    head = entries[j]['header']
    if not re.match(r'\s*(?:أبو|أم)\s', head):
        return False
    words = re.split(r'[،:.\n]', sheikh)[0].split()
    n = 4 if words[:1] == ['عبد'] else 3            # "عبد الرحمن بن صخر"
    name = ' '.join(words[:n])
    flat = re.sub(r'\s+', ' ', HARAKAT.sub('', head[:800]))     # not key(): it keeps 120 characters
    return len(words) >= n and key(name) in flat


# Itqan ids mapped to registry entries (review/itqan_id_map.json, by header): the current system names
# some narrators wrongly but means the right person («نافع بن همام» is Nafi' mawla Ibn Umar, «عكرمة بن
# منصور» is Ikrima mawla Ibn Abbas, «محمد بن أبي عمرة» is Ibn Sirin).
itqan_to = {}
_map = os.path.join(os.path.dirname(os.path.abspath(TAHDHIB)), 'review', 'itqan_id_map.json')
if os.path.exists(_map):
    _by_head = {}
    for _j, _e in enumerate(entries):
        _by_head.setdefault(re.sub(r'\s+', ' ', _e['header'])[:90], _j)
    for _x in json.load(open(_map, encoding='utf-8'))['ids']:
        if _x.get('registry_header') and (_k := re.sub(r'\s+', ' ', _x['registry_header'])[:90]) in _by_head:
            itqan_to[_x['itqan_id']] = _by_head[_k]

agree, disagree, beyond, cases = 0, 0, 0, []
vs_current = {}                     # (row, position) -> 'agree' / 'differ' / 'past', for SAVE
for r, (text, ours, links) in enumerate(rows):
    now = {ism_father(l['sheikh']) for l in links}
    now_ids = {itqan_to.get(l.get('sheikhItqan')) for l in links} - {None}
    for pos, (seg, j, how) in enumerate(ours):
        if j is None:
            continue
        # alt_nasab: the Taqrib form of a typo'd Tahdhib header ("عبيد الله بن عتبة" for عبيد الله بن
        # عبد الله بن عتبة) is the same narrator.
        if (j in now_ids or ism_father(entries[j]['header']) in now or (alt_nasab[j] and tuple(alt_nasab[j][:2]) in now)
                or any(named_in_kunya_entry(j, l['sheikh']) for l in links)):
            agree += 1
            vs_current[r, pos] = 'agree'
        elif pos >= len(links):
            # The current chain stops before this name ("... ← عروة" without عائشة): nothing to
            # compare with, so it is neither agreement nor disagreement.
            beyond += 1
            vs_current[r, pos] = 'past'
        else:
            disagree += 1
            vs_current[r, pos] = 'differ'
            cases.append((seg, entries[j]['header'][:45].replace('\n', ' '), how,
                          ' ← '.join(l['sheikh'][:22] for l in links)))
print(f'our resolved narrators found in the current chain: {agree} agree, {disagree} differ '
      f'({agree / max(1, agree + disagree):.0%} agreement); {beyond} past the end of the current chain')
# SAVE=<file>: every sampled isnad with each name's resolution (ours) and the current chain, so a
# run can be reviewed later or diffed against another run.
if os.environ.get('SAVE'):
    # Registry ids as pipeline.py writes them (tk<num> for Tahdhib, <source>:<num> for the other books).
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from pipeline import registry_ids
    reg_id = registry_ids(entries, sum(1 for e in json.load(open(TAHDHIB, encoding='utf-8')) if e.get('kind') == 'entry'))

    def _ties(text, pos):
        return [reg_id[x] for x in ties_of.get(text, {}).get(pos, [])] or None

    json.dump([{'isnad': re.sub(r'\s+', ' ', HARAKAT.sub('', text))[:400],
                'ours': [{'name': seg, 'narrator': entries[j]['header'][:100] if j is not None else None,
                          'source': entries[j].get('source', 'tahdhib') if j is not None else None, 'how': how,
                          'vs_current': vs_current.get((r, pos)),
                          'id': reg_id[j] if j is not None else None,
                          'verbs': verbs_of.get(text, [])[pos] if pos < len(verbs_of.get(text, [])) else [],
                          'ties': _ties(text, pos)}
                         for pos, (seg, j, how) in enumerate(ours)],
                'current': [l['sheikh'][:100] for l in links]} for r, (text, ours, links) in enumerate(rows)],
              open(os.environ['SAVE'], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

print('by how ours resolved them:', Counter(c[2] for c in cases))
import random
random.seed(5)
for seg, head, how, now_chain in random.sample(cases, min(int(os.environ.get('SHOW_DIFF', '0')), len(cases))):
    print(f'  DIFF [{how}] "{seg[:28]}" → {head}\n         now: {now_chain[:150]}')
random.seed(99)
for text, ours, links in random.sample(rows, min(SHOW, len(rows))):
    print('\n' + '─' * 100)
    print('ISNAD :', re.sub(r'\s+', ' ', HARAKAT.sub('', text))[:230])
    print('OURS  :', ' ← '.join(
        (entries[j]['header'][:32].replace('\n', ' ') if j is not None else f'?{seg[:20]}?') for seg, j, _ in ours))
    print('NOW   :', ' ← '.join(l['sheikh'][:32] for l in links) or '(no links)')
