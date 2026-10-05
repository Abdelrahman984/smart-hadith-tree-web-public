"""Compilers named by a bare word in Tahdhib al-Kamal's student lists.

Al-Mizzi lists a shaykh's students and writes the compilers of the Six Books as plain words:
«روى عنه: البخاري، ومسلم، وأبو داود، والترمذي، والنسائي، وابن ماجه» (352 times «النسائي»,
244 «ابن ماجه», ...). The strict linker drops them: a bare nisba matches several narrators and
the compilers' own entries have no shaykh list to confirm it (Tahdhib gives none for al-Nasa'i
and Ibn Majah, so they had 11 and 1 teachers). Here the word is mapped to the compiler's entry.

Only student lists ("talamidh") are read: a bare «أبو داود» in a shaykh list can be al-Tayalisi.
«أبو داود» is the one name another narrator shares in the same lists (al-Tayalisi), so there the shaykh's
own book symbols must include «د»; the other five names are unambiguous. Quotes («النسائي: ثقة») are
not relations.

Used by pipeline.py (build_registry); `python compiler_items.py` runs the self-test.
"""
import re

# key -> (regex for the compiler's own entry header in Tahdhib, symbols a shaykh of his carries)
COMPILERS = {
    'bukhari': (r'^محمد بن إسماعيل بن إبراهيم بن المغيرة', {'خ', 'بخ', 'خت', 'عخ', 'ي', 'ز'}),
    'muslim': (r'^مسلم بن الحجاج بن مسلم', {'م'}),
    'abudawud': (r'^سليمان بن الأشعث بن شداد', {'د', 'مد', 'خد', 'كد', 'فد'}),
    'tirmidhi': (r'^محمد بن عيسى بن سورة', {'ت', 'تم'}),
    'nasai': (r'^أحمد بن شعيب بن علي', {'س', 'سي', 'عس', 'ص', 'كن'}),
    'ibnmajah': (r'^محمد بن يزيد (?:القزويني|الربعي)', {'ق', 'فق'}),
}

SIX = ('bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'ibnmajah')
SUNAN = ('abudawud', 'tirmidhi', 'nasai', 'ibnmajah')

_DIACRITICS = re.compile(r'[ً-ْٰـ]')


def _norm(s: str) -> str:
    s = _DIACRITICS.sub('', s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    return re.sub(r'\s+', ' ', s).strip()


# A compiler's name, as it is written in the lists.
_NAMES = {
    'bukhari': r'البخاري',
    'muslim': r'مسلم',
    'abudawud': r'ابو داود',
    'tirmidhi': r'الترمذي',
    'nasai': r'النسائي',
    'ibnmajah': r'ابن ماجه',
}
_NAME_TO_KEY = {_norm(v): k for k, v in _NAMES.items()}

# What may follow the bare name: where he took it from («في "الأدب"»), how often («حديثا واحدا»),
# or how («تعليقا», «مقرونا بغيره»). A colon starts a critic's quote, which is not a relation.
_TAIL = r'(?:\s+(?:في|تعليقا|مقرونا|حديثا|حديث|وهو|وروى)\b[^:]*)?'
_SINGLE = re.compile(rf'^({"|".join(map(re.escape, _NAME_TO_KEY))}){_TAIL}$')
# «الجماعة» (all six), «الجماعة سوى ابن ماجه», «الأربعة» (the four Sunan), «الستة».
_GROUP = re.compile(r'^(الجماعه|السته|الاربعه|الخمسه)(?:\s+سوي\s+(.+?))?$')


def compilers_named(name: str) -> tuple[str, ...]:
    """The compilers an item of a student list names, or () when it names someone else."""
    s = _norm(name)
    if ':' in s or '؛' in s:
        return ()
    m = _SINGLE.match(s)
    if m:
        return (_NAME_TO_KEY[m.group(1)],)
    m = _GROUP.match(s)
    if m:
        kind, rest = m.group(1), m.group(2)
        base = SUNAN if kind == 'الاربعه' else SIX
        if kind == 'الخمسه' and not rest:
            return ()                       # «الخمسة» is five of the six, which five is not stated
        if rest:
            out = {_NAME_TO_KEY.get(_norm(w)) for w in re.split(r'\sو|\s*،\s*', rest)}
            if None in out:
                return ()
            base = tuple(k for k in base if k not in out)
        return tuple(base)
    return ()


def compiler_entries(entries, n_tahdhib: int) -> dict[str, int]:
    """key -> index of the compiler's entry among the first `n_tahdhib` entries (Tahdhib's own)."""
    found = {}
    for key, (header, _) in COMPILERS.items():
        hits = [i for i in range(n_tahdhib) if re.match(header, entries[i]['header'])]
        if len(hits) == 1:
            found[key] = hits[0]
    return found


def carries_symbol(raw_symbols: str, key: str) -> bool:
    """Whether a shaykh's own symbols («خ م دت س») say that this compiler narrates from him.

    Tahdhib joins symbols without spaces («دس» = د + س, «دق» = د + ق), so a letter counts when it
    occurs in any symbol; «ع» is the six books, «٤» the four Sunan, «٣» د ت س."""
    letters = {'bukhari': 'خ', 'muslim': 'م', 'abudawud': 'د', 'tirmidhi': 'ت', 'nasai': 'س', 'ibnmajah': 'ق'}
    for tok in raw_symbols.split():
        if tok == 'ع' or (tok == '٤' and key in SUNAN) or (tok == '٣' and key in ('abudawud', 'tirmidhi', 'nasai')):
            return True
        if letters[key] in tok:
            return True
    return False


def _selftest():
    assert compilers_named('النسائي') == ('nasai',)
    assert compilers_named('ابن ماجه') == ('ibnmajah',)
    assert compilers_named('ابن ماجة') == ('ibnmajah',)
    assert compilers_named('أبو داود') == ('abudawud',)
    assert compilers_named('البخاري تعليقا') == ('bukhari',)
    assert compilers_named('النسائي في "اليوم والليلة"') == ('nasai',)
    assert compilers_named('ابن ماجه حديثا واحدا') == ('ibnmajah',)
    assert compilers_named('البخاري في كتاب"الأدب"') == ('bukhari',)
    assert compilers_named('النسائي: ثقة') == ()                   # a critic's quote
    assert compilers_named('أبو داود الطيالسي') == ()              # someone else
    assert compilers_named('أبو داود سليمان بن سيف الحراني') == ()
    assert compilers_named('مسلم بن إبراهيم') == ()
    assert compilers_named('مسلم البطين') == ()
    assert compilers_named('النسائي وهو من أقرانه') == ('nasai',)
    assert set(compilers_named('الجماعة')) == set(SIX)
    assert set(compilers_named('الجماعة سوى البخاري')) == set(SIX) - {'bukhari'}
    assert set(compilers_named('الجماعة سوى ابن ماجه')) == set(SIX) - {'ibnmajah'}
    assert set(compilers_named('الأربعة')) == set(SUNAN)
    assert compilers_named('رجل') == ()
    assert carries_symbol('خ م دت س', 'abudawud') and carries_symbol('خ دس', 'nasai') and carries_symbol('ع', 'ibnmajah')
    assert not carries_symbol('ت ق', 'nasai') and not carries_symbol('', 'abudawud') and not carries_symbol('٤', 'muslim')
    print('compiler_items: ok')


if __name__ == '__main__':
    _selftest()
