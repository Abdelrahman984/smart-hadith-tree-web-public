"""Task 4.5 (draft): fill ``parts`` (isnad / matn / remark / text) for the records of data/shamela/<slug>/.

    split(record, slug) -> [{"type": "isnad"|"matn"|"remark"|"text", "start": int, "end": int}, ...]

Spans are offsets into ``record["arabic"]``, ordered and non-overlapping; the whitespace between spans
stays outside. A ``kind == "text"`` record gets ONE part ``{"type": "text"}`` covering the record, so
a reader can always iterate ``parts`` without a special case.

How it works (no registry, no Shamela links needed, so it runs the same on all 31 books):
  1. Tokenize on a normalized copy (harakat, tatweel and marks dropped, alef/yaa/spelled honorifics
     unified) and map every token back to offsets of ``arabic``.
  2. ``parse_chain`` walks isnad elements: transmission verb + name (حدثنا فلان), «عن» + name, tahwil
     «ح», speaker tags (قال X: حدثنا), «أن فلانا أخبره», «بهذا الإسناد», and stops at the first token that
     cannot continue a chain; that point is where the matn starts (after «قال:», or at «أن رسول الله»).
  3. After the matn: a second chain at a sentence start (وحدثنا / أخبرنا … بمثله) opens a new isnad part;
     the compiler's words (per-book openers in REMARKS) open a remark that runs to the record's end.

Usage:
    python split_parts.py --eval [--part tune|held|all] [slug ...]    gold measurement (7 linked books)
    python split_parts.py --show slug N [seed]                         N random records with their parts
    python split_parts.py --write OUT_DIR [slug ...]                   copies of data/shamela with parts
"""
import glob
import json
import os
import random
import re
import shutil
import sys
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
DATA = os.path.join(ROOT, 'data', 'shamela')

# ------------------------------------------------------------------------------------------------
# Normalization and tokens
# ------------------------------------------------------------------------------------------------
_DROP = re.compile('[ً-ٰٟـ​-‏‪-‮﻿ؐ-ؚۖ-ۭ]')
_MAP = str.maketrans({'أ': 'ا', 'إ': 'ا', 'آ': 'ا', 'ٱ': 'ا', 'ى': 'ي', 'ئ': 'ئ'})
PUNCT = '،,؛;:.!؟?«»"“”()[]{}*…‏'
COMMA, COLON, STOP = set('،,؛;'), set(':'), set('.!؟?')
OPENQ, CLOSEQ = set('«"“'), set('»"”')
HONOR = set('﵀﵁﵂﵃﵄﵅﵆﵇﵈﵉﵊﵋﵌﵍﵎﵏ﷻ﷿')
PROPHET_SIGN = 'ﷺ'
NL = chr(10)


class Tok:
    __slots__ = ('s', 'e', 'w', 'core', 'comma', 'colon', 'stop', 'oq', 'cq', 'nl', 'num')

    def __repr__(self):
        return self.core


def tokenize(text: str, quotes: bool = True) -> list[Tok]:
    chars, pos = [], []
    for i, ch in enumerate(text):
        if _DROP.match(ch):
            continue
        chars.append(ch.translate(_MAP))
        pos.append(i)
    norm = ''.join(chars)
    toks: list[Tok] = []
    prev_end_orig = 0
    inside = False                                      # inside a quotation: decides whether a plain " opens or closes
    pending_open = False                                # a lone « / " before a word opens that word's quotation

    def opens(chars: str) -> bool:
        nonlocal inside
        o = False
        for c in chars:
            if c in '«“':
                o, inside = True, True
            elif c == '"':
                if not inside:
                    o, inside = True, True
                else:
                    inside = False
        return o

    def closes(chars: str) -> bool:
        nonlocal inside
        cl = False
        for c in chars:
            if c in '»”':
                cl, inside = True, False
            elif c == '"':
                if inside:
                    cl, inside = True, False
                else:
                    inside = True
        return cl

    for m in re.finditer(r'\S+', norm):
        w = m.group()
        s_o = pos[m.start()]
        e_o = pos[m.end() - 1] + 1
        while e_o < len(text) and _DROP.match(text[e_o]):
            e_o += 1
        core = w.strip(PUNCT + '-–—')
        lead = w[:len(w) - len(w.lstrip(PUNCT + '-–—'))]
        trail = w[len(w.rstrip(PUNCT + '-–—')):]
        if not core:                                    # a lone punctuation token: flags go to the previous one
            if toks and not (set(w) <= set('«“"') and (not inside or w[0] in '«“')):
                p = toks[-1]
                p.comma |= any(c in COMMA for c in w)
                p.colon |= ':' in w
                p.stop |= any(c in STOP for c in w)
                p.cq |= closes(w)
                p.e = e_o
            elif set(w) <= set('«“"'):
                pending_open |= opens(w)
            prev_end_orig = e_o
            continue
        t = Tok()
        t.s, t.e, t.w, t.core = s_o, e_o, w, core
        t.oq = (opens(lead) or pending_open) and quotes
        pending_open = False
        t.comma = any(c in COMMA for c in trail)
        t.colon = ':' in trail
        t.stop = any(c in STOP for c in trail)
        t.cq = closes(trail)
        t.nl = NL in text[prev_end_orig:s_o] if toks else False
        t.num = bool(re.search(r'[0-9٠-٩]', core))
        toks.append(t)
        prev_end_orig = e_o
    return _merge_honorifics(toks)


_HON_SEQ = [  # (words, glyph)
    (('صلي', 'الله', 'عليه', 'وسلم'), PROPHET_SIGN), (('صلي', 'الله', 'عليه', 'وعلي', 'اله', 'وسلم'), PROPHET_SIGN),
    (('صلي', 'الله', 'عليه', 'واله', 'وسلم'), PROPHET_SIGN), (('عليه', 'السلام'), '﵊'),
    (('رضي', 'الله', 'عنه'), '﵁'), (('رضي', 'الله', 'عنها'), '﵂'), (('رضي', 'الله', 'عنهما'), '﵄'),
    (('رضي', 'الله', 'عنهم'), '﵁'), (('رضي', 'الله', 'تعالي', 'عنه'), '﵁'), (('رضي', 'الله', 'تعالي', 'عنها'), '﵂'),
    (('رضي', 'الله', 'تعالي', 'عنهما'), '﵄'), (('رحمه', 'الله'), '﵀'), (('رحمه', 'الله', 'تعالي'), '﵀'),
]


def _merge_honorifics(toks: list[Tok]) -> list[Tok]:
    out: list[Tok] = []
    i = 0
    while i < len(toks):
        hit = None
        for words, glyph in sorted(_HON_SEQ, key=lambda x: -len(x[0])):
            k = len(words)
            if i + k <= len(toks) and all(toks[i + j].core == words[j] for j in range(k)):
                hit = (k, glyph)
                break
        if hit:
            k, glyph = hit
            t = toks[i + k - 1]
            t.s, t.core, t.w = toks[i].s, glyph, glyph
            t.nl = toks[i].nl
            t.oq = toks[i].oq
            out.append(t)
            i += k
        else:
            out.append(toks[i])
            i += 1
    return out


# ------------------------------------------------------------------------------------------------
# Chain grammar
# ------------------------------------------------------------------------------------------------
_TV = {'حدثنا', 'حدثني', 'حدثناه', 'حدثنيه', 'حدثنى', 'حدثتني', 'حدثتنا', 'حدثت', 'اخبرنا', 'اخبرني', 'اخبرناه',
       'اخبرنيه', 'اخبرت', 'انبانا', 'انباني', 'انبا', 'انبانيه', 'ثنا', 'نا', 'انا', 'ابنا', 'سمعت', 'سمع', 'سمعنا', 'سمعناه', 'قرئ', 'سمعا', 'روي', 'روى', 'حدثكم', 'اخبركم', 'اخبرك', 'حدثك',
       'سمعته', 'سمعتها', 'حدثنيها', 'حدثناها', 'اخبرتني', 'اخبرتنا', 'يحدث', 'حدث', 'ذكر', 'قرات', 'ثني', 'حدثنا'}
_PV = {'وبه', 'به', 'رفعه', 'يرفعه', 'يبلغ', 'ينميه', 'يرويه', 'مرفوعا', 'اخبره', 'اخبرهم', 'حدثه', 'حدثهم', 'حدثاه', 'اخبراه', 'اخبرها', 'حدثها', 'اخبرنا', 'حدثتني', 'اخبرتني',
       'اخبرني', 'حدثني', 'حدثنا', 'حدثوه', 'اخبروه', 'اخبرهما', 'حدثهما', 'حدثتها', 'حدثته', 'اخبرته', 'سمعه', 'ذكره'}
_SV = {'قال', 'قالت', 'قالا', 'قالوا', 'يقول', 'تقول', 'يقولون', 'يقولان', 'قالتا', 'فقال', 'فقالت'}
_AV = {'ان', 'انه', 'انها', 'انهما', 'انهم'}
_TAHWIL = {'ح', 'ححح'}
_CONN = {'واحد', 'المعني', 'جميعا', 'كلاهما', 'كلهم', 'كلهما', 'جميعهم', 'كليهما', 'كلتاهما'}
_YANI = {'يعني', 'وهو', 'هو', 'يعنون', 'يعنيان', 'يعنى'}
_MITHL = {'ومعناه', 'ونحوه', 'بمثله', 'مثله', 'بنحوه', 'نحوه', 'بمعناه', 'بلفظه', 'بمثلها', 'بنحوها', 'بمثلهما',
          'بمعني', 'بذلك', 'بمثلهم', 'بنحوهم', 'بمثلة'}
_MITHL_WEAK = {'مثل', 'بمثل', 'نحو', 'بنحو', 'بحديث', 'بحديثه'}   # «مثل المؤمن …» opens a matn; «مثل حديث فلان» is a reference
_ISNAD_REF = {'بهذا', 'وباسناده', 'باسناده', 'باسنادهم', 'بالاسناد', 'باسنادهما', 'بهذين'}
_STOPWORDS = {'كان', 'كانت', 'كنا', 'كنت', 'كانوا', 'جاء', 'جاءت', 'رجلا', 'رجل', 'امراة', 'اتي', 'اتيت', 'دخل', 'دخلت',
              'خرج', 'خرجت', 'خرجنا', 'نهي', 'امر', 'امرنا', 'قلت', 'راي', 'رايت', 'رايته', 'سالت', 'سال', 'فقال',
              'فقالت', 'ثم', 'لا', 'ما', 'لم', 'لن', 'اذا', 'لما', 'قد', 'ان', 'هذا', 'هذه', 'ذلك', 'التي', 'الذي',
              'من', 'الي', 'في', 'ليس', 'اني', 'انا', 'اناس', 'يا', 'اللهم', 'اما', 'انما', 'كل', 'حتي', 'لو', 'هل',
              'بينما', 'بينا', 'اتينا', 'اتاه', 'فلما', 'وكان', 'وقد', 'قام', 'قمت', 'وقف', 'مررت', 'مر', 'اقبل',
              'ولا', 'وما', 'فان', 'فاذا', 'واذا', 'وان', 'و', 'اذ', 'اي', 'ايها'}
_PROPHET_PREV = {'مولي', 'خادم', 'صاحب', 'كاتب', 'زوج', 'حاجب', 'اخو', 'خال', 'عم', 'ابن', 'موالي', 'وفد', 'ام', 'ازواج',
                 'زوجة', 'امراة', 'اخي', 'عمة'}


_PV_RE = re.compile(r'^(?:اخبر|حدث|انبا|سمع)(?:ت|ا|و)?(?:ه|ها|هم|هما|هن|ني|نا|كم|ك)$')


def _is_mithl(toks: list[Tok], i: int) -> bool:
    """«بمثله», «نحوه», «بمثل حديث فلان»: the matn is another hadith's."""
    if i >= len(toks) or toks[i].oq:
        return False
    c = toks[i].core
    if c in _MITHL_WEAK:
        return i + 1 < len(toks) and toks[i + 1].core in ('حديث', 'ذلك', 'هذا', 'ما', 'حديثه', 'رواية', 'هذه', 'حديثهما', 'لفظ')
    return c in _MITHL


def vclass(core: str) -> str | None:
    """T transmit, P post-verb ("أخبره"), S speaker, A «أن», H tahwil, C «عن» — None otherwise."""
    c = core
    if c in _TAHWIL:
        return 'H'
    for cand in (c, c[1:] if c[:1] in 'وف' else None):
        if not cand:
            continue
        if cand == 'عن':
            return 'C'
        if cand in _AV:
            return 'A'
        if cand in _SV:
            return 'S'
        if cand in _TV:
            return 'T'
        if cand in _PV or _PV_RE.match(cand):
            return 'P'
    return None


def is_prophet_start(toks: list[Tok], k: int) -> int | None:
    """The index after «النبي ﷺ» / «رسول الله ﷺ» when it starts at k."""
    n = len(toks)
    c = toks[k].core
    if c == 'ابو' and k + 1 < n and toks[k + 1].core == 'القاسم' and k + 2 < n and toks[k + 2].core == PROPHET_SIGN:
        return k + 3
    if c in ('النبي', 'نبي') or (c == 'رسول' and k + 1 < n and toks[k + 1].core == 'الله'):
        j = k + (1 if c != 'رسول' else 2)
        if c == 'نبي' and j < n and toks[j].core == 'الله':
            j += 1
        if j < n and toks[j].core == PROPHET_SIGN:
            j += 1
        return j
    return None


def parse_name(toks: list[Tok], j: int, cap: int = 30, vague: bool = False):
    """(index after the name, kind) — kind in name / prophet / honor — or None."""
    n = len(toks)
    if j >= n:
        return None
    pj = is_prophet_start(toks, j)
    if pj is not None:
        return pj, 'prophet'
    if _is_mithl(toks, j) or toks[j].core in _ISNAD_REF:
        return None
    k, words = j, 0
    while k < n and words < cap:
        t = toks[k]
        c = t.core
        if k > j:
            if t.nl and not toks[k - 1].comma and False:
                break
            v = vclass(c)
            if (v in ('T', 'S', 'A', 'C', 'P', 'H') and c not in ('بن', 'ابن')) or _is_mithl(toks, k) \
                    or c in _ISNAD_REF or c in _CONN:
                break
        soft = k > j and k + 1 < n and ((c == 'من' and (toks[k + 1].core[:2] == 'ال' or toks[k + 1].core in ('اهل', 'بني', 'حمير', 'قريش', 'اصحاب')))
                                      or (c in ('رجل', 'رجلا', 'امراة') and toks[k + 1].core == 'من'))
        if t.oq or t.num or (c in _STOPWORDS and not soft and not (vague and k == j and c in ('رجل', 'امراة', 'رجلا', 'من'))):
            if k == j:
                return None
            break
        if c == PROPHET_SIGN:
            if k > j and toks[k - 1].core == 'الله' and k >= 2 and toks[k - 2].core == 'رسول':
                prev = toks[k - 3].core if k >= 3 else ''
            elif k > j and toks[k - 1].core in ('النبي', 'نبي'):
                prev = toks[k - 2].core if k >= 2 else ''
            else:
                return None
            if prev in _PROPHET_PREV:
                k += 1
                words += 1
                continue
            return None
        if c in HONOR:
            if k == j:
                return None
            e = k + 1                                            # «عائشة ﵂ زوج النبي ﷺ»
            if e < n and toks[e].core in ('زوج', 'زوجة', 'ام') and e + 1 < n:
                pj = is_prophet_start(toks, e + 1) if toks[e].core != 'ام' else None
                if pj is not None:
                    e = pj
                elif toks[e].core == 'ام' and toks[e + 1].core == 'المؤمنين':
                    e += 2
            return e, 'honor'
        words += 1
        k += 1
        if (t.comma or t.colon or t.stop or t.cq) and c not in _YANI:
            break
    if words == 0:
        return None
    return k, 'name'


# Interjections inside an isnad: «أو أحدهما، شك إسحاق، عن …», «يحدث في المسجد أنه سمع …», «- أو ابن X -», «رفعه».
_INTERJ = {'او', 'شك', 'احدهما', 'يحدث', 'يحدثنا', 'يعني', 'وهو', 'هو', 'اظنه', 'احسبه', 'رفعه', 'يرفعه', 'يبلغ', 'ينميه',
           'يبلغه', 'مرفوعا', 'وزعم', 'زعم', 'يزعم', 'وقال', 'ابن', 'قال', 'وتقاربا', 'تقاربا', 'فذكر', 'وذكر'}


_APPOS = {'مولي', 'ختن', 'ثم', 'وكان', 'وهو', 'خادم', 'كاتب', 'صاحب', 'اخو', 'عم', 'خال', 'ابن', 'موالي', 'نزيل', 'قاضي', 'امام'}
_P_INTERJ = {'به', 'رفعه', 'يرفعه', 'يبلغ', 'ينميه', 'يرويه', 'مرفوعا'}
_INTERJ_BLOCK = {'كان', 'كانت', 'كنا', 'جاء', 'جاءت', 'رجلا', 'رجل', 'ثم', 'لا', 'ما', 'لم', 'قد', 'قلت', 'فقال', 'فقالت', 'نهي', 'امر'}


def _skip_interjection(toks: list[Tok], i: int, names: int):
    """Index of the next «عن» / verb when toks[i] opens a short aside inside the chain, else None."""
    if names == 0:
        return None
    if toks[i].core == 'في' and i + 1 < len(toks) and toks[i + 1].core in ('هذه', 'هذا', 'حديث', 'حديثه'):
        for q in range(i + 2, min(len(toks), i + 7)):
            if vclass(toks[q].core) in ('S', 'C', 'T', 'A'):
                return q
        return None
    if toks[i].core not in _INTERJ:
        return None
    n = len(toks)
    for q in range(i + 1, min(n, i + 11)):
        t = toks[q]
        if t.oq or toks[q - 1].stop:
            return None
        if vclass(t.core) in ('C', 'T', 'A') or (vclass(t.core) == 'S' and t.colon):
            return q
        if t.core in _INTERJ_BLOCK:
            return None
    return None


_REF_RE = re.compile(r'(?:ف?ذكر|ثم ذكر|و?ذكر|فذكروا) (?:الحديث )?(?:ب)?(?:مثل|نحو|بمعني)|(?:في|ب)هذا الاسناد|(?:ب)?(?:مثل|نحو) حديث'
                     r'|ب(?:مثله|نحوه)')


def _ref_tail(toks: list[Tok], m0: int) -> bool:
    """A short tail that only points to another hadith's text («في هذا الإسناد بمثله», «ثم ذكر مثله»): no matn."""
    if m0 >= len(toks) or len(toks) - m0 > 45:
        return False
    if any(t.core == PROPHET_SIGN for t in toks[m0:m0 + 6]):
        return False
    return bool(_REF_RE.search(' '.join(t.core for t in toks[m0:m0 + 12])))


def _prophet_clause_ahead(toks: list[Tok], j: int) -> bool:
    """toks[j] is a speaker verb: do consecutive «قال X:» clauses end in «قال النبي ﷺ:» (the saying itself)? If not, the
    first clause is the frame of the report (Shamela starts the matn there)."""
    n = len(toks)
    for _ in range(4):
        if j >= n or vclass(toks[j].core) != 'S':
            return False
        if is_prophet_start(toks, j + 1) is not None:
            return True
        q = _adj_colon(toks, j)
        if q is None or q + 1 >= n:
            return False
        j = q + 1
    return False


def _adj_colon(toks: list[Tok], i: int):
    """i is a speaker verb: index of the colon token when it closes «قال [النبي ﷺ | قيس بن عباد]:», else None."""
    if toks[i].colon:
        return i
    r = parse_name(toks, i + 1, cap=5)
    if r and r[0] - 1 > i and toks[r[0] - 1].colon:
        return r[0] - 1
    return None


def _frame_start(toks: list[Tok], i: int, floor: int) -> int:
    """Walk back from the speaker verb i over «رسول الله ﷺ», «سمع», «أنه»: the report's own frame."""
    j = i
    while j - 1 >= floor and toks[j - 1].core in ('النبي', 'رسول', 'الله', PROPHET_SIGN, 'نبي'):
        j -= 1
    if j - 1 >= floor and vclass(toks[j - 1].core) == 'T' and toks[j - 1].core.lstrip('و') in ('سمع', 'سمعت', 'سمعنا', 'سمعته'):
        j -= 1
        if j - 1 >= floor and vclass(toks[j - 1].core) == 'A':
            j -= 1
    return j


def parse_chain(toks: list[Tok], i0: int, bare: bool = False):
    """Walk one isnad from token i0. Returns dict(end, matn, names, mithl): ``end`` = index after the last chain
    token, ``matn`` = index of the first matn token (None when the text ends inside the chain)."""
    n = len(toks)
    i = last = i0
    names = 0
    saw_ref = False
    ref_at = -1
    res = {'end': i0, 'matn': None, 'names': 0, 'ref': False}

    def done(matn_at):
        res.update(end=last, matn=matn_at, names=names, ref=saw_ref and names == ref_at)
        return res

    # a chain may open with a bare name ("مالك ؛ أنه", "عبد الرزاق، عن معمر")
    if bare and i0 < n and vclass(toks[i0].core) is None and not _is_mithl(toks, i0):
        r = parse_name(toks, i0)
        if r and r[1] == 'name' and (toks[r[0] - 1].comma or (r[0] < n and vclass(toks[r[0]].core) in ('C', 'A', 'T', 'S'))):
            i = last = r[0]
            names += 1
        else:
            return done(i0)

    while i < n:
        t = toks[i]
        c = t.core
        v = vclass(c)
        if names > 0 and c in _APPOS and i > i0 and not t.oq:        # «كريب، مولى ابن عباس»: an apposition to the name
            j = i + 1
            while j < n and j < i + 12 and not toks[j - 1].comma and not toks[j - 1].colon and not toks[j].oq                     and vclass(toks[j].core) is None:
                j += 1
            i = last = j
            continue
        if names > 0 and i > i0 and v is None and toks[i - 1].core in _P_INTERJ and is_prophet_start(toks, i) is not None:
            i = last = is_prophet_start(toks, i)                 # «يبلغ به النبي ﷺ»
            continue
        if t.oq and (names > 0 or i > i0):               # a quotation opens: the matn (Shamela's own matn spans start here)
            return done(i)
        if v == 'H':
            i += 1
            last = i
            continue
        if names > 0 and (c == 'من' and i + 1 < n and toks[i + 1].core in ('اصل', 'كتابه', 'كتاب', 'اصول')
                          or c in ('في', 'فيما', 'قراءة', 'املاء', 'إملاء', 'املاء') and i + 1 < n
                          and toks[i + 1].core in ('كتاب', 'كتابه', 'مسند', 'مسنده', 'اصل', 'تاريخ', 'قراءتي', 'كتابي', 'اماليه', 'اخرين')):
            j = i + 1                                      # «من أصل كتابه», «في كتاب النسب»: an aside inside the isnad
            while j < n and j < i + 9 and not toks[j - 1].comma and not toks[j - 1].colon:
                j += 1
            i = last = j
            continue
        if c in _CONN:
            i += 1
            last = i
            continue
        if c in _YANI and names > 0:                     # «هشام (يعني ابن سليمان)»: the name goes on
            r = parse_name(toks, i + 1)
            if r:
                i = last = r[0]
                continue
        if c in ('باسناد', 'باسنادهم', 'باسنادهما') and names > 0:     # «بإسناد عباد ومعناه»
            j = i + 1
            while j < n and j < i + 5 and not _is_mithl(toks, j) and not toks[j].comma and vclass(toks[j].core) is None:
                j += 1
            saw_ref = True
            ref_at = names
            i = last = j
            continue
        if _is_mithl(toks, i):
            return done(i)
        if c in _ISNAD_REF:
            j = i + 1
            if c == 'بهذا' and j < n and toks[j].core in ('الاسناد', 'الاسنادين', 'الحديث', 'الاسنادين'):
                j += 1
            i = last = j
            saw_ref = True
            ref_at = names
            continue
        if v in ('T', 'C'):
            r = parse_name(toks, i + 1, vague=(v == 'C' or c in ('حدثنا', 'حدثني')))
            if r is None:
                q = _skip_interjection(toks, i, names)
                if q is not None:
                    i = last = q
                    continue
                return done(i)
            j, kind = r
            if v == 'T' and c.lstrip('و') in ('سمعت', 'سمع', 'سمعنا') and j < n and kind == 'name'                     and not (toks[j - 1].comma or toks[j - 1].colon or toks[j - 1].stop)                     and vclass(toks[j].core) is None and not _is_mithl(toks, j) and toks[j].core not in _CONN                     and toks[j].core not in _ISNAD_REF and toks[j].core[:1] != 'و':
                return done(i)                              # «سمعت أبا ذر وتلا هذه الآية»: a report, not a link
            names += 1
            while j < n and toks[j - 1].comma and toks[j].core[:1] == 'و' and vclass(toks[j].core) is None \
                    and not _is_mithl(toks, j):
                r2 = parse_name(toks, j)
                if not r2:
                    break
                j = r2[0]
            i = last = j
            continue
        if v == 'P':
            i += 1
            last = i
            continue
        if v == 'S':
            nxt = toks[i + 1] if i + 1 < n else None
            if nxt is None:
                last = i + 1
                return done(None)
            nv = vclass(nxt.core)
            if t.colon:
                if nv in ('T', 'H', 'C'):
                    i += 1
                    last = i
                    continue
                if nxt.core == 'هذا' and i + 3 < n and toks[i + 2].core == 'ما' and vclass(toks[i + 3].core) == 'T':
                    i += 2                                  # «قال: هذا ما حدثنا أبو هريرة …»
                    last = i
                    continue
                if nxt.core in _YANI:
                    i += 1                                  # «قال: يعني الوليد، ثنا …»
                    last = i
                    continue
                if nv == 'A' and i + 2 < n:
                    ra = parse_name(toks, i + 2)
                    if ra is not None and ra[0] < n and vclass(toks[ra[0]].core) == 'S':
                        i += 1                              # «قال جابر: إن رسول الله ﷺ قال: …»
                        last = i
                        continue
                if nv == 'S':                               # «قال: قال النبي ﷺ: …» goes on; «قال: قال النبي ﷺ يوم كذا: …» starts at the second قال
                    q = _adj_colon(toks, i + 1)
                    if q is None:
                        last = i + 1
                        return done(i + 1)
                    if q + 1 < n and vclass(toks[q + 1].core) in ('T', 'C', 'S'):
                        i = last = q + 1
                        continue
                    last = q + 1
                    return done(q + 1)
                r2 = parse_name(toks, i + 1, cap=8)      # «قال هناد: عن أبي الطفيل» — a speaker tag, then the link
                if r2 and r2[1] == 'name' and toks[r2[0] - 1].comma and r2[0] < n and vclass(toks[r2[0]].core) == 'C':
                    i = last = r2[0]
                    continue
                last = i + 1
                return done(i + 1)
            if nv == 'T':
                i += 1
                last = i
                continue
            # «قال ابن المثنى: حدثنا …» — a speaker tag in front of the next link
            k = next((q for q in range(i + 1, min(n, i + 8)) if toks[q].colon), None)
            if k is not None and k + 1 < n and vclass(toks[k + 1].core) == 'T':
                i = last = k + 1
                continue
            r3 = parse_name(toks, i + 1, cap=8)              # «قال هشام بن عروة، عن أبيه» — a speaker tag, then the link
            if r3 and r3[1] == 'name' and toks[r3[0] - 1].comma and r3[0] < n and vclass(toks[r3[0]].core) == 'C':
                i = last = r3[0]
                continue
            q = _adj_colon(toks, i)                          # «قال النبي ﷺ: …» «قال قيس بن عباد: …»
            if q is not None:
                if q + 1 < n and vclass(toks[q + 1].core) in ('T', 'C', 'S'):
                    i = last = q + 1
                    continue
                last = q + 1
                return done(q + 1)
            if nv == 'S' or nv == 'A':
                i += 1
                last = i
                continue
            # «سمعت رسول الله ﷺ يقول عام الفتح وهو بمكة: …» — words between the verb and the colon: the report itself
            # starts at its frame («أنه سمع …», «قال النبي ﷺ يوم كذا: …»), as Shamela's matn spans do
            for q in range(i + 1, min(n, i + 10)):
                if toks[q].stop:
                    break
                if toks[q].colon and q + 1 < n:
                    j = _frame_start(toks, i, i0)
                    last = j
                    return done(j)
            last = i + 1
            return done(i + 1)
        if v == 'A':
            nxt = toks[i + 1] if i + 1 < n else None
            if nxt is None:
                return done(None)
            nv = vclass(nxt.core)
            if nv in ('S', 'T', 'P'):
                i += 1
                last = i
                continue
            r = parse_name(toks, i + 1)
            if r is not None and r[1] == 'prophet':
                # «أن النبي ﷺ قال: …» the words follow the colon; «أن النبي ﷺ كان يفعل» the report starts at «أن»
                if r[0] < n and vclass(toks[r[0]].core) == 'S':
                    i = last = r[0]
                    continue
                if r[0] < n and toks[r[0]].core in ('كان', 'كانت', 'نهي'):
                    last = r[0]                        # «أن رسول الله ﷺ كان …» / «نهى …»: 83% of gold starts after «ﷺ»
                    return done(r[0])
                return done(i)
            if r is not None and r[0] < n and vclass(toks[r[0]].core) == 'P':
                names += 1
                i = last = r[0] + 1
                continue
            if r is not None and r[1] in ('name', 'honor') and r[0] + 1 < n and toks[r[0]].core in ('كان', 'كانت')                     and vclass(toks[r[0] + 1].core) == 'S':
                names += 1                                  # «أن ابن عمر كان يقول: …»
                i = last = r[0] + 1
                continue
            if r is not None and r[1] in ('name', 'honor') and r[0] < n and vclass(toks[r[0]].core) in ('S', 'T'):
                names += 1                                  # «أن ابن عمر قال: …»: he is the next link
                i = last = r[0]
                continue
            return done(i)
        q = _skip_interjection(toks, i, names)
        if q is not None:
            i = last = q
            continue
        return done(i)
    return done(None)


# ------------------------------------------------------------------------------------------------
# The compiler's remarks: per-book openers, matched at a sentence / line start after the matn begins.
# Patterns are written on the normalized text (alef unified, ى -> ي, no harakat).
# ------------------------------------------------------------------------------------------------
_COMMON = [
    r'قال (?:الحاكم|البيهقي|الدارقطني|الالباني|الاعظمي|الدارمي|الزيلعي|الحليمي)',
    r'(?:تفرد به|لم يروه|لم يرو هذا|لا يروي هذا|هذا حديث (?:صحيح|حسن|غريب|ضعيف|منكر|لا يصح)|هذا اسناد)',
]
# Editions whose text carries Shamela's own matn marks: the opening « of the quotation is where Shamela's matn group
# starts in 99.8% of the records of these six books (the linked ones, measured in the 80% tuning part); an empty group
# («بهذا الإسناد مثله») has no quote. Elsewhere only a quote within a short lead-in after the chain is used.
QUOTE_BOOKS = {'bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'musnad_tayalisi'}
REMARKS: dict[str, dict] = {
    'bukhari': dict(pats=[r'قال ابو عبد الله', r'قال الفربري', r'تابعه', r'و?تابعه', r'قال ابن عبد الله', r'قال ابو عبد'],
                    nl=False),
    'muslim': dict(pats=[r'قال ابو (?:اسحاق|احمد|عبد الله|بكر)', r'و?في (?:رواية|حديث) (?:\S+ ){0,3}(?:قال|بدل|زاد|ليس)',
                         r'قال ابو (?:اسحاق|احمد)'], nl=False),
    'abudawud': dict(pats=[r'قال ابو داود', r'قال ابو عبد الله', r'قال ابو علي', r'وقال ابو داود', r'قال ابو داوود',
                           r'قال (?:احمد|ابن|محمد) '], nl=True, nl_min=1),
    'tirmidhi': dict(pats=[r'و?هذا حديث', r'و?في الباب', r'و?العمل (?:علي|عليه)', r'و?قد (?:روي|روى|رواه)', r'و?روي عن',
                           r'و?حديث (?:\S+ ){1,3}(?:حديث|حسن|غريب|صحيح)', r'قال ابو عيسي', r'و?يروي', r'و?قال بعض',
                           r'و?هو قول', r'و?سمعت محمدا', r'و?سالت محمد', r'و?اختلف', r'و?قال (?:الشافعي|احمد|اسحاق|سفيان)',
                           r'و?وقد قال', r'و?هكذا (?:روي|روى)', r'و?روي هذا', r'و?روى', r'و?هذا (?:اصح|احسن)'],
                     nl=True),
    'nasai': dict(pats=[r'قال ابو عبد الرحمن', r'هذا (?:حديث|خطا|الحديث|اولي)', r'و?الصواب', r'و?هذا خطا',
                        r'خالفه'], nl=False, trim_num=True),
    'ibnmajah': dict(pats=[r'قال ابو (?:عبد الله|الحسن)', r'قال محمد بن يزيد'], nl=False),
    'malik': dict(pats=[r'(?:و)?قال (?:مالك|يحيي)', r'قال يحيي', r'كمل كتاب', r'تم كتاب'], nl=False, self_remark=True, bare=True),
    'ahmed': dict(pats=[r'قال (?:عبد الله|ابو عبد الله|عبد الله بن احمد)', r'حقق هذا', r'شعيب الارنؤوط', r'عدد الاحاديث',
                        r'قال شعيب', r'قال ابو عبد'], nl=False),
    'darimi': dict(pats=[r'قال ابو محمد', r'\[ب? ?\d+', r'\[ب '], nl=False),
    'aladab_almufrad': dict(pats=[r'(?:صحيح|ضعيف|حسن)'], nl=True, nl_min=1),
    'shamail_muhammadiyah': dict(pats=[r'قال ابو عيسي', r'و?هذا حديث'], nl=False),
    'musannaf_ibnabi_shaybah': dict(pats=[], nl=False),
    'musannaf_abdurrazzaq': dict(pats=[r'قال عبد الرزاق'], nl=False, bare=True),
    'sahih_ibn_hibban': dict(pats=[r'قال ابو حاتم', r'قال الشيخ', r'تفرد به', r'هذا خبر', r'وقد (?:تركنا|احتججنا)',
                                   r'وانما نملي', r'انتهي المجلد', r'وبه ينتهي', r'بحمد الله ومنته'], nl=False),
    'sahih_ibn_khuzaymah': dict(pats=[r'قال ابو بكر', r'قال الالباني', r'قال الاعظمي', r'علق الالباني', r'ترجم المصنف',
                                      r'\d+ - قال', r'و?قال الالباني'], nl=False),
    'musnad_bazzar': dict(pats=[r'و?هذا (?:الحديث|الكلام|الاسناد)', r'و?لا نعلم', r'و?هذان الحديثان', r'و?هذه الاحاديث',
                                r'و?قد (?:رواه|روي|روى)', r'ما روي', r'و?اما حديث', r'قال ابو بكر', r'و?لا اعلم'], nl=False),
    'mujam_kabir_tabarani': dict(pats=[r'لم يروه', r'لم يرو', r'لا يروي', r'تفرد به'], nl=False),
    'mujam_awsat_tabarani': dict(pats=[r'لم يرو', r'لا يروي', r'لم يروه', r'تفرد به', r'لا نعلم'], nl=True, nl_min=1),
    'mujam_saghir_tabarani': dict(pats=[r'لم يروه', r'لم يرو', r'لا يروي', r'تفرد به'], nl=False),
    'musnad_abi_yala': dict(pats=[r'\[حكم', r'حكم حسين'], nl=True, nl_min=1),
    'mustadrak_hakim': dict(pats=[r'هذا (?:حديث|اسناد)', r'و?له شاهد', r'و?اما حديث', r'اما حديث', r'صحيح (?:الاسناد|علي)',
                                  r'قال الحاكم', r'و?قد (?:روي|رواه|روى)', r'و?هذا (?:الحديث|اسناد)', r'ذكر ',
                                  r'و?شاهده', r'و?لهذا الحديث', r'و?هذا لفظ'], nl=True, nl_min=1),
    'sunan_kubra_bayhaqi': dict(pats=[r'قال الشيخ', r'و?كذلك (?:رواه|روي)', r'و?روينا', r'و?قد (?:روي|رواه|روى)', r'و?روى',
                                      r'و?رواه', r'قال (?:الشافعي|ابو|احمد|الامام|البيهقي)', r'و?اخرجه', r'و?اخرجاه',
                                      r'و?في رواية', r'و?في حديث', r'و?لفظ حديث', r'و?ورويناه', r'و?هذا (?:حديث|الحديث|اسناد)',
                                      r'هذا حديث', r'و?تابعه', r'و?روي'], nl=False),
    'shuab_iman_bayhaqi': dict(pats=[r'قال الشيخ', r'رواه (?:البخاري|مسلم)', r'اخرجه (?:البخاري|مسلم)', r'اخرجاه',
                                     r'و?روينا', r'و?قد (?:روي|رواه|روى)', r'و?في رواية', r'و?لفظ حديث', r'قال (?:البيهقي|الحليمي|الامام|ابو|الشافعي)',
                                     r'و?كذلك', r'و?رواه', r'و?روى', r'و?في حديث', r'و?هذا (?:حديث|الحديث|اسناد)',
                                     r'هذا حديث', r'و?ورويناه'], nl=False),
    'mustakhraj_abi_awanah': dict(pats=[r'قال ابو عوانة', r'هذا (?:لفظ|حديث)', r'و?رواه', r'و?قد (?:روي|رواه|روى)',
                                        r'قال ابو (?:بكر|عمر|عبد الله)', r'قال ابن'], nl=False),
    'sunan_daraqutni': dict(pats=[r'تفرد به', r'لم يروه', r'و?هذا (?:وهم|الحديث|اسناد)', r'و?الصواب', r'و?المحفوظ',
                                  r'اسناد (?:صحيح|حسن|ضعيف)', r'وهم ', r'قال الشيخ', r'و?قال', r'وروي عن', r'و?لم يروه'],
                          nl=False),
    'sunan_said_ibn_mansur': dict(pats=[], nl=False, bare=True),
    'musnad_ishaq': dict(pats=[], nl=False),
    'musnad_shafii': dict(pats=[r'قال الشافعي'], nl=False, bare=True),
    'musnad_humaydi': dict(pats=[], nl=False),
    'musnad_tayalisi': dict(pats=[r'قال ابو داود', r'و?روي هذا', r'و?يروي هذا', r'و?هذا الحديث', r'هكذا قال'], nl=False),
    'sunan_kubra_nasai': dict(pats=[r'قال ابو عبد الرحمن', r'هذا (?:حديث|خطا)'], nl=False),
}
# Second iteration: a remark opens only with a book's own signature or the compiler's name/kunya, never a bare «قال:» (dialogue).
# notes = the editor's inline text (takhrij lines, grades): part type "note", to the end of the line.
# strong = signatures that may also open after a comma; variant = «فلم يقل / ولم يذكر …» notes that start with names.
_VARIANT = [r'(?:ف|و)لم يقول(?:ا|وا)?(?: |$)', r'(?:ف|و)لم يقل ', r'(?:ف|و)لم يذكر(?:ا|وا)? ', r'(?:ف|و)لم يرفع(?:ه|وه)?(?: |$)', r'(?:ف|و)لم يسنده']
REMARKS.update({
    'musannaf_ibnabi_shaybah': dict(pats=[], lpats=[r'قال ابو بكر'], nl=False),
    'ahmed': dict(pats=[r'قال عبد الله بن احمد'], lpats=[r'قال (?:عبد الله|ابو عبد الله)'], nl=False,
                  notes=[r'حقق هذا', r'شعيب الارنؤوط', r'عدد الاحاديث', r'قال شعيب', r'حكم']),
    'darimi': dict(pats=[r'قال ابو محمد'], nl=False, notes=[r'ب [٠-٩0-9]+ د', r'ب [٠-٩0-9]+']),
    'musnad_abi_yala': dict(pats=[], nl=False, notes=[r'حكم حسين']),
    'aladab_almufrad': dict(pats=[], nl=False, notes=[r'(?:صحيح|ضعيف|حسن)(?: |$)']),
    'sahih_ibn_khuzaymah': dict(pats=[r'قال ابو بكر'], nl=False, notes=[r'قال الالباني', r'قال الاعظمي', r'علق الالباني', r'ترجم المصنف',
                                                                       r'فقال الالباني', r'وقال الالباني', r'[٠-٩0-9]+ - قال الاعظمي', r'وعلق علي']),
    'musnad_bazzar': dict(pats=[r'و?هذا (?:الحديث|الكلام|الاسناد)', r'و?لا نعلم', r'و?لم يرو', r'و?لا يروي', r'و?هذان الحديثان', r'و?هذه الاحاديث',
                                r'و?قد (?:رواه|روي|روى)', r'ما روي', r'و?اما حديث', r'قال ابو بكر', r'و?لا اعلم'],
                          strong=[r'و?لم يرو', r'و?لا نعلم', r'و?هذا الحديث لا'], variant=_VARIANT, nl=False),
    'sunan_daraqutni': dict(pats=[r'تفرد به', r'لم يروه', r'و?لم يرو', r'وكذلك رواه', r'كذا (?:قال|رواه)', r'هذا (?:اسناد|مرسل|صحيح|وهم|لفظ)', r'و?هذا اسناد',
                                  r'اسناد (?:صحيح|حسن|ضعيف)', r'لم يسنده', r'كلهم ثقات', r'لا يثبت', r'موقوف', r'قال الشيخ', r'و?الصواب', r'و?المحفوظ',
                                  r'و?تابعه', r'و?خالفه', r'و?رواه', r'و?روي عن', r'و?وهم '],
                            strong=[r'تفرد به', r'لم يروه', r'و?خالفه', r'و?لم يسنده'], variant=_VARIANT, nl=False),
    'sahih_ibn_hibban': dict(pats=[r'قال ابو حاتم', r'قال الشيخ', r'تفرد به', r'هذا خبر', r'اراد به', r'يريد به', r'وقد (?:تركنا|احتججنا)',
                                   r'وانما نملي', r'انتهي المجلد', r'وبه ينتهي', r'بحمد الله ومنته'], nl=False, variant=_VARIANT),
    'mustadrak_hakim': dict(pats=[r'هذا (?:حديث|اسناد)', r'و?له شاهد', r'و?اما حديث', r'صحيح (?:الاسناد|علي)', r'قال الحاكم', r'و?قد (?:روي|رواه|روى)',
                                  r'و?هذا (?:الحديث|اسناد)', r'و?شاهده', r'و?لهذا الحديث', r'و?هذا لفظ'], nl=True, nl_min=1),
})

_DEFAULT = dict(pats=[], nl=False)
_compiled: dict[str, re.Pattern] = {}
_TRAIL_MARK = re.compile(r'^[\(\[]?[0-9٠-٩]+[\)\]]?$')


def _remark_re(slug: str) -> re.Pattern | None:
    if slug not in _compiled:
        pats = REMARKS.get(slug, _DEFAULT)['pats'] + _COMMON
        _compiled[slug] = re.compile('^(?:' + '|'.join(pats) + ')')
    return _compiled[slug]


def _compile(slug: str, key: str):
    ck = slug + ':' + key
    if ck not in _compiled:
        pats = REMARKS.get(slug, _DEFAULT).get(key) or []
        _compiled[ck] = re.compile('^(?:' + '|'.join(pats) + ')') if pats else None
    return _compiled[ck]


def _nre(slug):
    return _compile(slug, 'notes')


def _lre(slug):
    return _compile(slug, 'lpats')


def _sre(slug):
    return _compile(slug, 'strong')


def _vre(slug):
    return _compile(slug, 'variant')


def _tail(toks: list[Tok], pos: int, kind: str, slug: str, parts: list) -> None:
    """From a remark / note start to the record's end: editor's notes («note») run to the end of their line, the compiler's
    remark to the next note line."""
    n = len(toks)
    nre = _nre(slug)
    while pos < n:
        if kind == 'note':
            e = next((k for k in range(pos + 1, n) if toks[k].nl), n)
            parts.append(('note', pos, e))
            pos = e
            if pos >= n:
                return
            kind = 'note' if (nre and nre.match(_text_from(toks, pos))) else 'remark'
        else:
            e = next((k for k in range(pos + 1, n) if toks[k].nl and nre and nre.match(_text_from(toks, k))), n)
            parts.append(('remark', pos, e))
            pos = e
            kind = 'note'


def _text_from(toks: list[Tok], k: int, m: int = 9) -> str:
    return ' '.join(t.core for t in toks[k:k + m])


def _boundary(toks: list[Tok], k: int) -> bool:
    """k starts a sentence or a line."""
    if k == 0:
        return True
    p = toks[k - 1]
    return toks[k].nl or p.stop or p.cq or p.w.endswith((']', '﴾', ')')) and not (p.w.endswith(')') and p.num)


STRONG_T = {'حدثنا', 'حدثني', 'اخبرنا', 'اخبرني', 'حدثناه', 'اخبرناه', 'ثنا', 'انبانا', 'نا', 'حدثنيه', 'انباني',
            'اخبرنيه', 'انبا', 'حدثنى', 'اخبرنى', 'انا'}


def _strong_t(c: str) -> bool:
    return c in STRONG_T or (c[:1] == 'و' and c[1:] in STRONG_T) or c == 'ح'


# ------------------------------------------------------------------------------------------------
# split
# ------------------------------------------------------------------------------------------------
def split(record: dict, slug: str, mode: str = 'auto') -> list[dict]:
    text = record['arabic']
    if record.get('kind') == 'text':
        s, e = _trim(text, 0, len(text))
        return [{'type': 'text', 'start': s, 'end': e}] if e > s else []
    if mode == 'auto':
        mode = 'strict' if slug in QUOTE_BOOKS else 'generic'
    toks = tokenize(text, quotes=mode != 'blind')
    n = len(toks)
    if n == 0:
        return []
    cfg = REMARKS.get(slug, _DEFAULT)
    rre = _remark_re(slug)
    parts: list[tuple[str, int, int]] = []         # token-index spans (type, a, b)
    i = 0
    # Malik: «قال مالك: …» / «قال يحيى: سمعت مالكا يقول: …» — the compiler's own words, no isnad
    if cfg.get('self_remark') and re.match(r'^(?:و?قال (?:مالك|يحيي|قال)|و?قال)', _text_from(toks, 0)) \
            and re.match(r'^و?قال(?: قال)? (?:مالك|يحيي)', _text_from(toks, 0)):
        return _emit(text, toks, [('remark', 0, n)])
    while i < n - 1 and toks[i].core in ('ز', 'م', 'و', 'كذلك', 'وكذلك', 'هكذا', 'وهكذا') and (i == 0 or toks[i - 1].core in ('ز', 'م', 'و')):
        i += 1                                      # «ز- حدثنا …» (the editor's zawa'id mark), «(و) عن …», «كذلك حدثنا …»
    if toks[0].core == 'باب':                       # a chapter heading in front of the isnad (first line)
        k = next((k for k in range(1, n) if toks[k].nl), None)
        if k is not None and k < 20:
            i = k
    while i < n - 1 and toks[i].num and len(toks[i].core) <= 6 and vclass(toks[i + 1].core) == 'T':
        i += 1                                      # a stray hadith number in front of the isnad («٣٩ حدثنا …»)
    guard = 0
    while i < n and guard < 8:
        guard += 1
        if i == 0 and (rre.match(_text_from(toks, 0)) or (_lre(slug) and _lre(slug).match(_text_from(toks, 0)))) and not cfg.get('no_start_remark'):
            # the record opens with the compiler's own words («ورواه فلان …», «قال الشيخ: …»): a remark up to a new chain
            k = next((k for k in range(1, n) if _boundary(toks, k) and _strong_t(toks[k].core)
                      and (lambda sc: sc['names'] >= 2 or (sc['names'] >= 1 and sc['ref']))(parse_chain(toks, k))), n)
            parts.append(('remark', 0, k))
            if k >= n:
                break
            i = k
            continue
        ch = parse_chain(toks, i, bare=bool(cfg.get('bare')) or i > 0)
        has_chain = ch['names'] > 0 or ch['ref']
        if not has_chain:
            m0 = i                                  # no isnad: the matn starts here
        else:
            m0 = ch['matn']
            parts.append(('isnad', i, ch['end']))
        if m0 is None:
            break
        q = _quote_start(toks, m0)
        if mode == 'strict' and has_chain:
            q = next((k for k in range(max(i, 1), n) if toks[k].oq), q)
            if q < m0:
                q = m0
        by_ref = False
        if q != m0:                                 # lead-in before the opening quote stays in the isnad
            if has_chain:
                parts[-1] = ('isnad', i, q)
            m0 = q
        elif has_chain and (ch['ref'] or _is_mithl(toks, m0) or _ref_tail(toks, m0)):
            # «بهذا الإسناد»، «بمثله»، «نحوه»: the matn is another hadith's. The reference closes the isnad;
            # a following variant note («وفي حديثه …») is the compiler's own remark, there is no matn.
            j = m0
            if _is_mithl(toks, m0):
                while j < n and j < m0 + 10:
                    j += 1
                    if toks[j - 1].stop or toks[j - 1].comma or toks[j - 1].colon:
                        break
                parts[-1] = ('isnad', i, j)
            m0, by_ref = j, True
            # «بهذا الإسناد، وقال في الحديث: «…»»: the variant words after the colon are the matn Shamela marks
            qc = next((k for k in range(m0, min(n - 1, m0 + 6)) if toks[k].colon), None)
            if mode != 'strict' and qc is not None and not any(toks[k].stop or toks[k].comma for k in range(m0, qc)):
                parts[-1] = ('isnad', i, qc + 1)
                m0, by_ref = qc + 1, False
        # body: from m0; find a remark opener, an editor's note or a new chain at a boundary
        cut, kind = n, None
        for k in range(m0 + (0 if by_ref else 1), n):
            c = toks[k].core
            bnd = _boundary(toks, k)
            if not bnd and not (k - m0 >= 4 and toks[k - 1].comma and _sre(slug) and _sre(slug).match(_text_from(toks, k))):
                v0 = _vre(slug)
                if v0 and k - m0 >= 5 and v0.match(_text_from(toks, k)):
                    # a variant note that opens with names («وأبو مسعود، عن معمر، فلم يقولا …»): back up to the sentence start
                    b0 = next((q for q in range(k - 1, max(m0, k - 25), -1) if toks[q].cq or toks[q].stop or toks[q + 1].nl), None)
                    cut, kind = (b0 + 1 if b0 is not None else k), 'remark'
                    break
                continue
            if bnd and _strong_t(c) and k - m0 >= (0 if by_ref else 2):
                sub = parse_chain(toks, k)
                if sub['names'] >= 2 or (sub['names'] >= 1 and sub['ref']):
                    cut, kind = k, 'isnad'
                    break
            if toks[k].nl and bnd and _nre(slug) and _nre(slug).match(_text_from(toks, k)):
                cut, kind = k, 'note'
                break
            if rre.match(_text_from(toks, k)) or (toks[k].nl and _lre(slug) and _lre(slug).match(_text_from(toks, k))):
                cut, kind = k, 'remark'
                break
            if not bnd:
                continue
            if cfg['nl'] and toks[k].nl and k - m0 >= cfg.get('nl_min', 3):
                cut, kind = k, 'remark'
                break
        if cut > m0:
            parts.append(('remark' if by_ref else 'matn', m0, cut))
        if kind == 'isnad':
            i = cut
            continue
        if kind in ('remark', 'note'):
            _tail(toks, cut, kind, slug, parts)
        break
    return _emit(text, toks, parts)


QMAX = 6


def _quote_start(toks: list[Tok], m0: int) -> int:
    """Shamela's own matn spans start at the opening quote of the quotation: when one opens within a short
    lead-in after the parsed chain end («أن رسول الله ﷺ قال: «…»», «بهذا الإسناد نحوه إلا أنه قال: «…»»), the matn
    starts there and the lead-in stays in the isnad."""
    for q in range(m0, min(len(toks), m0 + QMAX + 1)):
        if toks[q].oq:
            if all(not toks[k].stop for k in range(m0, q)):
                return q
            break
    return m0


def _trim(text: str, s: int, e: int) -> tuple[int, int]:
    while s < e and text[s] in ' \n\t\r‏‎':
        s += 1
    while e > s and text[e - 1] in ' \n\t\r‏‎':
        e -= 1
    return s, e


def _emit(text: str, toks: list[Tok], parts: list[tuple[str, int, int]]) -> list[dict]:
    out = []
    for typ, a, b in parts:
        if b <= a:
            continue
        # trailing marks "(٣٢)", "[٦٨٤٠]" belong to no part; the last two-three words only
        while b - 1 > a and _TRAIL_MARK.match(toks[b - 1].w) and toks[b - 1].num:
            b -= 1
        s, e = toks[a].s, toks[b - 1].e
        if typ == 'isnad':
            e = _isnad_end(toks, a, b, text)
        out.append({'type': typ, 'start': s, 'end': e})
    # the matn of an isnad-only record ("بهذا الإسناد .") — drop a part that is only punctuation
    return [p for p in out if re.search(r'\w', text[p['start']:p['end']])]


def _isnad_end(toks: list[Tok], a: int, b: int, text: str) -> int:
    return toks[b - 1].e


# ------------------------------------------------------------------------------------------------
# I/O, evaluation, CLI
# ------------------------------------------------------------------------------------------------
def book_files(slug: str) -> list[str]:
    fs = [f for f in glob.glob(os.path.join(DATA, slug, '*.json')) if os.path.basename(f) not in ('book.json', 'index.json')]
    return sorted(fs, key=lambda f: int(re.sub(r'\D', '', os.path.basename(f)) or 0))


def load_book(slug: str) -> list[dict]:
    out = []
    for f in book_files(slug):
        out += json.load(open(f, encoding='utf-8'))
    return out


LINKED = ['bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'malik', 'musnad_tayalisi']


def _wordpos(text: str):
    return [m.start() for m in re.finditer(r'\S+', text)]


def _word_index(wp: list[int], off: int) -> int:
    import bisect
    return bisect.bisect_left(wp, off)


def evaluate(slug: str, part: str, **kw) -> dict:
    recs = [r for r in load_book(slug) if r['kind'] == 'hadith']
    if part == 'tune':
        recs = [r for r in recs if r['id'] % 5 != 0]
    elif part == 'held':
        recs = [r for r in recs if r['id'] % 5 == 0]
    st = Counter()
    errs = []
    for r in recs:
        g = r.get('groups') or []
        nar = r.get('narrators') or []
        text = r['arabic']
        parts = split(r, slug, **kw)
        isn = [p for p in parts if p['type'] == 'isnad']
        mat = [p for p in parts if p['type'] == 'matn']
        rem = [p for p in parts if p['type'] == 'remark']
        if rem:
            st['rec_with_remark'] += 1
        if g:                                           # narrators linked inside the matn / remarks are no isnad names
            nar = [x for x in nar if x['end'] <= g[0]['start'] + 1]
        else:                                           # no matn group: the compiler's own words («قال مالك: …»), linked as names
            nar = []
        if nar:
            st['nar_rec'] += 1
            cov = sum(any(p['start'] <= x['start'] and x['end'] <= p['end'] + 1 for p in isn) for x in nar)
            st['nar_total'] += len(nar)
            st['nar_covered'] += cov
            if cov == len(nar):
                st['nar_rec_all'] += 1
            else:
                errs.append(('cov', r['id']))
            # isnad must not run into the matn: no isnad past the gold matn start by more than 3 words
        if g:
            gs = g[0]['start']
            wp = _wordpos(text)
            gi = _word_index(wp, gs)
            ms = mat[0]['start'] if mat else len(text)
            mi = _word_index(wp, ms)
            d = mi - gi
            st['g_rec'] += 1
            empty = g[0]['end'] - g[0]['start'] <= 1
            if empty:
                st['empty_rec'] += 1
            for tol in (0, 1, 3):
                if abs(d) <= tol:
                    st[f'start_pm{tol}'] += 1
                    if empty:
                        st[f'empty_pm{tol}'] += 1
            if abs(d) > 3:
                errs.append(('start', r['id'], d))
        else:
            st['nogroup_rec'] += 1
            if isn:
                st['nogroup_with_isnad'] += 1
            if not isn and not mat:
                st['nogroup_no_isnad_no_matn'] += 1
        if len(isn) > 1:
            st['multi_isnad'] += 1
    st['n'] = len(recs)
    st['errs'] = errs
    return st


def fmt_eval(slug: str, st: Counter) -> str:
    def pct(a, b):
        return f'{100 * a / b:5.1f}%' if b else '  n/a '
    return (f'{slug:18s} n={st["n"]:5d} start+-3 {pct(st["start_pm3"], st["g_rec"])} (+-1 {pct(st["start_pm1"], st["g_rec"])}, '
            f'exact {pct(st["start_pm0"], st["g_rec"])}) | empty-group recs {st["empty_rec"]}: +-3 {pct(st["empty_pm3"], st["empty_rec"])} | '
            f'narrators covered {pct(st["nar_covered"], st["nar_total"])} ({st["nar_total"]}), records all-covered '
            f'{pct(st["nar_rec_all"], st["nar_rec"])} | no-group recs {st["nogroup_rec"]} (isnad given {st["nogroup_with_isnad"]}) '
            f'| remark recs {st["rec_with_remark"]} multi-isnad {st["multi_isnad"]}')


# Independent check of remark detection: a phrase that, wherever it occurs after the matn has begun, is the compiler's own
# (the verdict / the reference). The share of records where it falls inside a ``remark`` part is the recall of the openers.
SIGNATURES = {
    'tirmidhi': r'هذا حديث (?:حسن|صحيح|غريب)', 'mustadrak_hakim': r'هذا حديث صحيح|صحيح الاسناد|صحيح علي شرط',
    'musnad_bazzar': r'لا نعلمه|لا نعلم (?:رواه|له|احدا)|هذا الحديث لا', 'mujam_awsat_tabarani': r'لم يرو هذا الحديث|لا يروي هذا الحديث',
    'mujam_kabir_tabarani': r'لم يروه|لم يرو هذا', 'mujam_saghir_tabarani': r'لم يروه|لم يرو هذا|لا يروي',
    'nasai': r'قال ابو عبد الرحمن', 'abudawud': r'قال ابو داود', 'sahih_ibn_hibban': r'قال ابو حاتم', 'sahih_ibn_khuzaymah': r'قال ابو بكر',
    'sunan_kubra_bayhaqi': r'رواه (?:البخاري|مسلم)|اخرجه (?:البخاري|مسلم)|اخرجاه', 'shuab_iman_bayhaqi': r'رواه (?:البخاري|مسلم)|اخرجه (?:البخاري|مسلم)|اخرجاه',
    'sunan_daraqutni': r'تفرد به|لم يروه', 'musnad_abi_yala': r'حكم حسين', 'aladab_almufrad': r'^(?:صحيح|ضعيف|حسن)',
    'bukhari': r'قال ابو عبد الله|تابعه', 'muslim': r'قال ابو (?:اسحاق|احمد)', 'malik': r'قال مالك', 'ahmed': r'قال عبد الله بن احمد|قال ابو عبد الله',
    'darimi': r'قال ابو محمد', 'ibnmajah': r'قال ابو (?:عبد الله|الحسن)', 'mustakhraj_abi_awanah': r'قال ابو عوانة',
}


def remark_report(slug: str, sample: int = 6, seed: int = 1) -> str:
    recs = [r for r in load_book(slug) if r['kind'] == 'hadith']
    st = Counter()
    sig = re.compile(SIGNATURES[slug]) if slug in SIGNATURES else None
    starts = []
    for r in recs:
        parts = split(r, slug)
        rem = [p for p in parts if p['type'] == 'remark']
        st['records'] += 1
        st['with_remark'] += bool(rem)
        if rem:
            starts.append((r, rem[0]))
        if sig:
            for p in parts:
                if p['type'] in ('isnad', 'matn', 'remark'):
                    seg = ' '.join(t.core for t in tokenize(r['arabic'][p['start']:p['end']]))
                    if sig.search(seg):
                        st[f'sig_in_{p["type"]}'] += 1
                        st['sig_recs'] += 1
                        break
    random.seed(seed)
    out = [f'{slug}: {st["with_remark"]}/{st["records"]} records with a remark ({100 * st["with_remark"] / max(1, st["records"]):.1f}%)']
    if sig:
        tot = st['sig_recs']
        out.append(f'   signature /{SIGNATURES[slug]}/ in {tot} records: in remark {st["sig_in_remark"]}, matn {st["sig_in_matn"]}, isnad {st["sig_in_isnad"]}'
                   f'  (recall {100 * st["sig_in_remark"] / max(1, tot):.1f}%)')
    for r, p in random.sample(starts, min(sample, len(starts))):
        out.append(f'   id {r["id"]}: matn ends ...{r["arabic"][max(0, p["start"] - 40):p["start"]].strip()!r} | REMARK {r["arabic"][p["start"]:p["start"] + 90]!r}')
    return NL.join(out)


def show(slug: str, n: int, seed: int):
    recs = [r for r in load_book(slug)]
    random.seed(seed)
    for r in random.sample(recs, n):
        parts = split(r, slug)
        print(f'--- id {r["id"]} kind {r["kind"]} len {len(r["arabic"])}')
        for p in parts:
            seg = r['arabic'][p['start']:p['end']].replace('\n', ' / ')
            seg = seg if len(seg) <= 260 else seg[:130] + ' ... ' + seg[-110:]
            print(f'  [{p["type"]:6s} {p["start"]}-{p["end"]}] {seg}')


def write_all(out_dir: str, slugs: list[str]):
    stats = {}
    for slug in slugs:
        src = os.path.join(DATA, slug)
        dst = os.path.join(out_dir, slug)
        os.makedirs(dst, exist_ok=True)
        for f in glob.glob(os.path.join(src, '*.json')):
            name = os.path.basename(f)
            data = json.load(open(f, encoding='utf-8'))
            if name not in ('book.json', 'index.json'):
                for r in data:
                    r['parts'] = split(r, slug)
                    c = stats.setdefault(slug, Counter())
                    c['records'] += 1
                    ty = [p['type'] for p in r['parts']]
                    c['remark'] += 'remark' in ty
                    c['no_isnad'] += r['kind'] == 'hadith' and 'isnad' not in ty
                    c['no_matn'] += r['kind'] == 'hadith' and 'matn' not in ty
                    c['multi_isnad'] += ty.count('isnad') > 1
            json.dump(data, open(os.path.join(dst, name), 'w', encoding='utf-8'), ensure_ascii=False)
        print(slug, dict(stats.get(slug, {})), flush=True)
    return stats


def main(argv):
    sys.stdout.reconfigure(encoding='utf-8')
    if not argv or argv[0] not in ('--eval', '--show', '--write', '--remarks'):
        print(__doc__)
        return
    mode, rest = argv[0], argv[1:]
    if mode == '--eval':
        part = 'tune'
        if rest[:1] == ['--part']:
            part, rest = rest[1], rest[2:]
        kw = {}
        if rest[:1] == ['--mode']:
            kw['mode'] = rest[1]
            rest = rest[2:]
        for slug in rest or LINKED:
            st = evaluate(slug, part, **kw)
            print(fmt_eval(slug, st))
            errs = st['errs']
            print('   first errors:', errs[:12])
    elif mode == '--remarks':
        for slug in rest or sorted(os.path.basename(p) for p in glob.glob(os.path.join(DATA, '*')) if os.path.isdir(p) and os.path.basename(p) != 'dump'):
            print(remark_report(slug), flush=True)
    elif mode == '--show':
        show(rest[0], int(rest[1]), int(rest[2]) if len(rest) > 2 else 1)
    else:
        out = rest[0]
        slugs = rest[1:] or sorted(os.path.basename(p) for p in glob.glob(os.path.join(DATA, '*'))
                                   if os.path.isdir(p) and os.path.basename(p) != 'dump')
        write_all(out, slugs)


if __name__ == '__main__':
    main(sys.argv[1:])
