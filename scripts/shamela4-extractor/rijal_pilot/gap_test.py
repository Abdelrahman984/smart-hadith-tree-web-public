"""Measure how much of a book's isnads the Tahdhib/Taqrib registry covers.

The compiler need not be in the registry: each chain is walked from the compiler's shaykh
down, a name is looked up among the previous narrator's shuyukh when
that narrator is known, and globally otherwise.
Usage: python gap_test.py tahdhib.json book_dir [sample]
"""
import glob
import json
import random
import re
import sys
from collections import Counter, defaultdict

sys.argv, (TAHDHIB, BOOK_DIR, *rest) = [sys.argv[0], sys.argv[1]], sys.argv[1:]
SAMPLE = int(rest[0]) if rest else 1000
COMPILER = rest[1] if len(rest) > 1 else None      # header prefix of the compiler's entry, if any
exec(open(__file__.replace('gap_test.py', 'link_tahdhib.py'), encoding='utf-8').read().split('stats = {k')[0])

def _shuyukh_of() -> list[set[int]]:
    out = []
    for i, e in enumerate(entries):
        s = set()
        for it in e['shuyukh']:
            c = set(symbol_filter(set(candidates(it['name'])) - {i}, it['symbols']))
            if it.get('note') != 'short' or len(c) == 1:    # "نافع" in تعجيل المنفعة: only if unique
                s |= c
        out.append(s)
    return out


shuyukh_of = cached('shuyukh_of', _shuyukh_of)      # the slow part of a run: ~35k list names

# Later books abbreviate the transmission verbs: ثنا، نا، أنا، أنبأ.
# «قرئ على X وأنا أسمع», «قرأت على مالك» (the Muwatta): reading to the shaykh is a transmission formula.
# «سمعت قتادة يحدث عن عطاء»: the verb after a name must not stay in it. («أنه» is not a verb here: what follows it
# is usually the matn, and as a segment of its own it was read as a name; see `clean_segment`.)
VERBS = (r'(?:^|\s|،)و?(?:حدثناه|أخبرناه|أنبأناه|حدثنا|حدثني|حدثه|أخبرنا|أخبرني|أخبره|أنبأنا|أنبأني|أنبأ'
         r'|ثنا|نا|أنا|أبنا|قرئ على|قرأت على|قرأنا على|كنت أسمع|أسمع|سمعت|سمع|يحدثنا|يحدثه|يحدث|حدث'
         r'|عن|قال|قالت|أن|يقول)(?=\s|،|:)')
HARAKAT_RE = re.compile(r'[ً-ْٰـ]')
# Al-Bayhaqi's edition writes the verbs with a final alef maqsura («أخبرنى», «حدثنى»): VERBS knows only the ya forms, so
# «حدثنا يحيى، أخبرنى محمد» stayed one segment and Muhammad was lost (Sunan al-Kubra 15101). The verbs are made ya forms.
VERB_MAQSURA = re.compile(r'(?<![^\s،.:;(\[])(و?(?:حدثن|أخبرن|اخبرن|أنبأن|انبأن))ى(?=[\s،.:;)\]])')


def normalize_verbs(text: str) -> str:
    return VERB_MAQSURA.sub(lambda m: m.group(1) + 'ي', text)


# Clean-up of a narrator segment: honorifics before the name, "ببغداد"/"بمكة" and "إملاء"
# after it, footnote markers, and "X ويحيى بن ..., قالا" (two shaykhs: keep the first).
HONORIFIC = re.compile(r'^(?:الشيخ|الإمام|الأستاذ|القاضي|الحافظ|الفقيه|الأديب)\s+')
# "ب" + a place ("ببغداد", "بنيسابور") or "به"; never a name after a nasab word: "بن بكر", "بن بكير",
# "بن بشار", "أبي بردة" lost their last word (over 7,000 times in Bukhari and al-Bayhaqi alone).
TRAILER = re.compile(r'(?:(?<!بن)(?<!بنت)(?<!أبي)(?<!أبو)(?<!أبا)(?<!أبى)(?<!أم)\s+'
                     r'(?:ب(?:بغداد|مكة|مرو|الكوفة|البصرة|نيسابور|الري|همذان|بخارى|\S+)|إملاء|قراءة عليه'
                     r'|من أصل كتابه|في آخرين|وغيره|قالا|قالوا))+\s*$')
# Hadith records sometimes start with the previous hadith's verdict and number:
# "هذا حديث صحيح ... ولم يخرجاه. ٣٧٦٣ - حدثنا ..." -> keep what follows the last "N -".
PREVIOUS_TAIL = re.compile(r'^.*(?:يخرجاه|يخرجه|الإسناد|الشيخين|شرط مسلم|شرط البخاري)[^٠-٩]{0,40}[٠-٩]+\s*م?\s*-\s*', re.S)
# Some extracted books (al-Sunan al-Kubra of al-Bayhaqi) have shifted record boundaries: a record
# holds a matn and then "١٠٨٣٨ - أخبرنا ..." — the next isnad. Start from the last such marker.
NUMBERED_ISNAD = re.compile(r'[٠-٩]+\s*م?\s*-\s*(?=و?(?:أخبرنا|أخبرني|حدثنا|حدثني|أنبأنا|أنبأ|ثنا|أنا))')
NOT_A_NARRATOR_START = ('قال', 'يقول', 'ورواه', 'رواه', 'وقد', 'ومنها', 'أخرجه', 'وأخرجه', 'تابعه', 'وكذلك')


# «عائشة زوج النبي ﷺ» (no comma in the Muwatta of al-A'zami) ends the name like an honorific.
AFTER_NAME = re.compile(r'\s*(?:رض[يى] الله (?:عنه|عنها|عنهما|عنهم)|عليه السلام|﵁|﵂|﵄|﵃|﵀|زوج(?: النبي| رسول الله)?(?=\s|$))')
# A comma inside a segment ends the name unless the nasab goes on ("عبد الملك، بن أبي بكر"):
# "عائشة، زوج النبي", "ابن جريج، أخبرهم", "أبي، ح" (tahwil).
# "وهو / يعني" go on to identify him ("هشام، يعني ابن حسان", "سفيان، يعني الثوري"), not to describe
# him ("المغيرة بن أبي بردة، وهو من بني عبد الدار").
# "يعني" / "يعنون" (Muslim: "إسماعيل، يعنون ابن جعفر") / "يعنيان".
_YANI = r'(?:يعني|يعنى|يعنون|يعنيان)'
NAME_GOES_ON = re.compile(r'\s*(?:(?:بن|ابن|بنت|أبو|أبي|أبا)\s'
                          rf'|(?:وهو|{_YANI}|هو)\s+(?!(?:من|في|أخو|أحد|مولى|صاحب|ثقة|الذي|كان)\s))')


# «ز-» (Abu Awanah's marker of the editor's additions) is a mark, not a narrator.
ADDITION_MARK = re.compile(r'(?:^|\s)ز\s*-(?=\s|$)')
# Where the isnad ends: the Prophet ﷺ is named («النبى» with a final alef maqsura too), or, in the Muwatta, «مالك أنه بلغه
# أن…» (it reached him: no more narrators). «بلغه عن سعيد بن المسيب» still names one, so only «بلغه أن» ends it.
ISNAD_END = re.compile(r'رسول الله|نبي الله|نبى الله|النبي|النبى|ﷺ|صلى الله عليه وسلم|(?<=\s)بلغ(?:ه|ني)(?=\s+أن\s)')


def clean_segment(s: str) -> str:
    s = ADDITION_MARK.sub(' ', s)
    # Footnote marks "(٣)", "(¬٣)" and, in Musnad Ahmad, "(1)" ("ابن أبي أنيس (1) ، عن أبيه").
    s = re.sub(r'\s+', ' ', re.sub(r'\(¬?[٠-٩0-9]+\)', ' ', s)).strip()
    s = s.replace(' , ', '، ').replace(',', '،').replace('؛', '،')   # «مالك ؛ أنه» (al-A'zami's Muwatta)
    s = re.sub(r'\s+-\s*|\s*-\s+', ' ', s)              # "إسماعيل، - وهو ابن علية -"
    s = AFTER_NAME.split(s)[0]
    if '،' in s:
        head, tail = s.split('،', 1)
        if head.strip() and not NAME_GOES_ON.match(tail + ' '):
            s = head
    s = re.sub(r'\s+ح\s*$', '', s.strip(' ،:.'))       # "أبي ح": tahwil mark
    s = re.sub(r'\s+(?:أنه\s+)?بلغ(?:ه|ني)$', '', s)           # "مالك أنه بلغه" (a narrator follows: "بلغه عن X")
    s = re.sub(r'\s+(?:أنه|أنها)$', '', s)               # "عطاء أنه" (a verb follows: "أنه قال")
    # "إسماعيل، وهو ابن علية", "سعيد هو ابن أبي سعيد": the "ابن" part completes the name.
    if m := re.match(rf'(\S+(?:\s\S+)?)،?\s+(?:وهو|{_YANI}|هو)\s+(ابن\s.+)', s):
        s = f"{m.group(1).rstrip('،')} {m.group(2)}"
    # "سعيد هو المقبري", "أبي معاذ هو عطاء بن أبي ميمونة": the part after "هو" identifies the narrator.
    elif re.search(rf'\s(?:هو|{_YANI})\s', s):          # also "سفيان، يعني الثوري"
        s = re.split(rf'\s(?:هو|{_YANI})\s', s, maxsplit=1)[1]
    # "علي بن حمشاذ ويحيى بن محمد" -> the first shaykh. The text is stripped first so that a
    # name that itself starts with و ("وهب بن جرير") is not taken for a conjunction.
    s = re.split(r'\s+و(?=\S+ بن |أب[يوى]\s)', s)[0]   # also "سعيد وأبى سلمة"
    s = HONORIFIC.sub('', s.strip())
    s = TRAILER.sub('', s.strip(' ،,:.'))                 # "مسدد قالا:" -> "مسدد"
    s = re.sub(r'\s+', ' ', s).strip(' ،,:.')
    # Accusative after "سمعت" / "أن": "أبا هريرة" -> "أبي هريرة", "جابرا" -> "جابر".
    s = re.sub(r'^أبا(?=\s)', 'أبي', s)
    if re.fullmatch(r'\S{3,}ا', s) and norm(s).strip() not in isms and norm(s[:-1]).strip() in isms:
        s = s[:-1]
    return s


def chain_segments_verbs(arabic: str) -> list[tuple[str, list[str]]]:
    """Each name segment with the transmission verbs that follow it, up to the next kept segment:
    "الأعمش، عن إبراهيم" gives ("الأعمش", ["عن"]), the formula al-A'mash used for his shaykh."""
    # Invisible direction marks around punctuation ("قال‏:‏" in al-Adab al-Mufrad) hide the verbs.
    t = normalize_verbs(re.sub('[‌-‏‪-‮﻿]', '', HARAKAT_RE.sub('', arabic)))
    t = PREVIOUS_TAIL.sub('', t)
    marks = list(NUMBERED_ISNAD.finditer(t))
    if marks:
        t = t[marks[-1].end():]
    t = re.split(ISNAD_END, t)[0]
    # An editor's [..] or (..) inside the isnad restores a narrator the manuscript dropped («عن زيد (عن أبي سلام) عن
    # أبي مالك», «[حدثنا أبو داود، حدثنا]»): keep what is in it, drop the brackets that glued it to the names.
    t = re.sub(r'\s+', ' ', re.sub(r'\(¬?[٠-٩0-9]+\)', ' ', t))     # footnote marks first: (١) is not an insertion
    t = re.sub(r'[\[\]()]', ' ', t)
    parts = re.split(f'({VERBS})', t)                   # segment, verb, segment, verb, ...
    pairs: list[tuple[str, list[str]]] = []
    prev = None                                          # the previous segment shaped like one
    for k in range(0, len(parts), 2):
        s = clean_segment(parts[k])
        verb = re.sub(r'^[\s،]*و?', '', parts[k + 1]) if k + 1 < len(parts) else None
        if 1 < len(s) < 70 and not s.startswith(NOT_A_NARRATOR_START):
            # Muslim's speaker tags: "محمد بن المثنى وابن بشار، قال ابن المثنى: حدثنا محمد بن جعفر" — "ابن X"
            # right after a narrator whose father is X repeats him; it is not a new link.
            if not (prev is not None and (m := re.fullmatch(r'ابن (\S+)', s))
                    and re.match(rf'(?:عبد )?\S+ بن {re.escape(m.group(1))}(?:\s|$)', prev)):
                pairs.append((s, []))
            prev = s
        if verb and pairs:
            pairs[-1][1].append(verb)
    return pairs


def chain_segments(arabic: str) -> list[str]:
    return [s for s, _ in chain_segments_verbs(arabic)]


# A segment that is not shaped like a name (verdicts, matn, numbers) is reported apart from real misses.
NOT_NAME = re.compile(r'[٠-٩0-9"«»﴿﴾؟?!]|(?:^|\s)هذا(?:\s|$)|يخرجاه|صحيح|ﷺ|﷿|رضي|فقال|كان|لما|قلت|إذا|كنا|شاهده')


def is_name(seg: str) -> bool:
    if re.fullmatch(r'(?:أبو|أبي|أبا|ابن|بن|أم)', seg.strip()):
        return False                                     # a kunya/ibn cut off from its name
    return not NOT_NAME.search(seg) and len(seg.split()) <= 9 and (
        bool(re.search(r'(?:^|\s)(?:بن|ابن|أبو|أبي|أبا|بنت|أم)(?:\s|$)', seg)) or len(seg.split()) <= 3)


def lookup(seg: str, prev: int | None) -> set[int]:
    if prev is not None and re.fullmatch(r'أبيه|ابيه', seg):
        father = tokens(entries[prev]['header'][:80])[1:2]
        return {j for j in shuyukh_of[prev] if ism[j] == father}
    return set(candidates(seg)) - ({prev} if prev is not None else set())


# Several entries can share the prefix (al-Hakim also has an empty duplicate from السلسبيل): the one
# with the most lists is the compiler, since his shaykh list gives the chain's first link.
compiler = max((i for i, e in enumerate(entries) if COMPILER and e['header'].startswith(COMPILER) and not e.get('fallback')),
               key=lambda i: len(entries[i]['shuyukh']) + len(entries[i]['talamidh']), default=None)
print('compiler entry:', entries[compiler]['header'][:60] if compiler is not None else None)

hadiths = []
for f in glob.glob(f'{BOOK_DIR}/*.json'):
    if not f.endswith('index.json'):
        data = json.load(open(f, encoding='utf-8'))      # Itqan books also hold non-hadith files
        hadiths += [h['arabic'] for h in (data if isinstance(data, list) else [])
                    if isinstance(h, dict) and h.get('arabic')]
random.seed(1)
sample = random.sample(hadiths, min(SAMPLE, len(hadiths)))

MAX_DEPTH = 8
by_depth = defaultdict(Counter)
missing_names = Counter()
entered_at = Counter()
noise = 0
for text in sample:
    all_segs = chain_segments(text)
    segs = [s for s in all_segs if is_name(s)][:MAX_DEPTH]
    noise += len(all_segs) - len([s for s in all_segs if is_name(s)])
    prev, entered = compiler, None
    for depth, seg in enumerate(segs):
        c = lookup(seg, prev)
        within = c & shuyukh_of[prev] if prev is not None else set()
        if len(within) > 1 and depth + 1 < len(segs):
            within = {j for j in within if lookup(segs[depth + 1], j) & shuyukh_of[j]} or within
        if len(within) > 1 and (p := fame_pick(within)) is not None:
            within = {p}
        if len(within) == 1:
            status, prev = 'resolved (teacher list)', next(iter(within))
        elif len(c) == 1:
            status, prev = 'resolved (unique name)', next(iter(c))
        elif c:
            status, prev = 'ambiguous', None
        else:
            status, prev = 'not in registry', None
            missing_names[re.sub(r'\s+', ' ', seg)] += 1
        by_depth[depth][status] += 1
        if status.startswith('resolved') and entered is None:
            entered = depth
    entered_at['never' if entered is None else entered] += 1

print(f'hadiths: {len(sample)} of {len(hadiths)}  segments dropped as non-names (segmentation noise): {noise}')
statuses = ['resolved (teacher list)', 'resolved (unique name)', 'ambiguous', 'not in registry']
print(f'{"depth":>5} {"names":>6}  ' + '  '.join(f'{s[:22]:>22}' for s in statuses))
for d in sorted(by_depth):
    total = sum(by_depth[d].values())
    print(f'{d:>5} {total:>6}  ' + '  '.join(f'{by_depth[d][s] / total:>22.0%}' for s in statuses))
allc = Counter()
for c in by_depth.values():
    allc.update(c)
total = sum(allc.values())
print('all depths:', ', '.join(f'{s}: {allc[s] / total:.1%}' for s in statuses))
print('first resolved narrator at depth:', sorted(entered_at.items(), key=lambda x: (x[0] == 'never', x[0])))
top = missing_names.most_common(25)
print(f'distinct missing names: {len(missing_names)}; the 25 most frequent cover '
      f'{sum(n for _, n in top) / max(1, sum(missing_names.values())):.0%} of misses:')
for name, n in top:
    print(f'   {n:4}  {name[:70]}')

json.dump({'missing': missing_names, 'by_depth': {d: dict(c) for d, c in by_depth.items()}},
          open('gap_result.json', 'w', encoding='utf-8'), ensure_ascii=False)
