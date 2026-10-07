"""Resolve a whole isnad jointly instead of link by link.

Link-by-link resolution loses all context once one narrator is unknown ("عمرو بن علي ←
أبو عاصم ← ابن جريج" all fail when the first name is ambiguous). Here every name gets its
candidate set, and a Viterbi pass picks the assignment that maximises teacher/student
consistency along the chain:

    edge(student, shaykh) = 2 if each lists the other, 1 if only one does, 0 otherwise

A choice is accepted only when it beats every alternative at its position given its
chosen neighbours (strictly higher local score), so coverage does not come at the cost of
guessing; on a tie, only a clearly more cited candidate (fame_pick) wins. When no candidate has any edge and the name is given in full (kunya + ism +
father), fame_pick (3x the students) may still decide. Loaded with exec() by
compare_current.py / gap_test-style scripts after gap_test.py's helpers (chain_segments,
is_name, lookup, shuyukh_of, compiler).
"""
import math
import re

MAX_CANDIDATES = 80
# After each resolve(): position -> the candidates tied for an undecided name (the best linked to
# their neighbours, or the most cited when none is linked), so the Ilal data can check whether a
# tie hides a mudallis or mukhtalit.
last_ties: dict[int, list[int]] = {}
TIES_KEPT = 10
KUNYA_FIRST = re.compile(r'\s*(?:أبو|أبي|أبا)\s')
FULL_KUNYA_NAME = re.compile(r'\s*(?:أبو|أبي|أبا)\s+(?:عبد\s+)?\S+\s+(?:عبد\s+)?\S+\s+بن\s+\S+')

# Students as resolved from each entry's talamidh list (the mirror of shuyukh_of).
def _talamidh_of() -> list[set[int]]:
    out = []
    for i, e in enumerate(entries):
        s = set()
        for it in e['talamidh']:
            c = set(symbol_filter(set(candidates(it['name'])) - {i}, it['symbols']))
            if it.get('note') != 'short' or len(c) == 1:  # short names: only if unique
                s |= c
        out.append(s)
    return out


talamidh_of = cached('talamidh_of', _talamidh_of)


# Bare names the current system's ContextualDisambiguator maps without a condition, kept only where our
# own chain resolution agrees with it 97%+ (30+ cases) on the 31-book bench: used when the chain cannot
# decide. Rules our evidence contradicts («سفيان» 28%, «جابر» 26%, «يحيى بن سعيد» 35%, «عكرمة»,
# «محمد بن كثير», «ابن علية») are left out. Values are header prefixes, matched to one entry at load.
DEFAULT_FOR = {
    'ابن عباس': 'عبد الله بن عباس بن عبد المطلب', 'عبد الله بن عباس': 'عبد الله بن عباس بن عبد المطلب',
    'عائشة': 'عائشة بنت أبي بكر الصديق أم المؤمنين', 'ابن عمر': 'عبد الله بن عمر بن الخطاب القرشي',
    'الزهري': 'محمد بن مسلم بن عبيد الله بن عبد الله بن شهاب', 'ابن شهاب': 'محمد بن مسلم بن عبيد الله بن عبد الله بن شهاب',
    'جابر بن عبد الله': 'جابر بن عبد الله بن عمرو بن حرام', 'أنس': 'أنس بن مالك بن النضر',
    'قتادة': 'قتادة بن دعامة', 'الأعمش': 'سليمان بن مهران الأسدي', 'مجاهد': 'مجاهد بن جبر',
    'شعبة': 'شعبة بن الحجاج بن الورد', 'أبو العباس محمد بن يعقوب': 'محمد بن يعقوب بن يوسف بن معقل',
    'عروة': 'عروة بن الزبير بن العوام', 'نافع': 'نافع ، مولى عبد الله بن عمر', 'الحكم': 'الحكم بن عتيبة',
    'معمر': 'معمر بن راشد', 'ابن جريج': 'عبد الملك بن عبد العزيز بن جريج', 'مالك': 'مالك بن أنس بن مالك بن أبي عامر',
    'أبي سلمة': 'أبو سلمة بن عبد الرحمن بن عوف', 'وكيع': 'وكيع بن الجراح بن مليح الرؤاسي', 'سالم بن عبد الله': 'سالم بن عبد الله بن عمر بن الخطاب',
    'ابن سيرين': 'محمد بن سيرين الأنصاري', 'أبو الزبير': 'محمد بن مسلم بن تدرس',
    'أبو بكر بن أبي شيبة': 'عبد الله بن محمد بن إبراهيم بن عثمان بن خواستي', 'إسماعيل ابن علية': 'إسماعيل بن إبراهيم بن مقسم',
    'ابن عيينة': 'سفيان بن عيينة بن أبي عمران', 'شقيق': 'شقيق بن سلمة',
}
default_of = {}
for _name, _prefix in DEFAULT_FOR.items():
    # A fallback entry (Lisan, Siyar) never counts: «جابر بن عبد الله بن عمرو بن حرام» also heads the Siyar's entry for him.
    _hit = [j for j, e in enumerate(entries) if e['header'].startswith(_prefix) and not e.get('fallback')]
    if len(_hit) == 1:
        default_of[_name] = _hit[0]


def edge(student: int | None, shaykh: int | None) -> int:
    if student is None or shaykh is None:
        return 0
    return (shaykh in shuyukh_of[student]) + (student in talamidh_of[shaykh])


def named_pick(tied: list[int], seg: str, student: int | None, shaykh: int | None) -> int | None:
    """The one tied candidate a neighbour names with the isnad's own form, where that neighbour lists
    every tied candidate: al-A'mash's students include «أبو معاوية الضرير» and «هشيم بن بشير», so
    «أبو معاوية» from al-A'mash is al-Darir, though Hushaym has the same kunya and is as well linked.
    The neighbour must list them all: when it lists only one, the form says nothing about the others
    («أبي بشر» went to بيان بن بشر, «أبي سلام» to the Kufan al-Aswad; 9 of 80 such picks were wrong).
    Both neighbours must agree when both decide. Only for a kunya, a bare ism or a nisba: with a nasab
    («ابن عون», «يحيى بن سعيد») the lists give every candidate those words, and a difference is spelling."""
    want = set(tokens(seg))
    if not want or re.search(r'(?:^|\s)(?:بن|ابن|بنت)(?:\s|$)', seg):
        return None
    picks = set()
    for nb, key in ((student, 'shuyukh'), (shaykh, 'talamidh')):
        if nb is None:
            continue
        items = {x: [it for it in entries[nb][key] if x in candidates(it['name'])] for x in tied}
        if not all(items.values()):
            continue
        named = [x for x in tied if any(want <= set(tokens(it['name'])) for it in items[x])]
        if len(named) == 1:
            picks.add(named[0])
    return picks.pop() if len(picks) == 1 else None


def prior(j: int) -> float:
    return 0.01 * math.log1p(fame[j])        # tie-breaker only; never outweighs one edge


def father_of(p: int, unlisted: bool = True) -> set[int]:
    """Entries that can be p's father ("عن أبيه"): p's shaykh whose ism is p's father's name."""
    father = nasab[p][1:2]
    if father == ['ابو']:
        # "سهيل بن أبي صالح، عن أبيه": the father is the shaykh with that kunya (ذكوان أبو صالح),
        # not a kunya-only entry whose ism is "ابو".
        k = nasab[p][2:3]
        return {j for j in shuyukh_of[p] if k and k[0] in kunyas[j]}
    if not father:
        return set()
    listed = {j for j in shuyukh_of[p] if ism[j] == father}
    # The father's own nasab continues the son's ("سهل بن معاذ بن أنس" -> معاذ بن أنس الجهني): it breaks a tie
    # between listed shaykhs (عامر بن سعد among several «سعد بن …»), and when none is listed (the son's
    # list says only "أبيه", or he has none: عمرو بن شعيب) the one entry that continues it is taken.
    # Father and grandfather must both agree. The Taqrib nasab wins over a typo'd Tahdhib one («عبيد الله بن
    # عتبة» for عبيد الله بن عبد الله بن عتبة gave عتبة بن مسعود), and a kunya-headed son («أبو بردة بن أبي موسى»)
    # has no nasab to continue.
    tail = (alt_nasab[p] or nasab[p])[1:] if nasab[p][:1] != ['ابو'] else []

    def continues(f: int) -> bool:
        n = min(len(tail), len(nasab[f]))
        return n >= 2 and nasab[f][:n] == tail[:n]

    if len(listed) > 1:
        return {j for j in listed if continues(j)} or listed
    if not listed and unlisted and len(tail) >= 2:
        found = {j for j in ism_index.get(father[0], ()) if j != p and continues(j)}
        return found if len(found) == 1 else set()
    return listed


# «أبيه» / «أبي» (the speaker's own father: «حدثني أبي»), «والده» / «والدي»; «جده» / «جدي».
_HONORIFIC_AFTER = r'(?:\s+(?:رحمه الله|رحمهما الله|رضي الله عنه|رضي الله عنهما|رحمة الله عليه))?'
KIN_FATHER = re.compile(r'(?:أبيه|ابيه|أبي|أبى|ابي|والده|والدي|أبوه)' + _HONORIFIC_AFTER)
KIN_GRAND = re.compile(r'(?:جده|جدي|جدّه)' + _HONORIFIC_AFTER)


def kin_form(seg: str) -> str | None:
    """'أبيه' for a bare reference to the previous narrator's father, 'جده' for his grandfather, else None."""
    s = HARAKAT_RE.sub('', seg).strip(' ،:.')
    if KIN_FATHER.fullmatch(s):
        return 'أبيه'
    if KIN_GRAND.fullmatch(s):
        return 'جده'
    return None


def grandfather_of(p: int, unlisted: bool = True) -> set[int]:
    """Entries that can be p's grandfather ("عن جده" right after p): the father of p's father."""
    return set().union(*(father_of(f, unlisted) for f in father_of(p, unlisted))) if father_of(p, unlisted) else set()


def resolve(segs: list[str], start: int | None) -> list[tuple[str, int | None, str]]:
    # Candidate sets; "أبيه" depends on the previous position's candidates.
    cands: list[list[int]] = []
    fathers: list[dict | None] = []                  # for "أبيه": son -> his possible fathers
    for i, seg in enumerate(segs):
        fathers.append(None)
        if seg.strip() in ('أبيه', 'ابيه'):
            prev_c = cands[-1] if cands else ([start] if start is not None else [])
            # An unlisted father only when the son is certain (see father_of).
            fathers[i] = {p: father_of(p, len(prev_c) == 1) for p in prev_c}
            c = set().union(*fathers[i].values()) if prev_c else set()
        elif seg.strip() == 'جده':
            prev_c = cands[-1] if cands else ([start] if start is not None else [])
            # «عمرو بن شعيب، عن أبيه، عن جده»: right after «أبيه» the grandfather is the father of that father;
            # otherwise it is the father of the previous narrator's father.
            after_father = i > 0 and segs[i - 1].strip() in ('أبيه', 'ابيه')
            # «عمرو بن شعيب، عن أبيه، عن جده»: the books disagree whether «جده» is Muhammad (Shu'ayb's father) or
            # the Companion Abdullah b. Amr, so after «أبيه» the grandfather's father is a candidate too and the
            # chain context (or nothing, when it is a tie) decides.
            step = (lambda p, u: father_of(p, u) | grandfather_of(p, u)) if after_father else grandfather_of
            fathers[i] = {p: step(p, len(prev_c) == 1) for p in prev_c}
            if after_father:
                # «عمرو بن شعيب، عن أبيه، عن جده»: Tahdhib al-Kamal takes «جده» to be the Companion عبد الله بن عمرو
                # (who is not among Muhammad's listed shaykhs), not Muhammad b. Abdullah, Shu'ayb's own father.
                companion = set(candidates('عبد الله بن عمرو بن العاص'))
                for p in prev_c:
                    if nasab[p][:3] == ['شعيب', 'محمد', 'عبد_له'] and companion:
                        fathers[i][p] = set(companion)
            c = set().union(*fathers[i].values()) if prev_c else set()
        else:
            c = set(lookup(seg, None))
        c.discard(start)
        ranked = sorted(c, key=lambda j: -fame[j])[:MAX_CANDIDATES]
        cands.append(ranked)

    # Viterbi over states = candidates + None ("unknown here"; breaks the chain's edges).
    layers = [[(start, 0.0, -1)]]                   # (entry, score, back-pointer)
    for i, c in enumerate(cands):
        layer = []
        prev_layer = layers[-1]
        for j in c + [None]:
            best, back = -1e9, -1
            for k, (p, sc, _) in enumerate(prev_layer):
                # "أبيه" is the father of the son chosen before it, not of another candidate for that name
                # (it had paired «أبو بردة» with the father of «بريد بن عبد الله بن أبي بردة»).
                if fathers[i] is not None and j is not None and j not in fathers[i].get(p, ()):
                    continue
                s = sc + edge(p, j)
                if s > best:
                    best, back = s, k
            layer.append((j, best + (prior(j) if j is not None else 0.0), back))
        layers.append(layer)

    # Back-track.
    path, k = [], max(range(len(layers[-1])), key=lambda x: layers[-1][x][1])
    for layer in reversed(layers[1:]):
        j, _, back = layer[k]
        path.append(j)
        k = back
    path.reverse()
    # A father or grandfather equal to a neighbour on the chain means the son was resolved wrongly
    # («حدثنا جدي، حدثنا إبراهيم بن المنذر» gave the grandfather «إبراهيم بن المنذر»): leave it undecided.
    # Likewise the narrator after «أبيه» is not the son again (Bakkar ← his father ← «أبي بكرة» chose Bakkar).
    for i, f in enumerate(fathers):
        if f is None:
            continue
        before = path[i - 1] if i else start
        after = path[i + 1] if i + 1 < len(path) else None
        if path[i] is not None and path[i] in (before, after):
            path[i] = None
        if after is not None and after == before:
            path[i + 1] = None

    out = []
    last_ties.clear()
    for i, (seg, j) in enumerate(zip(segs, path)):
        c = cands[i]
        if not c:
            out.append((seg, None, 'missing'))
            continue
        left = path[i - 1] if i > 0 else start
        right = path[i + 1] if i + 1 < len(path) else None

        def local(x: int) -> int:
            return edge(left, x) + edge(x, right)

        if fathers[i] is not None and seg.strip() == 'جده' and j is not None and any(
                local(x) == local(j) for x in fathers[i].get(left, ()) if x != j):
            # «عن أبيه، عن جده» where the father's father and his own father are equally linked
            # (Muhammad and Abdullah b. Amr for عمرو بن شعيب): the books differ, so it stays undecided.
            out.append((seg, None, 'ambiguous'))
            last_ties[i] = sorted(fathers[i][left], key=lambda x: -fame[x])[:TIES_KEPT]
        elif fathers[i] is not None:
            # "أبيه" follows the son chosen on the path: a single father in the union of all the son's
            # candidates may belong to another of them.
            out.append((seg, j, 'father' if seg.strip() != 'جده' else 'grandfather') if j is not None
                       else (seg, None, 'ambiguous'))
        elif len(c) == 1:
            out.append((seg, c[0], 'unique'))
        elif j is not None and local(j) > 0 and all(local(x) < local(j) for x in c if x != j):
            out.append((seg, j, 'chain'))
        elif (j is not None and local(j) > 0 and not (i == 0 and start is not None and KUNYA_FIRST.match(seg))
              and (f := fame_pick({x for x in c if local(x) == local(j)})) is not None):
            # Equally linked to the neighbours: Abu Nadra lists both Abu Sa'id al-Khudri and Samura
            # b. Jundub (also "أبو سعيد"); the far more cited one (3x the students) is meant. Not for
            # the compiler's own shaykh named by kunya: al-Hakim's "أبو بكر بن إسحاق" is Ahmad b.
            # Ishaq al-Faqih, not the far more cited Ibn Khuzayma (who died before al-Hakim was born).
            out.append((seg, f, 'chain_fame'))
        elif (j is not None and local(j) > 0
              and (f := named_pick([x for x in c if local(x) == local(j)], seg, left, right)) is not None):
            # Equally linked, but a neighbour names only one of them with this very form. Phase 3: ties
            # such as «أبو معاوية» (al-Darir / Hushaym) hid tadlis findings.
            out.append((seg, f, 'chain_named'))
        elif (FULL_KUNYA_NAME.match(seg) and all(local(x) == 0 for x in c)
              and (f := fame_pick(set(c))) is not None):
            # No context at all: "أبو العباس محمد بن يعقوب" is al-Asamm (18 students listed),
            # not al-Ahwazi (6). Only for kunya + ism + father: on shorter forms ("ابن أبي
            # مليكة", "عبدان") the most-cited candidate is often not the narrator meant.
            out.append((seg, f, 'fame'))
        elif (d := default_of.get(seg.strip())) is not None and d in c and (j is None or local(d) >= local(j)):
            out.append((seg, d, 'default'))
        else:
            out.append((seg, None, 'ambiguous'))
            best = max(local(x) for x in c)
            last_ties[i] = [x for x in c if local(x) == best][:TIES_KEPT]   # c is ranked by fame
    return out
