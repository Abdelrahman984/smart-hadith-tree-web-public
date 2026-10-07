"""Pilot: resolve the names in Tahdhib al-Kamal's shuyukh/talamidh lists to entries of the same book.

A link A -> B (B listed as a shaykh of A) is "confirmed" when B's own entry lists A as a student.
Usage: python link_tahdhib.py tahdhib.json
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict
from functools import lru_cache

data = json.load(open(sys.argv[1], encoding='utf-8'))
entries = [e for e in data if e['kind'] == 'entry']
# Entries from other rijal books (e.g. extra_shaykh_books.json from parse_shaykh_books.py) sit next to tahdhib.json.
for _extra in sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(sys.argv[1])), 'extra_*.json'))):
    entries += [e for e in json.load(open(_extra, encoding='utf-8')) if e['kind'] == 'entry']
xrefs = [e for e in data if e['kind'] == 'crossref']


def _key3(header: str) -> tuple:
    h = re.sub(r'[ً-ْٰـ]', '', re.split(r'[،.\n]', header)[0])
    h = re.sub('[أإآ]', 'ا', h).replace('ى', 'ي').replace('ة', 'ه')
    return tuple(w for w in re.sub(r'[^ء-ي ]', ' ', h).split() if w not in ('بن', 'ابن'))[:3]


# The Thiqat of Ibn Qutlubugha repeats many narrators that Lisan and Siyar already list (and lists some twice), which
# turned names those two had decided into ties. It is the last fallback: an entry is kept only when neither of them has
# a narrator with the same first three names.
_early = {_key3(e['header']) for e in entries if e.get('source') in ('lisan', 'siyar')}
entries = [e for e in entries if not (e.get('source') == 'thiqat' and _key3(e['header']) in _early)]


def cached(tag: str, compute):
    """Load `compute()`'s result from data/<...>/cache/ when nothing it depends on has changed.

    The key hashes the bytes of every registry file (tahdhib.json, taqrib.json, align.json,
    extra_*.json) and of every script in rijal_pilot/, so any edit to data or code recomputes.
    Off with NO_CACHE=1, and off when the scripts' folder cannot be found (code exec'd from
    elsewhere), since then the key would not cover the code in use."""
    import hashlib
    import pickle
    data_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
    code_dir = os.path.dirname(os.path.abspath(globals().get('__file__', '')))
    if os.environ.get('NO_CACHE') or not os.path.exists(os.path.join(code_dir, 'link_tahdhib.py')):
        return compute()
    h = hashlib.sha256(tag.encode())
    files = [os.path.join(data_dir, f) for f in ('tahdhib.json', 'taqrib.json', 'align.json')]
    files += sorted(glob.glob(os.path.join(data_dir, 'extra_*.json')))
    files += sorted(glob.glob(os.path.join(code_dir, '*.py')))
    for f in files:
        if os.path.exists(f):
            h.update(os.path.basename(f).encode())
            h.update(open(f, 'rb').read())
    path = os.path.join(data_dir, 'cache', f'{tag}_{h.hexdigest()[:20]}.pkl')
    if os.path.exists(path):
        with open(path, 'rb') as f:
            return pickle.load(f)
    result = compute()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for old in glob.glob(os.path.join(data_dir, 'cache', f'{tag}_*.pkl')):   # keep one version per tag
        if old != path:
            try:
                os.remove(old)
            except OSError:                 # another run (in parallel) is reading it
                pass
    tmp = f'{path}.{os.getpid()}.tmp'       # parallel runs each write their own file, then swap it in
    with open(tmp, 'wb') as f:
        pickle.dump(result, f)
    try:
        os.replace(tmp, path)
    except OSError:                         # Windows: the file is open in another run; it has the same data
        os.remove(tmp)
    return result
STOP = {'بن', 'ابن', 'بنت', 'ويقال', 'يقال', 'وهو', 'مولي', 'مولاهم', 'نزيل', 'صاحب', 'والد', 'اخو', 'ام', 'ثم'}


GLUED_ABU = re.compile(r'\bابو(?!(?:ه|ها|هم|هما|اب|ا|اه|ين|ي)\b)(?=\S)')


def norm(s: str) -> str:
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    # Kunyas glued in the edition: "أبوالتياح", "أبويحيى" (not "أبوه", "أبواب", "أبوين").
    s = GLUED_ABU.sub('ابو ', s)
    # Genitive kunya in lists: "عن أبي مسلم". "أبي بن كعب" is a name (Ubayy), not a kunya.
    s = re.sub(r'\bابي\b(?!\s+بن\b)', 'ابو', s)
    s = re.sub(r'\bزكرياء\b', 'زكريا', s)       # both spellings occur in isnads and headers
    s = re.sub(r'\bابيه\b', '', s)              # "أبيه السائب" -> "السائب"
    # "عبيد الله" is one name too: as two words it pushed "شهاب" out of reach in al-Zuhri's
    # nasab (محمد بن مسلم بن عبيد الله بن عبد الله بن شهاب), so "ابن شهاب" found someone else.
    s = re.sub(r'\bعبيد\s+الله\b', 'عبيد_الله', s)
    s = re.sub(r'\bال(?=\S)', '', s)            # drop the article: البصري ~ بصري
    s = re.sub(r'\bعبد\s+(\S+)', r'عبد_\1', s)  # "عبد الله" / "عبد السلام" is one name, not "عبد" + another
    return re.sub(r'[^ء-ي_ ]', ' ', s)


# Leading relation words / "عن:" in list items: "عمه X", "جده X", "مولاه X".
LEADING = {'عن', 'عمه', 'جده', 'اخيه', 'خاله', 'مولاه', 'مولاته', 'امه', 'جدته', 'عمته', 'خالته', 'ابنه', 'زوجه'}


def tokens(s: str) -> list[str]:
    toks = [w for w in norm(s).split() if w not in STOP and len(w) > 1]
    while toks and toks[0] in LEADING:
        toks.pop(0)
    return toks


def soft_norm(s: str) -> str:
    """Like norm() but keeps the comma, so kunya positions can be read."""
    s = re.sub(r'[ً-ْٰـ]', '', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    s = GLUED_ABU.sub('ابو ', s)
    s = re.sub(r'\bابي\b(?!\s+بن\b)', 'ابو', s)
    s = re.sub(r'\bزكرياء\b', 'زكريا', s)
    s = re.sub(r'\bعبيد\s+الله\b', 'عبيد_الله', s)
    s = re.sub(r'\bال(?=\S)', '', s)
    return re.sub(r'\bعبد\s+([^\s،.:]+)', r'عبد_\1', s)


# The nasab chain in order (ism, father, grandfather, ...): only names linked by "بن",
# so a trailing nisba ("سليمان بن عمرو النخعي") is not mistaken for a grandfather.
# "عبد X" and "عبيد الله" are single names inside the chain.
_NAME = r'(?:عبد\s+[^\s،.]+|عبيد\s+الله(?![^\s،.])|[^\s،.]+)'
NASAB_CHAIN = re.compile(rf'\s*({_NAME}(?:\s+(?:بن|ابن)\s+(?:أبي\s+)?{_NAME})*)')
def nasab_chain(s: str) -> list[str]:
    s = re.sub(r'(?<!\S)بن\s+بن(?!\S)', 'بن', s)      # edition typo: "حماد بن بن سلمة"
    m = NASAB_CHAIN.match(s + ' ')
    return tokens(m.group(1)) if m else []



# --- The same narrator listed by two books ------------------------------------------------------------------
# A name that matches two entries which are one person is no tie, and the isnad should not stop at it. Entries of
# different books are merged when their names agree on three generations (or two, with the same nisba), nothing
# contradicts (a nisba, a kunya or a death date that differs), and a book never lists the person twice: two entries
# of one book are two narrators, and so are two of Tahdhib. A group that is not that clear is left alone.
def _plain(h: str) -> str:
    h = re.sub(r'[ً-ْٰـ]', '', h)
    return re.sub('[أإآ]', 'ا', h).replace('ى', 'ي').replace('ة', 'ه')


def _nisbas(h: str) -> set[str]:
    # «البصري», and with a preposition: «يعرف بالطالقاني», «ولدي الشوسي».
    return {'ال' + m for m in re.findall(r'(?<![ء-ي])(?:ب|و|ل)?ال([ء-ي]{2,}ي)(?![ء-ي])', _plain(h)[:200])} - {'الي', 'الذي', 'التي'}


def _tail(e: dict, n_units: int) -> frozenset[str]:
    """What follows the nasab in the name part of the header (nisbas, laqabs), without the kunya."""
    first = re.split(r'[.\n]', e['header'])[0][:200]
    first = re.sub(r'(?<![ء-ي])ابو\s+[ء-ي]+', ' ', _plain(first))
    return frozenset(tokens(first)[n_units:])


def _generations(h: str) -> list[str]:
    """The fathers named in the first line of a header: «محمد بن هشام بن (أبي حرة) السدوسي» -> هشام، حرة."""
    first = re.split(r'[.\n،]', h)[0][:200].replace('(', ' ').replace(')', ' ')
    return re.findall(r'(?<![ء-ي])ا?بن\s+(?:ابي\s+)?([ء-ي]+)', _plain(first))


def _kunya_words(h: str) -> set[str]:
    return set(re.findall(r'(?<![ء-ي])ابو\s+([ء-ي]+)', _plain(h)[:200]))


def _sources(e: dict) -> set[str]:
    return set(e['source'].split('+')) if e.get('source') else {'tahdhib'}


def merge_same_person(entries: list[dict]) -> tuple[list[dict], list[list[str]]]:
    units = [nasab_chain(re.split(r'[،.\n]', e['header'])[0]) for e in entries]
    nis = [_nisbas(e['header']) for e in entries]
    kun = [_kunya_words(e['header']) for e in entries]
    death = [_plain(e.get('death') or '').strip() for e in entries]
    nis_df = Counter(n for ns in nis for n in ns)
    buckets: dict[tuple, list[int]] = defaultdict(list)
    for i, u in enumerate(units):
        # A fallback entry (Lisan, Siyar) is merged only with another fallback entry, never into a main one: it must not
        # change what a main narrator matches (that made «علي بن عبد العزيز» and «عطاء» ties).
        fb = 'fb' if entries[i].get('fallback') else 'main'
        if len(u) >= 3:
            buckets[(fb, '3', *u[:3])].append(i)
        elif len(u) == 2:
            for n in sorted(nis[i]):                         # a fixed order: the result must not depend on the hash seed
                buckets[(fb, '2', *u, n)].append(i)

    def two_names_agree(a: int, b: int) -> bool:
        """Two names are too common to go on alone: the rest of the name must be the same, or share a rare nisba."""
        return _tail(entries[a], len(units[a])) == _tail(entries[b], len(units[b])) or any(
            nis_df[n] <= 4 for n in nis[a] & nis[b])

    def evidence(a: int, b: int) -> bool:
        """Three names agree; something more says it is one person: a shared nisba or kunya, the very same nasab, or
        a name that adds nothing (no nisba or laqab) and is the beginning of the other."""
        if nis[a] & nis[b] or kun[a] & kun[b] or units[a] == units[b]:
            return True
        short, long_ = (a, b) if len(units[a]) <= len(units[b]) else (b, a)
        return units[long_][:len(units[short])] == units[short] and not _tail(entries[short], len(units[short]))

    def clash(a: int, b: int) -> bool:
        if len(units[a]) >= 4 and len(units[b]) >= 4 and units[a][3] != units[b][3]:
            return True                                   # the great-grandfather differs
        return bool((nis[a] and nis[b] and not nis[a] & nis[b]) or (kun[a] and kun[b] and not kun[a] & kun[b])
                    or (death[a] and death[b] and death[a] != death[b]))

    parent = list(range(len(entries)))
    groups: list[list[int]] = []
    for key, members in buckets.items():
        if len(members) < 2 or any(parent[m] != m for m in members):
            continue
        src = [_sources(entries[m]) for m in members]
        if any(a & b for k, a in enumerate(src) for b in src[k + 1:]):
            continue                                    # a book lists two: two narrators
        if any(clash(a, b) for k, a in enumerate(members) for b in members[k + 1:]):
            continue
        if key[1] == '2' and not all(two_names_agree(members[0], m) for m in members[1:]):
            continue
        if key[1] == '3':
            if any('tahdhib' in x for x in src):
                # Against Tahdhib a later namesake with the same three names is possible: wait for a shared nisba or kunya.
                if not all((nis[members[0]] & nis[m]) or (kun[members[0]] & kun[m]) for m in members[1:]):
                    continue
            elif not all(evidence(members[0], m) for m in members[1:]):
                continue
        root = min(members, key=lambda m: (0 if 'tahdhib' in _sources(entries[m]) else 1,
                                           -(len(entries[m]['shuyukh']) + len(entries[m]['talamidh'])), m))
        for m in members:
            if m != root:
                parent[m] = root
        groups.append([root] + [m for m in members if m != root])

    # A late narrator listed twice, once with his nasab cut short («أحمد بن سلمة» / «أحمد بن سلمة أبو الفضل البزاز»):
    # when the shorter name's units begin exactly two entries of the whole registry and neither is Tahdhib's, they
    # are one narrator unless a nisba, kunya or death date differs. (Two books may share a source here: the late
    # books list one man under several headings.) A header that only points elsewhere or describes the man
    # («هو فلان», «في ترجمة», «شيخ», «والد») is left alone.
    pointer = re.compile(r'\sهو\s|في ترجمة|يأتي في|ينظر|\sشيخ\s|\sوالد\s|\(عن\)')
    by_prefix: dict[tuple, list[int]] = defaultdict(list)
    for i, u in enumerate(units):
        if not entries[i].get('fallback'):
            for k in range(2, len(u) + 1):
                by_prefix[tuple(u[:k])].append(i)
    for i, u in enumerate(units):
        if not 2 <= len(u) <= 2 or entries[i].get('fallback') or parent[i] != i:
            continue
        grp = by_prefix[tuple(u)]
        if len(grp) != 2:
            continue
        j = grp[0] if grp[1] == i else grp[1]
        if parent[j] != j or len(units[j]) < len(u) or (len(units[j]) == len(u) and j < i):
            continue
        if 'tahdhib' in _sources(entries[i]) or 'tahdhib' in _sources(entries[j]) or clash(i, j):
            continue
        if pointer.search(entries[i]['header'][:150]) or pointer.search(entries[j]['header'][:150]):
            continue
        # The generations both headers give must be the same words: «محمد بن هشام بن أبي حرة» is not «محمد بن هشام بن
        # أبي الدميك», even when one header is cut short by a bracket.
        gi, gj = (_generations(entries[m]['header']) for m in (i, j))
        if any(x != y for x, y in zip(gi, gj)):
            continue
        members = [i, j]
        root = min(members, key=lambda m: (-(len(entries[m]['shuyukh']) + len(entries[m]['talamidh'])), m))
        parent[i if root == j else j] = root
        groups.append([root] + [m for m in members if m != root])
        if os.environ.get('SHOW_PAIRS'):
            print('PAIR', ' || '.join(f"{entries[m]['header'][:70]} [{entries[m].get('source') or 'tahdhib'}]" for m in members))

    review =[[f"{entries[m]['header'][:75]} [{'+'.join(sorted(_sources(entries[m])))}]" for m in g] for g in groups]
    for g in groups:
        t = entries[g[0]]
        for j in g[1:]:
            e = entries[j]
            for k in ('shuyukh', 'talamidh'):
                have = {x['name'] for x in t[k]}
                t[k] = t[k] + [x for x in e[k] if x['name'] not in have]
            # The other book's own spelling of the name must still find him (البيهقي: «أبو الحسين بن بشران»).
            t['aliases'] = list(dict.fromkeys(t.get('aliases', []) + [re.split(r'[،\n]', e['header'])[0].strip()]
                                              + e.get('aliases', [])))
            if e.get('kunya_aliases'):
                t['kunya_aliases'] = list(dict.fromkeys(t.get('kunya_aliases', []) + e['kunya_aliases']))
            t['verdict'] = t.get('verdict') or e.get('verdict')
            if t.get('source'):
                t['source'] = '+'.join(dict.fromkeys(t['source'].split('+') + (e.get('source') or '').split('+')))
            t.setdefault('merged_ids', []).append(f"{(e.get('source') or 'extra').split('+')[0]}:{e['num']}")   # as in pipeline.py
            t.setdefault('merged_headers', []).append(e['header'])
    drop = {j for g in groups for j in g[1:]}
    return [e for i, e in enumerate(entries) if i not in drop], review


_before = len(entries)
if not os.environ.get('NO_MERGE'):
    entries, _review = merge_same_person(entries)
    if os.environ.get('SHOW_MERGED'):                   # review: each merged group, one line, 40 at random
        import random
        random.seed(int(os.environ['SHOW_MERGED']))
        for _g in random.sample(_review, min(40, len(_review))):
            print('  ≡ '.join(_g))
print(f'same narrator in two books merged: {_before - len(entries)} entries')

# Index each entry by the tokens of its header (name, kunya, nisbas, laqab).
entry_tokens = [set(tokens(e['header'][:250])) for e in entries]
first_two = [tokens(e['header'][:120])[:2] for e in entries]
# A merged narrator is also known by the words of the headers that were merged into him (the other book's nisba,
# kunya or laqab): «محمد بن عمرو بن خالد الحراني» must still find «… الحراني المصري أبو علاثة».
for _i, _e in enumerate(entries):
    for _h in _e.get('merged_headers', ()):
        entry_tokens[_i] |= set(tokens(_h[:250]))
by_token = defaultdict(set)
for i, toks in enumerate(entry_tokens):
    for w in toks:
        by_token[w].add(i)


def all_tokens_match(toks: list[str]) -> set[int]:
    cands = set(by_token.get(toks[0], ()))
    for w in toks[1:]:
        cands &= by_token.get(w, set())
    return cands


# The narrator's own ism is the first token of the header; his kunyas are "أبو X" in it.
# Matching on any header token lets relatives match ("أخو محمد بن سيرين"), so the
# item must start with the entry's ism, or with one of its kunyas followed by the ism.
ism = [tokens(e['header'][:120])[:1] for e in entries]
isms = {i[0] for i in ism if i}
# Own kunya only: at the start, right after the ism ("ذكوان أبو صالح السمان") or after "،"/"ويقال:",
# never "والد أبي X" / "أخو أبي X" / "ابن أبي X".
KUNYA = re.compile(r'(?:^(?!(?:ابن|ابنه|بن)\s)(?:[^\s،.:]+\s+)?|،\s*|يقال\s*:?\s*|وهو\s+)ابو ([^\s،.:]+)')
# A kunya the narrator is known by: "عبد الله بن ذكوان ... المعروف بأبي الزناد".
KNOWN_AS = re.compile(r'(?:معروف|يعرف)\s+ب(?:ابو|ابي)\s+([^\s،.:]+)')
kunyas = [set(KUNYA.findall(soft_norm(e['header'][:200]).strip()))
          | set(KNOWN_AS.findall(soft_norm(e['header'][:200]))) for e in entries]
for _i, _e in enumerate(entries):
    for _h in _e.get('merged_headers', ()):
        kunyas[_i] |= set(KUNYA.findall(soft_norm(_h[:200]).strip())) | set(KNOWN_AS.findall(soft_norm(_h[:200])))
kunya_index = defaultdict(set)
for _j, _ks in enumerate(kunyas):
    for _k in _ks:
        kunya_index[_k].add(_j)


def starts_like(toks: list[str], j: int) -> bool:
    if not ism[j]:
        return False
    # Kunya-only entries ("أبو زيد. عن: أبي هريرة") have "ابو" as ism: their own kunya must
    # match, not just words anywhere in the header.
    if toks[0] == ism[j][0] and toks[0] != 'ابو':
        return True
    return toks[0] == 'ابو' and len(toks) > 1 and toks[1] in kunyas[j]


@lru_cache(maxsize=None)
def name_candidates(name: str) -> frozenset[int]:
    """Entries whose own name (ism/kunya + nasab) matches `name`."""
    toks = tokens(name)
    # "النبي ﷺ" is not a narrator entry; "رجل" / "امرأة" / "شيخ" are unnamed narrators, not the
    # entries "رجل من آل سهل بن حنيف" or "امرأة من عبد القيس".
    if not toks or toks[0] in ('نبي', 'رجل', 'امراه', 'شيخ', 'غلام', 'بعض'):   # "عن بعض أصحاب النبي"
        return frozenset()
    # The "X بن Y بن Z" part of the name, after a leading kunya if any ("أبو بكر محمد بن أحمد").
    chain = nasab_chain(re.sub(r'^\s*أب[وي]\s+(?:عبد\s+)?\S+\s+(?!بن\s)', '', name))

    def chain_fits(j: int) -> bool:
        return len(chain) < 2 or fits(j, chain)

    exact = all_tokens_match(toks)
    if exact:
        anchored = {j for j in exact if starts_like(toks, j) and chain_fits(j)}
        if toks[0] == 'ابو' and len(toks) > 2 and re.match(r'\s*أب[وي]\s+\S+\s+بن\s', name):
            # "أبو سلمة بن عبد الرحمن": the word after "بن" is the father (or the kunya-headed
            # entry's own "أبو سلمة بن عبد الرحمن بن عوف"), not the ism of عبد الرحمن بن حماد أبو سلمة.
            anchored = {j for j in anchored
                        if nasab[j][1:2] == toks[2:3] or own_seq[j][:3] == toks[:3]} or anchored
        elif toks[0] == 'ابو' and len(toks) > 2:    # "أبي أمامة أسعد ..." -> the ism after the kunya decides
            anchored = {j for j in anchored if toks[2] == ism[j][0]} or anchored
        if anchored or len(toks) < 2:
            return frozenset(anchored)      # unanchored matches are usually relatives: drop them
        return by_grandfather(toks)
    # Kunya-led forms common in later isnads:
    #   "أبو بكر بن إسحاق"         -> kunya بكر, father إسحاق
    #   "أبو زكريا العنبري"         -> kunya زكريا + a nisba of the narrator's own name
    #   "أبو بكر محمد بن أحمد بن بالويه" -> kunya + ism + nasab (all tokens present)
    if toks[0] == 'ابو' and len(toks) >= 3:
        k, rest = toks[1], toks[2:]
        pool = kunya_index.get(k, set())
        if re.match(r'\s*أب[وي] \S+ بن ', name):
            # "أبو بكر بن إسحاق" names the father; "أبو بكر بن أبي شيبة" names an ancestor
            # (عبد الله بن محمد بن أبي شيبة), and "أبي شيبة" is two tokens.
            anc = rest[:2] if rest[0] == 'ابو' else rest[:1]
            return frozenset(j for j in pool
                             if any(nasab[j][p:p + len(anc)] == anc for p in range(1, 5)))
        return frozenset(j for j in pool if set(rest) <= (own_tokens[j] | entry_tokens[j]) and chain_fits(j))
    # Fallback: ism + father must match the entry's own ism + father, and a grandfather named in
    # the item ("X بن Y بن Z") must not contradict the entry's grandfather.
    if len(toks) >= 2:
        chain = nasab_chain(name)
        base = {i for i in all_tokens_match(toks[:2])
                if (first_two[i] == toks[:2] or (alt_nasab[i] or [])[:2] == toks[:2]) and fits(i, chain)}
        if base:
            best = max(len(set(toks) & entry_tokens[i]) for i in base)
            return frozenset(i for i in base if len(set(toks) & entry_tokens[i]) == best)
        return by_grandfather(toks)
    return frozenset()


def by_grandfather(toks: list[str]) -> frozenset[int]:
    """Named by grandfather or by the family's "ابن X", nobody having that father:
    "عبد الله بن أحمد بن حنبل" (… بن محمد بن حنبل), "عثمان بن أبي شيبة", "محمد بن أبي عدي",
    "علي بن المديني", "إسحاق بن راهويه", "إسماعيل ابن علية". The words after the ism must come
    in order in the narrator's own name part."""
    if toks[0] == 'ابو':
        return frozenset()

    def near(i: int) -> bool:
        # A name from the nasab must be the grandfather: "أحمد بن أسد" is not
        # أحمد بن محمد بن حنبل بن هلال بن أسد. Words outside the nasab (المديني، ابن أبي شيبة) are fine.
        p = nasab[i].index(toks[1]) if toks[1] in nasab[i] else 0
        return p <= 2

    def no_kunya(i: int) -> list[str]:
        # "مالك بن يحيى" is not مالك بن دينار، أبو يحيى: the word of his own kunya is not an ancestor
        # (an ancestor's kunya in the nasab stays: "عثمان بن أبي شيبة").
        seq = own_seq[i]
        return [w for k, w in enumerate(seq)
                if not (k and seq[k - 1] == 'ابو' and w in kunyas[i] and w not in nasab[i])]

    return frozenset(i for i in ism_index.get(toks[0], ()) if in_order(toks[1:], no_kunya(i)[1:])
                     and near(i) and not re.match(r'\s*(?:أم|أبو)\s', entries[i]['header']))  # "أم عثمان"


def in_order(words: list[str], seq: list[str]) -> bool:
    it = iter(seq)
    return all(w in it for w in words)


# ── Shuhra (أسماء الشهرة) ──────────────────────────────────────────────
# 1. Aliases from Taqrib redirects: "سليمان الأعمش هو ابن مهران" -> سليمان بن مهران.
# 2. A bare laqab/nisba or "ابن X" ("الأعمش", "الزهري", "ابن جريج") matches the entry's own
#    name part (not its relatives); context (teacher lists) and fame then pick one.
alias_index: dict[tuple, set[int]] = defaultdict(set)
RELATION = re.compile(r'\s(?:أخو|أخي|والد|والدة|ابن عم|ابن أخي|ابن أخت|عم|خال|زوج|جد|صهر|ختن)\s')
own_tokens = [set(tokens(RELATION.split(re.split(r'[.\n]', e['header'])[0] + ' ')[0])) for e in entries]
# The same words in order (ism, nasab, kunya, nisbas, "ابن X"), for names given by grandfather.
own_seq = [tokens(RELATION.split(re.split(r'[.\n]', e['header'])[0] + ' ')[0]) for e in entries]
ism_index = defaultdict(set)
for _j, _i in enumerate(ism):
    if _i:
        ism_index[_i[0]].add(_j)


def laqabs_of(header: str) -> set[str]:
    """Laqabs written without ال: the last word of the kunya phrase ("أبو بكر البصري بندار"),
    or the word after "الملقب" / "يلقب". Other header words ("خرج", "للنبي") are not names."""
    first = re.split(r'[.\n]', header)[0]
    out = set()
    for chunk in re.split(r'،', first):
        if re.match(r'\s*أبو\s', chunk):
            out |= set(tokens(chunk)[-1:])
    for m in re.finditer(r'(?:الملقب|يلقب|لقبه)\s*:?\s*([^\s،.]+)', first):
        out |= set(tokens(m.group(1)))
    # تاريخ بغداد: "أبو بكر البزاز، المعروف بالشافعي" -> الشافعي ("بابن X" is a nasab form, not a laqab).
    for m in re.finditer(r'(?:المعروف|ويعرف|يعرف)\s+ب(?!ابن\s|أبي\s|ابن$)([^\s،.]+)', first):
        out |= set(tokens(m.group(1)))
    return out


own_laqabs = [laqabs_of(e['header'][:300]) for e in entries]
for _i, _e in enumerate(entries):
    for _h in _e.get('merged_headers', ()):
        own_laqabs[_i] |= laqabs_of(_h[:300])
        own_tokens[_i] |= set(tokens(RELATION.split(re.split(r'[.\n]', _h)[0] + ' ')[0]))
for _j, _l in enumerate(own_laqabs):
    own_tokens[_j] |= _l
fame = [len(e['talamidh']) for e in entries]
nasab = [nasab_chain(re.split(r'[،.\n]', e['header'])[0]) for e in entries]
# Shamela's Tahdhib has typos in names ("عبيد الله بن عتبة" for عبيد الله بن عبد الله بن عتبة):
# the aligned Taqrib entry's nasab is accepted as a second nasab (align.json, Tahdhib entries
# come first in `entries`, in the same order).
alt_nasab: list[list[str] | None] = [None] * len(entries)
_align = os.path.join(os.path.dirname(os.path.abspath(sys.argv[1])), 'align.json')
if os.path.exists(_align):
    _tq = json.load(open(_align.replace('align.json', 'taqrib.json'), encoding='utf-8'))['entries']
    for _p in json.load(open(_align, encoding='utf-8')):
        _i, _n = _p['tahdhib'], nasab_chain(_tq[_p['taqrib']]['name'])
        if _p['score'] < 1.0 or not _n or nasab[_i][:1] != _n[:1]:
            continue
        # Taqrib's short name carries the laqab and nisbas that Tahdhib may give only in a later
        # sentence ("... وقيل: ... أبو بكر الحميدي المكي"): they count as the narrator's own words.
        own_tokens[_i] |= set(tokens(_tq[_p['taqrib']]['name']))
        if _n != nasab[_i]:
            alt_nasab[_i] = _n
            entry_tokens[_i] |= set(_n)
            for _w in _n:
                by_token[_w].add(_i)


def fits(j: int, chain: list[str]) -> bool:
    """"محمد بن علي" must not match "محمد بن عمر بن علي": the nasab must agree in order."""
    return any(n[:len(chain)] == chain[:len(n)] for n in (nasab[j], alt_nasab[j]) if n)


laqab_index = defaultdict(set)
for j, toks in enumerate(own_tokens):
    for w in toks:
        laqab_index[w].add(j)

# --- Fallback entries (Lisan al-Mizan, the Siyar): weak or famous narrators of other books ----------------------------
# A name is looked up among the other entries first, exactly as if the fallback ones did not exist; only when nothing
# matches is it looked up among the fallback ones. Searching them together made their namesakes tie «أنس بن مالك» and
# «بندار» (the fallback entries named «بندار» ended the search before the laqab step reached محمد بن بشار), and made
# the Taqrib and cross-reference aliases (which need a single hit) fail. So the name indexes exist twice: from here on
# the globals are the main ones, and `candidates` (below, after everything is built) swaps in the fallback ones for the
# second look. `isms` is one of them: the fallback «بندار بن عمر…» must not stop «بندار» being a laqab nobody is named.
_FALLBACK = {_j for _j, _e in enumerate(entries) if _e.get('fallback')}
_main_indexes, _fallback_indexes = {}, {}


def _split_indexes(names):
    for name in names:
        main, fb = defaultdict(set), defaultdict(set)
        for key, found in globals()[name].items():
            if found - _FALLBACK:
                main[key] = found - _FALLBACK
            if found & _FALLBACK:
                fb[key] = found & _FALLBACK
        _main_indexes[name], _fallback_indexes[name] = main, fb
        globals()[name] = main


if _FALLBACK:
    _main_indexes['isms'] = {_i[0] for _j, _i in enumerate(ism) if _i and _j not in _FALLBACK}
    _fallback_indexes['isms'] = set(isms)
    isms = _main_indexes['isms']
    _split_indexes(('by_token', 'ism_index', 'kunya_index', 'laqab_index'))

_dir = os.path.dirname(os.path.abspath(sys.argv[1]))
_taqrib = os.path.join(_dir, 'taqrib.json')
taqrib_alias_hits = 0
if os.path.exists(_taqrib):
    NOTE = re.compile(r'\s(?:بضم|بفتح|بكسر|بالتصغير|مصغر|عن|شيخ|روى|يروي|تقدم|يأتي|في الكنى)\b.*')
    for x in json.load(open(_taqrib, encoding='utf-8'))['xrefs']:
        alias = NOTE.sub('', x['alias']).strip()
        target = NOTE.sub('', x['target']).strip()
        a_toks, t_raw = tokens(alias), norm(target).split()
        if not a_toks or not t_raw:
            continue
        # "X الأعمش هو ابن مهران" completes X's nasab (X may be a compound "عبد الله");
        # otherwise the target is a full name.
        alias_ism = re.match(r'\s*(عبد\s+\S+|\S+)', alias).group(1)
        full = f'{alias_ism} {target}' if t_raw[0] in ('ابن', 'بن') else target
        hit = name_candidates(full)
        if len(hit) == 1:
            alias_index[tuple(a_toks)] |= hit
            taqrib_alias_hits += 1


# Exact name forms recorded by shaykh books ("وورد: أبو القاسم الفقيه"): tokens -> entries.
# Bare kunyas ("أبو خليفة" = الفضل بن الحباب in ري الظمآن; "وورد: أبو إسحاق" for al-Bayhaqi's
# shaykh يحيى بن إبراهيم) are shared by many narrators ("أبو إسحاق" from Shu'ba is al-Sabi'i),
# so they are loose: they only add a candidate and the chain context decides.
exact_aliases: dict[tuple, set[int]] = defaultdict(set)
loose_aliases: dict[tuple, set[int]] = defaultdict(set)
for _j, _e in enumerate(entries):
    for _a in _e.get('aliases', []) + _e.get('kunya_aliases', []):
        _t = tuple(tokens(_a))
        # Two words ("عبد الله بن يوسف" for al-Bayhaqi's shaykh ابن بامويه) are shared by older
        # narrators (al-Tinnisi), so only 3+ words are exact.
        if len(_t) >= 3:
            exact_aliases[_t].add(_j)
        elif _t:
            loose_aliases[_t].add(_j)


if _FALLBACK:
    _split_indexes(('alias_index', 'exact_aliases', 'loose_aliases'))


@lru_cache(maxsize=None)
def candidates(name: str) -> frozenset[int]:
    return _candidates(name) | frozenset(loose_aliases.get(tuple(tokens(name)), ()))


def _candidates(name: str) -> frozenset[int]:
    toks = tokens(name)
    if not toks or toks[0] == 'نبي':
        return frozenset()
    if tuple(toks) in exact_aliases:        # the compiler's own spelling of his shaykh (إتحاف المرتقي)
        return frozenset(exact_aliases[tuple(toks)])
    raw = soft_norm(name).strip()
    if raw.startswith('ابن ') and len(toks) <= 3:
        # "ابن جريج", "ابن أبي ذئب": the words must be a father/ancestor in the narrator's own nasab
        # (checked first: dropping "ابن" would otherwise turn "ابن وهب" into a narrator named وهب).
        n = len(toks)
        found = frozenset(j for j in laqab_index.get(toks[0], ())
                          if any(nasab[j][p:p + n] == toks for p in range(1, 5)))
        # "ابن عمر" is Abdullah b. Umar, not his sons Salim and Ubaydullah (… بن عبد الله بن عمر): when
        # a son of X is at least as cited as the grandsons, only the sons count. "ابن جريج" is still
        # the grandson عبد الملك بن عبد العزيز بن جريج, far more cited than the son عبد العزيز.
        sons = frozenset(j for j in found if nasab[j][1:1 + n] == toks)
        if sons and max(fame[j] for j in sons) >= max(fame[j] for j in found):
            found = sons
        # A Taqrib alias joins the candidates: «ابن علية» is إسماعيل بن إبراهيم (علية is his mother), while
        # the nasab match alone found only his son حماد بن إسماعيل بن علية.
        return found | frozenset(alias_index.get(tuple(toks), ()))
    found = name_candidates(name)
    if found:
        return found
    if tuple(toks) in alias_index:          # aliases only fill in when the name itself matches nobody
        return frozenset(alias_index[tuple(toks)])
    if len(toks) == 1 and re.match(r'ال\S', name.strip()):
        return frozenset(laqab_index.get(toks[0], ()))   # bare laqab / nisba: "الأعمش", "الزهري"
    if len(toks) == 1 and toks[0] not in isms:
        # A laqab without ال that is nobody's ism ("بندار" = محمد بن بشار), see laqabs_of; not a
        # father or grandfather of that name ("علي بن محمد بن بندار").
        return frozenset(j for j in laqab_index.get(toks[0], ())
                         if toks[0] in own_laqabs[j] and toks[0] not in nasab[j])
    return frozenset()


def fame_pick(cands: set[int]) -> int | None:
    """The clearly most-cited narrator among `cands` (3x the students of the runner-up), if any."""
    ranked = sorted(cands, key=lambda j: -fame[j])
    if len(ranked) == 1:
        return ranked[0]
    if fame[ranked[0]] >= 3 * max(1, fame[ranked[1]]):
        return ranked[0]
    return None


# Cross-references add alias names: "أحمد بن بكار الدمشقي، هو: أحمد بن عبد الرحمن بن بكار".
alias_hits = 0
for x in xrefs:
    if not x.get('target'):
        continue
    target = candidates(x['target'])
    if len(target) == 1:
        (j,) = target
        alias = re.split(r'[،:]', x['header'])[0]
        for w in tokens(alias):
            by_token[w].add(j)
        entry_tokens[j] |= set(tokens(alias))
        alias_hits += 1
candidates.cache_clear()
name_candidates.cache_clear()

# The two-look `candidates` (see the note above `_FALLBACK`).
if _FALLBACK:
    _plain_candidates, _plain_name_candidates = candidates.__wrapped__, name_candidates.__wrapped__
    _cached_name_candidates = name_candidates

    @lru_cache(maxsize=None)
    def candidates(name: str) -> frozenset[int]:
        found = _plain_candidates(name)
        if found:
            return found
        _saved = {_n: globals()[_n] for _n in _fallback_indexes}
        globals().update(_fallback_indexes)
        globals()['name_candidates'] = _plain_name_candidates       # its cache holds main-index answers
        try:
            return _plain_candidates(name)
        finally:
            globals().update(_saved)
            globals()['name_candidates'] = _cached_name_candidates

print(f'entries: {len(entries)}  crossref aliases attached: {alias_hits}/{len(xrefs)}  Taqrib aliases: {taqrib_alias_hits}')


@lru_cache(maxsize=None)
def lists_resolve_to(j: int, key: str, other: int) -> bool:
    return any(other in candidates(it['name']) for it in entries[j][key])


# Book symbols: an item "(خ م)" in A's list means A narrates from B in those books,
# so B's own entry symbols must cover them. "ع" = the six books, "٤" = the four Sunan.
SIX = {'خ', 'م', 'د', 'ت', 'س', 'ق'}


def expand(sym: str) -> set[str]:
    out = set()
    for s in re.sub(r'[()]', ' ', sym).split():
        if s == 'ع':
            out |= SIX
        elif s == '٤':
            out |= {'د', 'ت', 'س', 'ق'}
        elif s == '٣':
            out |= {'د', 'ت', 'س'}
        else:
            out.add(s)
    return out


entry_syms = [expand(e['symbols']) for e in entries]


def symbol_filter(c: set[int], item_sym: str) -> set[int]:
    need = expand(item_sym)
    if not need:
        return c
    kept = {j for j in c if need <= entry_syms[j]}
    return kept or c


stats = {k: Counter() for k in ('shuyukh', 'talamidh')}
unresolved = Counter()
for i, e in enumerate(entries):
    for key, back in (('shuyukh', 'talamidh'), ('talamidh', 'shuyukh')):
        for it in e[key]:
            c = candidates(it['name']) - {i}
            if not c:
                stats[key]['unresolved'] += 1
                unresolved[key, it['symbols'] != ''] += 1
                continue
            c = symbol_filter(c, it['symbols'])
            confirmed = [j for j in c if lists_resolve_to(j, back, i)] if len(c) <= 60 else []
            if len(confirmed) == 1:
                stats[key]['confirmed (reciprocal)'] += 1
            elif len(c) == 1:
                stats[key]['unique, not reciprocal'] += 1
            elif len(confirmed) > 1:
                stats[key]['ambiguous after reciprocity'] += 1
            else:
                stats[key]['ambiguous'] += 1

for key, c in stats.items():
    total = sum(c.values())
    print(f'== {key}: {total} names')
    for k, v in c.most_common():
        print(f'   {k:28} {v:6}  {v / total:.1%}')
# A name carrying book symbols, e.g. "(خ م)", is a six-books narrator and should have an entry.
for key in ('shuyukh', 'talamidh'):
    with_sym = sum(1 for e in entries for it in e[key] if it['symbols'])
    print(f'{key}: unresolved among names WITH symbols: {unresolved[key, True]}/{with_sym} '
          f'({unresolved[key, True] / with_sym:.1%}); without symbols: {unresolved[key, False]}')
