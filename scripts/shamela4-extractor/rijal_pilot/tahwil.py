"""Isnads with several chains (تحويل): split a record's isnad into branches.

About one hadith in ten has more than one chain, written in three ways that the old segmenter flattened
into a single line (Mustadrak 493 joined «قتيبة» to «الصيدلاني», which is false):

  1. Alternatives at one level, one shared tail:
       «حدثنا A، وB، وC، قالوا: ثنا X، ثنا Y، عن Z»   ->  A→X→Y→Z, B→X→Y→Z, C→X→Y→Z
     also with several sub-heads: «حدثنا قيس، حدثنا قتيبة. وأخبرني الصيدلاني، حدثنا ابن أيوب؛ قالا: حدثنا إسماعيل».
  2. Parts joined by the tahwil mark «ح»: «حدثنا A، ثنا B، ح وحدثنا C، ثنا D، عن E، عن F». Each part is a chain;
     a part that stops short of the Companion takes the tail of the next part, from the narrator its last
     narrator is listed with (checked against the teacher/student lists, so a wrong join is not made).
  3. «. وأخبرني ...» sentences without «قالا»: handled like parts joined by «ح».

`chain_branches(text)` returns a list of branches; each branch is a list of (segment, verbs) pairs exactly as
gap_test.chain_segments_verbs gives them for one chain, so compare_current.our_chain can resolve each one.
`join_heads(branches)` is applied after resolution (it needs the registry).
Exec'd by compare_current.py after chain_resolver.py (same namespace: chain_segments_verbs, clean_segment,
is_name, VERBS, HARAKAT_RE, NAME_GOES_ON, edge).
"""
import re

MAX_BRANCHES = 8
_INVISIBLE = '[‌-‏‪-‮﻿]'

_TRANSMISSION = r'(?:حدثنا|حدثني|أخبرنا|أخبرني|أنبأنا|أنبأني|ثنا|نا|أنا|أبنا|سمعت)'
# «ح» between two chains: a token on its own, followed by the next chain's verb.
HAH = re.compile(rf'(?<![^\s،.:;])\(?ح\)?(?=[\s،.:;]+و?{_TRANSMISSION}(?=\s))')
# «. وأخبرني ...»: a new sentence that starts a chain.
SENTENCE = re.compile(rf'[.؛]\s*(?=و{_TRANSMISSION}(?=\s))')
# «قالا:» / «قالوا:» after the alternatives, before the shared tail.
SPEECH = re.compile(r'(?:،|\s)(?:قالا|قالوا)(?:\s+جميعا)?\s*[:،]?\s*')
PROPHET = re.compile(r'رسول الله|النبي|ﷺ|صلى الله عليه وسلم')


def _clean(text: str) -> str:
    return normalize_verbs(re.sub(_INVISIBLE, '', HARAKAT_RE.sub('', text)))


def _isnad_end(text: str) -> int:
    m = PROPHET.search(text)
    return m.start() if m else len(text)


def _alternatives(names_text: str) -> list[str]:
    """«A، وB، وC» / «A وB» -> ['A', 'B', 'C']: the names of one level, each cleaned like a segment."""
    names_text = re.sub(r'\([^)]*\)', ' ', names_text)         # «(واللفظ لابن المثنى)»: the editor's remark
    # A comma starts a new name unless the nasab goes on («عبد الملك، بن أبي بكر»).
    out: list[str] = []
    for piece in re.split(r'،', names_text):
        if out and NAME_GOES_ON.match(piece.lstrip() + ' '):
            out[-1] += '،' + piece
        else:
            out.append(piece)
    pieces = []
    for p in out:
        # A conjunction between two names: «A وB» (the name itself may start with «و», so it needs a space before
        # it and a nasab or a kunya after the next word).
        pieces += re.split(r'\s+و(?=\S+\s+بن\s|\S+\s+ابن\s|ابن\s|أب[يوى]\s)', p.strip())
    names = []
    for k, p in enumerate(pieces):
        p = p.strip()
        # «وحسن الحلواني»: the «و» of the list. Kept when the name itself starts with it and is known («وكيع»).
        if k and p.startswith('و') and not (candidates(p) and not candidates(p[1:])):
            p = p[1:]
        s = clean_segment(p)
        if s and is_name(s):
            names.append(s)
    return names


def _split_head(head: str) -> list[list[str]]:
    """Heads of one part: [[sub-head text variants...], ...], one list of alternatives per sub-head."""
    heads = []
    for sub in SENTENCE.split(head):
        sub = sub.strip(' ،:.;')
        if not sub:
            continue
        verbs = list(re.finditer(VERBS, sub))
        if not verbs:
            heads.append([sub])
            continue
        last = verbs[-1]
        prefix, names = sub[:last.end()], sub[last.end():]
        alts = _alternatives(names)
        heads.append([f'{prefix} {a}' for a in alts] if len(alts) > 1 else [sub])
    return heads


def _lead_verb(tail: str) -> str | None:
    m = re.match(rf'\s*و?({_TRANSMISSION}|عن|قال)(?=\s|،)', tail)
    return m.group(1) if m else None


def _branches_of_part(part: str, depth: int = 0) -> list[list[tuple[str, list[str]]]]:
    """Branches of one part (no «ح» inside). One branch when it has no alternatives."""
    part = part.strip()
    zone = part[:min(_isnad_end(part), 1500)]
    m = SPEECH.search(zone) if depth < 3 else None
    if not m:
        return [chain_segments_verbs(part)]
    head, tail = part[:m.start()], part[m.end():]
    heads = _split_head(head)
    # «قالوا:» in the matn («فرأيت ناسا مجتمعين، قالوا: ...») is not a tahwil: the head must name two chains or more.
    if sum(len(alts) for alts in heads) < 2:
        return [chain_segments_verbs(part)]
    tail_branches = _branches_of_part(tail, depth + 1)
    lead = _lead_verb(tail)
    out = []
    for alts in heads:
        for alt in alts:
            head_pairs = chain_segments_verbs(alt)
            if not head_pairs:
                continue
            for tb in tail_branches:
                pairs = [(s, list(v)) for s, v in head_pairs]
                if lead:
                    pairs[-1][1].append(lead)
                out.append(pairs + [(s, list(v)) for s, v in tb])
    return out[:MAX_BRANCHES] or [chain_segments_verbs(part)]


def chain_branches(text: str) -> list[dict]:
    """All chains of a record's isnad (see the module docstring), one when it has no tahwil.

    Each item: {'pairs': [(segment, verbs)], 'part': index of the «ح» / sentence part it came from,
    'complete': False for a head that stops short of the Prophet (the next part supplies its tail)}."""
    t = _clean(text)
    zone = t[:2500]
    parts, pos = [], 0
    for m in HAH.finditer(zone):
        parts.append(t[pos:m.start()])
        pos = m.end()
    parts.append(t[pos:])
    if len(parts) == 1:
        # «. وأخبرني ...» without «ح» or «قالا»: sentences that each start a chain, only inside the isnad
        end = _isnad_end(t)
        if SENTENCE.search(t[:end]) and not SPEECH.search(t[:end]):
            pieces = SENTENCE.split(t[:end])
            parts = pieces[:-1] + [pieces[-1] + t[end:]]
    items, seen = [], set()
    for i, part in enumerate(parts):
        complete = i == len(parts) - 1 or bool(PROPHET.search(part))
        for pairs in _branches_of_part(part):
            key = tuple(s for s, _ in pairs)
            if pairs and key not in seen:
                seen.add(key)
                items.append({'pairs': pairs, 'part': i, 'complete': complete})
    return items[:MAX_BRANCHES] or [{'pairs': chain_segments_verbs(t), 'part': 0, 'complete': True}]
