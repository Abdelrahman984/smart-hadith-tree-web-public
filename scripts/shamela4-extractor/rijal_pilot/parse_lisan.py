"""Parse Lisan al-Mizan (Shamela 4 book 36357) into entries shaped like tahdhib.json.

Usage: python parse_lisan.py <dump_dir> <out_json>. First draft written by a sub-agent; parse_shaykh_books.py
reads its output (layout 'lisan'), cuts each header to the name and skips Tahdhib narrators.

Layout: every entry head is a <span data-type='title'> "N - [symbols -] name[, عن شيخه]," where the
symbols are ز (Ibn Hajar's addition) / ذ / ص / ك / صح. Unnumbered "* -<span> ز - name، في X [N]" heads are
cross-reference stubs. Entry text: Dhahabi's Mizan text (ends with "انتهى"), then Ibn Hajar's "قلت:" notes.
"""
import json, re, sys
from collections import Counter

SRC = f'{sys.argv[1]}/36357_pages.tsv'
OUT = sys.argv[2]
AR = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
HARAKAT = re.compile('[\u064B-\u0652\u0670\u0640]')
NL = '\n'


def strip_h(s): return HARAKAT.sub('', s)


def normalize(s):
    s = strip_h(s)
    s = re.sub('[أإآ]', 'ا', s).replace('ى', 'ي').replace('ة', 'ه')
    return re.sub(r'\s+', ' ', s).strip()


rows = []
for line in open(SRC, encoding='utf-8'):
    line = line.rstrip('\r\n')
    if '\t' not in line: continue
    pid, body = line.split('\t', 1)
    rows.append((int(pid.split('-')[1]), body.replace('\\n', NL)))
rows.sort()
parts = []
for k, (_, b) in enumerate(rows):
    if k:
        prev_end = parts[-1].rstrip(' ')[-1:] if parts[-1].strip() else ''
        # a page that breaks mid-sentence: re-join with a space so lists/headers are not cut in two
        glue = NL if (prev_end in ('', '.', ':', '؟', ')', NL, '،'.replace('،', '.')) or re.match(r'\s*(?:\*|[٠-٩]+\s*(?:مكرر)?\s*-|<span)', b)) else ' '
        parts.append(glue)
    parts.append(b)
text = ''.join(parts)
text = re.sub(r'\s*\(\s*¬?\s*[٠-٩]+\s*\)', '', text)       # footnote marks (¬١) / (١)
text = re.sub('[​-‏‪-‮]', '', strip_h(text))

SPAN = re.compile(r"(\*?\s*-?\s*)<span data-type='title' id=toc-\d+>(.*?)</span>", re.S)
HEAD = re.compile(r'^\s*\[?\s*(?:(\d+)(?:\s*مكرر|\s*و\s*\d+)?\s*-\s*\[?\s*)?((?:(?:ز|ذ|ص|ك|صح|مكرر)(?:\s+|(?=-)))*)-?\s*(.*)$', re.S)

spans = []
for m in SPAN.finditer(text):
    inner = m.group(2).strip()
    star = '*' in m.group(1)
    h = HEAD.match(inner.translate(AR))
    num = int(h.group(1)) if h.group(1) else None
    syms = h.group(2).strip()
    if num is not None and re.match(r'\s*\[?\s*\d+\s*مكرر', inner.translate(AR)): syms = (syms + ' مكرر').strip()
    if num is None and not star:          # chapter / letter / "من اسمه" markers: boundaries only
        spans.append((m.start(), m.end(), None, None, None, None)); continue
    if num is None and not syms and not re.search(r'[ء-ي]', h.group(3)):
        spans.append((m.start(), m.end(), None, None, None, None)); continue
    spans.append((m.start(), m.end(), num, syms, h.group(3).strip(), star))

# ---- list splitting -------------------------------------------------------------------------
W_NAMES = ('وكيع', 'وهب', 'واصل', 'وهيب', 'وائل', 'واقد', 'وردان', 'وبرة', 'وضاح', 'ورقاء', 'وعلة', 'والبة',
           'وثيمة', 'وفد', 'ورد', 'وبر', 'وازع', 'وابصة', 'وائلة', 'واثلة', 'ودان', 'وراد', 'وقاص', 'وليد')
STOP_START = re.compile(
    r'^(?:و?(?:بصري|كوفي|مصري|شامي|مدني|مكي|بغدادي|واه|يمني|حجازي|خراساني|واسطي|أثنى|قال|ذكر|ذكره|ضعف|وثق|لا|ليس|لم|مجهول|متروك|كذاب|ضعيف|صدوق|ثقة|منكر|له|لها|مات|قد|ما|في|من|هو|هي|'
    r'قاله|حدث|روى|روت|عنه|عنها|يقال|قيل|انتهى|كان|فيه|حديث|'
    r'وغيره|وغيرهم|وجماعة|جماعة|وآخر|وآخرون|بحديث|بخبر|بأحاديث|بعد|ثم|إن|أنه|أن|بل|فهو|فهذا|هذا|'
    r'يروي|يرويه|نكره|وضع|منهم|أخرج|أخبر|أنبأ|اتهم|رماه|سكت|يعرف)\b|روا)')
BAD_IN = re.compile(r'[«»"٠-٩\d\[\]]|حديث|قال|ذكره|مرفوع|يقول')
NOISE_END = re.compile(r'\s+(?:و?(?:طائفة|جماعة|غيرهم|غيره|آخرون|آخر|آخرين)|في\s+(?:آخرين|جماعة)|رفعه|مرفوعا|بحديث|بخبر|حديثا|أيضا)\b.*$')
BLESS = re.compile('[\ufd40-\ufdff\u0610-\u061a]|\ufdfa|ﷺ')
VERDICT_CUT = re.compile(r'\s(?:و?قال|ضعفه|وثقه|ذكره|وذكره|لا يعرف|لا يدرى|لا أعرف|مجهول|متروك|كذبه|وهو|وهي|ثقة|انتهى|حدث|روى)\b')


def split_items(seg, bare=False):
    """'A، وB، وC: bla' -> ([A, B, C], consumed_chars). Stops at the first chunk that is not a name.
    bare=True ("عن X، عن Y" isnad chain): only the first name is a teacher."""
    seg = seg.replace(NL, ' ')
    end = re.search(r'[.؟](?:\s|$)|\s—\s|…', seg)
    seg = seg[:end.start()] if end else seg
    out, consumed = [], 0
    for i, mm in enumerate(re.finditer(r'(.*?)(?:\s*[،,]\s*|\s+وعن\s+|$)', seg)):
        chunk = mm.group(1)
        if mm.start() >= len(seg): break
        c = chunk.strip()
        stop_after = False
        if bare and i > 0 and re.match(r'^عن\s', c): break
        if ':' in c:
            c = c.split(':')[0]; stop_after = True
        if re.match(r'^و[ء-ي]', c) and not c.startswith(W_NAMES) and i > 0:
            c = c[1:]
        c = re.sub(r'^عن\s+', '', c.strip())
        c = BLESS.sub('', c).strip()
        if not c: break
        if STOP_START.match(c) and not re.match(r'^(?:و?عن)\b', chunk.strip()): break
        words = c.split()
        cut = VERDICT_CUT.search(' ' + c)
        if cut and (i == 0 or len(words) > 3):
            c = c[:max(cut.start() - 1, 0)].strip(); stop_after = True
            if not c: break
        c2 = NOISE_END.sub('', c).strip()
        if c2 != c: stop_after = True
        c = re.sub(r'^(?:أيضا|سوى)\s+', '', c2)
        if len(words) > 12 or BAD_IN.search(c): break
        out.append(c)
        consumed = mm.start() + len(chunk) if stop_after else mm.end()
        if stop_after: break
    return out, consumed


JUNK_ITEMS = {'آخرون', 'آخرين', 'جماعة', 'غيره', 'غيرهم', 'جماعه', 'البصريون', 'الكوفيون', 'أهل البصرة', 'أهل الكوفة'}


LONG_DROPPED = []


def item_dicts(names):
    LONG_DROPPED.extend(n for n in names if len(n) > 60)
    names = [n for n in names if not any(m != n and m.startswith(n + ' ') for m in names)]   # 'الحارث' vs 'الحارث بن مرة'
    return [{'name': strip_h(n), 'symbols': '', 'note': ''} for n in names if 1 < len(n) <= 60 and n not in JUNK_ITEMS]


SHAYKH = re.compile(r'(?:(?<=[\s.،])|^)(?:و?(?:روى|يروي|روت|حدث|حدثت|أخذ|سمع|يروى)\s+(?:عن|من)|وعن|عن)\s+')
TALMEEZ = re.compile(r'(?:(?<=[\s.،])|^)(?:و?(?:روى|حدث|حدثت|أخذ|سمع|روت)\s+عنه?ا?|وعنه?ا?|ورووا عنه|وعنهم|رواه عنه)\s+')
QUOTE = re.compile(r'(?:^|\n|[.،]\s+|\)\s+)و?قال\s+([^:\n]{2,70}?)\s*:\s*')
QUOTE_LAZY = re.compile(r'(?:^|\n)\s*(قلت)\s*:\s*')
NOT_CRITIC = ('رسول الله', 'النبي', 'لي ', 'لنا', 'له ', 'لها', 'لابن', 'لأبي', 'لأحمد', 'لعبد')
EXPL_S = re.compile(r'(?:^|(?<=[\s.]))و?(?:(?:روى|يروي|روت|أخذ)\s+عن|سمع\s+من)\s+')
EXPL_T = re.compile(r'(?:^|(?<=[\s.]))و?(?:روى|حدث|روت|ورووا)\s+عنه?ا?\s+|(?:^|(?<=[\s.]))وعنه?ا?\s+')
SKIP_PARA = re.compile(r'^\s*(?:و?قال|قلت|و?ذكره|وفي|وأورده|وقد|ووقع|و?ذكر|و?رواه|و?له|و?فيه)\b')

SENT_END = re.compile(r'[.؟](?:\s|$)')
CROSS_TAIL = re.compile(
    r'(?:^|[\s،.])(?:(?:هو|هي|وهو|وهي)\s+(?:في\s+)?|(?:في|تقدم في|تقدمت في|يأتي في|سيأتي في|ويأتي في|مضى في|تقدم)\s+(?:ترجمة\s+)?)'
    r'([^.\n\[]{2,120}?)\s*(?:\[\s*([٠-٩0-9…\s]*)\s*\])?\s*\.?\s*$')


def parse_entry(num, syms, headtxt, body, star, suspect):
    paras = [p.strip() for p in body.split(NL) if p.strip()]
    head = re.sub(r'\s+', ' ', headtxt).strip()
    name_part = re.split(r'\s*[،,:]\s*|\s+عن\s+|\s+ويقال\b|\s+يكنى\b|\s+وقيل\b|\s+\(', head)[0]
    name = normalize(name_part)[:200]
    p0 = paras[0] if paras else ''
    first_par = head + ' ' + p0
    first_par = re.sub(r'\s+', ' ', first_par).strip()
    m_end = SENT_END.search(first_par)
    sent1 = first_par[:m_end.start() + 1] if m_end else first_par       # sentence 1 = header
    header = strip_h(sent1)
    if len(header) > 280:
        cut_at = max(header.rfind('،', 0, 280), header.rfind(' ', 0, 280))
        header = header[:cut_at if cut_at > 60 else 280].rstrip(' ،')

    shuyukh, talamidh, seen = [], [], set()
    consumed_end = 0
    rest = sent1[len(name_part):] if sent1.startswith(name_part) else sent1

    def add(lst, names):
        for n in names:
            if n not in seen:
                seen.add(n); lst.append(n)

    # (a) lists in the first paragraph. Bare "عن X" (isnad-like) counts only in the header sentence, before
    #     any "وعنه", and only its first name; explicit "روى عن" / "وعنه" lists count anywhere in the paragraph
    #     unless they sit inside a critic's quote ("قال X: ... روى عنه").
    par_rest = first_par[len(name_part):] if first_par.startswith(name_part) else first_par
    in_quote = lambda src, pos: bool(re.search(r'قال\s[^:.]{0,70}:[^.]{0,200}$', src[max(0, pos - 300):pos]))
    tm_all = [tm for tm in TALMEEZ.finditer(par_rest) if not in_quote(par_rest, tm.start())]
    first_t = tm_all[0].start() if tm_all else len(par_rest)
    used_to, bare_done = 0, False
    for sm in SHAYKH.finditer(par_rest):
        if sm.start() < used_to or in_quote(par_rest, sm.start()): continue
        explicit = bool(re.match(r'\s*و?(?:روى|يروي|روت|حدث|حدثت|أخذ|سمع|يروى)', par_rest[sm.start():sm.end()]))
        if not explicit and (bare_done or sm.start() >= len(rest) or sm.start() >= first_t): continue
        names, used = split_items(par_rest[sm.end():], bare=not explicit)
        if names:
            add(shuyukh, names); used_to = sm.end() + used
            if not explicit: consumed_end = max(consumed_end, used_to); bare_done = True
    t_used = 0
    for tm in tm_all:
        if tm.start() < t_used: continue
        names, used = split_items(par_rest[tm.end():])
        if names:
            add(talamidh, names); t_used = tm.end() + used
            if tm.end() + used <= len(rest): consumed_end = max(consumed_end, t_used)
    # (b) later paragraphs: only explicit "روى عن" / "روى عنه" / "وعنه" paragraphs that are not quotes
    for p in (paras[1:4] if p0 else paras[:3]):
        if SKIP_PARA.match(p) and not re.match(r'^\s*و?(?:روى|حدث)', p): continue
        if len(p) > 600: continue
        for sm in EXPL_S.finditer(p):
            add(shuyukh, split_items(p[sm.end():])[0])
        for tm in EXPL_T.finditer(p):
            add(talamidh, split_items(p[tm.end():])[0])
    shuyukh = [n for n in shuyukh if n not in talamidh]

    # --- quotes: "قال X: ...", "قلت: ..." (Ibn Hajar) + the unattributed lead verdict (Mizan or Ibn Hajar's own)
    quotes, prev = [], None
    qs = sorted(list(QUOTE.finditer(body)) + list(QUOTE_LAZY.finditer(body)), key=lambda m: m.start())
    for k, q in enumerate(qs):
        stop = qs[k + 1].start() if k + 1 < len(qs) else len(body)
        nl = body.find(NL, q.end())
        said = body[q.end():min(stop, nl if nl > 0 else stop)].strip()
        who = re.sub(r'\s+', ' ', q.group(1).strip())
        via = None
        if who == 'قلت':
            critic = 'ابن حجر'
        else:
            mv = re.match(r'^(.*?)\s*،\s*عن\s+(.+)$', who)
            critic, via = (mv.group(2), mv.group(1)) if mv else (who, None)
            critic = re.split(r'\s+(?:في|ومن)\s', critic)[0].strip()
            if (critic + ' ').startswith(NOT_CRITIC) or not said: continue
            if critic in ('أيضا', 'مرة') and prev: critic = prev
        if not critic or len(critic) > 60: continue
        quotes.append({'critic': critic, 'via': via, 'text': said[:1500]})
        prev = critic
    lead = ''
    if consumed_end and consumed_end < len(rest):
        lead = rest[consumed_end:].strip(' ،.:;')
    tail = first_par[len(sent1):].strip() if m_end else ''
    tail = re.split(r'(?:^|\s)و?قال\s[^:]{2,70}:', tail)[0].strip()
    lead = re.sub(r'\s*انتهى\.?\s*$', '', (lead + ' ' + tail).strip()).strip()
    lead = re.split(r'["«]', lead)[0].strip(' ،')
    if lead.endswith(':'): lead = ''
    if len(lead) > 2 and not SHAYKH.match(lead) and not TALMEEZ.match(lead):
        crit = 'ابن حجر' if 'ز' in syms.split() or syms.startswith('ز') else 'الذهبي'
        quotes.insert(0, {'critic': crit, 'via': None, 'text': lead[:600]})

    # --- crossref: stub head, or a short entry whose last sentence is a pointer "في X [N]" / "هو X"
    after = re.sub(r'\s+', ' ', (head.rstrip(' ،,') + ' ' + ' '.join(paras)).strip())[len(name_part):].strip()
    kind, target, tnum, xm = 'entry', None, None, None
    if len(after) < 300 and not re.search(r'قال|ذكره|مجهول|ضعيف|ثقة|كذاب|متروك|لا يعرف', after):
        last = re.split(r'[.؟]\s+', after.rstrip(' .'))[-1]
        xm = CROSS_TAIL.search(last + ' ')
        if xm and not (xm.group(2) is not None or re.search(r'الذي (?:قبله|بعده)|ترجمة', last)
                       or re.search(r'(?:^|[\s،])(?:هو|هي)\s', last)):
            xm = None
    if star:
        kind = 'crossref'
        target = strip_h(xm.group(1)).strip(' .،:') if xm else (strip_h(after.lstrip('، ')).strip(' .،:') or None)
        tnum = xm.group(2) if xm else None
    elif xm and not re.search(r'^كتاب|إلى|وشيخ', xm.group(1)) and len(xm.group(1).split()) <= 8:
        kind = 'crossref'; target = strip_h(xm.group(1)).strip(' .،:'); tnum = xm.group(2)
    e = {
        'kind': kind, 'target': target, 'num': num, 'num_suspect': suspect,
        'symbols': syms, 'source': 'lisan', 'header': header, 'name': name,
        'shuyukh': item_dicts(shuyukh), 'talamidh': item_dicts(talamidh), 'quotes': quotes, 'rawa_lahu': None,
    }
    if kind == 'crossref':
        tn = (tnum or '').translate(AR).strip()
        e['target_num'] = int(tn) if tn.isdigit() else None
    return e


# Entries whose head is plain text (no title span, e.g. at the top of a page): "٧٦ - ز- إبراهيم ...،"
items = [(sp[0], sp[1], sp[2], sp[3], sp[4], sp[5], False) for sp in spans]
span_ranges = [(sp[0], sp[1]) for sp in spans]
cands = []
for m in re.finditer(r'(?:^|\n)[ \t]*(\[?[٠-٩]+(?:\s*مكرر|\s*و\s*[٠-٩]+)?\s*-)', text):
    pos = m.start(1)
    nl = text.find(NL, pos)
    line = text[pos:nl if nl > 0 else len(text)]
    h = HEAD.match(line.translate(AR))
    if not h or not h.group(1): continue
    rem = line[h.start(3):]
    cut = rem.find('،')
    if cut < 0 or cut > 140:
        cut = rem.find('.')
    if cut < 0: cut = len(rem) - 1
    syms = h.group(2).strip()
    if 'مكرر' in line[:20]: syms = (syms + ' مكرر').strip()
    cands.append((pos, pos + h.start(3) + cut + 1, int(h.group(1)), syms, rem[:cut + 1].strip(), False, True))
merged = sorted(items + cands, key=lambda x: x[0])
accepted, last_num = [], 0
for it in merged:
    s0, s1, num, syms, headtxt, star, unspanned = it
    if num is None and not star:
        accepted.append(it); continue                   # boundary marker
    if unspanned:
        if not (num == last_num + 1 or (num == last_num + 2 and syms)): continue
    if num is not None and 'مكرر' not in syms: last_num = num
    accepted.append(it)

entries, last_num = [], 0
for i, (s0, s1, num, syms, headtxt, star, unsp) in enumerate(accepted):
    if num is None and not star: continue
    nxt = accepted[i + 1][0] if i + 1 < len(accepted) else len(text)
    body = re.sub(r'\s*\*?\s*-?\s*$', '', text[s1:nxt])      # drop "* -" glue before the next head
    if num is None:
        suspect, num_i = True, last_num
    else:
        suspect = bool(last_num) and num not in (last_num, last_num + 1)
        num_i = num
        if 'مكرر' in syms: suspect = True
        else: last_num = num
    e = parse_entry(num_i, syms, headtxt, body, bool(star), suspect)
    if unsp: e['unspanned_head'] = True
    entries.append(e)

json.dump(entries, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# ---- stats ----
real = [e for e in entries if e['kind'] == 'entry']
xr = [e for e in entries if e['kind'] == 'crossref']
ordered = [e['num'] for e in entries]
numbered = [e['num'] for e in entries if not (e['kind'] == 'crossref' and e['num_suspect']) and 'مكرر' not in e['symbols']]
print(f'total {len(entries)}  entries {len(real)}  crossrefs {len(xr)}')
print('num range', min(ordered), max(ordered), ' numbered heads:', len(set(numbered)))
print('non-increasing (b<a):', sum(1 for a, b in zip(numbered, numbered[1:]) if b < a),
      ' repeats:', sum(1 for a, b in zip(numbered, numbered[1:]) if b == a),
      ' gaps>1:', sum(1 for a, b in zip(numbered, numbered[1:]) if b > a + 1))
for k in ('shuyukh', 'talamidh', 'quotes'):
    h = sum(1 for e in real if e[k]); t = sum(len(e[k]) for e in real)
    print(f'{k:9} with: {h} ({h/len(real):.0%}) items: {t}')
print('header empty:', sum(1 for e in entries if not e['header']), ' header>300:', sum(1 for e in entries if len(e['header']) > 300))
print('list items >60 chars (dropped):', len(LONG_DROPPED), ' kept items >60:', sum(1 for e in entries for k in ('shuyukh','talamidh') for x in e[k] if len(x['name'])>60), ' >40:', sum(1 for e in entries for k in ('shuyukh','talamidh') for x in e[k] if len(x['name'])>40))
print('symbols:', Counter(e['symbols'] for e in entries).most_common(8))
