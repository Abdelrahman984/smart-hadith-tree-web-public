# -*- coding: utf-8 -*-
"""Parse Ibn Hajar's «تعريف أهل التقديس بمراتب الموصوفين بالتدليس» (Shamela 1186)
and cross-check it against Sibt Ibn al-Ajami's «التبيين لأسماء المدلسين» (Shamela 1187).

Usage:
    parse_mudallisin.py <dump_dir> <out.json> [<tabyin_not_in_tabaqat.json>]

Input  : <dump_dir>/1186_pages.tsv, 1186_titles.tsv, 1186_foot.tsv (and 1187_pages.tsv)
         Each line is "<book>-<page>\t<body>" with literal "\\n" for line breaks.
Output : {"entries": [...], "stats": {...}} in <out.json>.
         If the third argument is given, the 1187 cross-check goes to that file.

Layout notes (verified on the dump):
  * Tier headings are <span data-type="title"> spans: «المرتبة الاولى» ... «المرتبة الخامسة»,
    then «فصل» (closes tier 5), «ملحق ...» (the editor's appendix) and two poems.
  * Entries are "(N) <symbols> <name> <description>" in Arabic-Indic digits. In the tiers they
    often run on inside the same paragraph (e.g. "... يدلس (٦٩) ع حبيب ..."), so the marker is
    searched anywhere, not only at line start.
  * Appendix entries are "(N) k - name / symbols : text" with the editor's own comments (قلت ...)
    on the following lines.
  * The footnotes file only holds real text for the editor's introduction (pages 3-4); for the
    book itself every footnote record is just a "[التعليق] <section>" placeholder. The editor's
    notes sit in the body text, so they are kept in the entry text and, for the appendix, split
    out as "editor_notes".
"""
import bisect
import json
import re
import sys
import unicodedata

sys.stdout.reconfigure(encoding="utf-8")

AR_DIGITS = str.maketrans("٠١٢٣٤٥٦٧٨٩", "0123456789")
HARAKAT = re.compile("[ً-ٰٟـ]")
PAGE_MARK = re.compile("\x03(\\d+)\x03")


def to_int(s):
    return int(s.translate(AR_DIGITS))


def norm(s):
    """Loose normal form for lookups and cross-matching."""
    s = unicodedata.normalize("NFC", s)
    s = HARAKAT.sub("", s)
    s = s.replace("أ", "ا").replace("إ", "ا").replace("آ", "ا").replace("ٱ", "ا")
    s = s.replace("ى", "ي").replace("ة", "ه").replace("ؤ", "و").replace("ئ", "ي")
    return s


# --------------------------------------------------------------------------- loading
def load_tsv(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f.read().split("\n"):
            if "\t" not in line:
                continue
            key, body = line.split("\t", 1)
            try:
                page = int(key.rsplit("-", 1)[1])
            except ValueError:
                continue
            rows.append((page, body.replace("\\n", "\n")))
    return rows


TITLE_SPAN = re.compile(r"<span[^>]*data-type=[\"']title[\"'][^>]*>(.*?)</span>", re.S)
OTHER_TAG = re.compile(r"<[^>]+>")


def build_stream(rows):
    """One string for the whole book: title spans become \\x00text\\x01, pages \\x03n\\x03."""
    parts, starts = [], []
    pos = 0
    for page, body in rows:
        body = TITLE_SPAN.sub(lambda m: "\x00" + OTHER_TAG.sub("", m.group(1)) + "\x01", body)
        body = OTHER_TAG.sub("", body)
        chunk = "\x03%d\x03" % page + body + "\n"
        starts.append((pos, page))
        parts.append(chunk)
        pos += len(chunk)
    return "".join(parts), starts


def page_at(starts, offsets, pos):
    i = bisect.bisect_right(offsets, pos) - 1
    return starts[max(i, 0)][1]


# --------------------------------------------------------------------------- name grammar
SYMS = set("ع خ م د ت س ق ٤ 4 خت بخ عخ ز ى ي مد صد خد قد ف ل كد تم سى عس كن فق ح ن مق مقرونا "
           "تعليقا ر سي ص كد عق".split())
SYMS = {norm(x) for x in SYMS}

KUNYA_HEADS = {"ابو", "ابي", "ابا", "ام"}
ISM_JOIN = {"عبد", "عبيد"}          # "عبد الله", "عبيد الله" are two tokens, one name unit
BIN = {"بن", "ابن", "بنت"}
# words that start a description; checked on the normalised token
STOP = {norm(x) for x in """
تابعي مشهور مشهورا الشهير ثقه صدوق وصفه وصف وصفوه وصفوا كان وكان قال وقال ذكره ذكر روي يروي يروى روى
عن وعنه احد له لقبه وهو هو وهي في من صاحب نزيل قاضي قارئ شيخ محدث عالم اخو اصله اكثر موصوف مختلف
اختلف فيه ضعفه ضعيف ضعفوه اشار اتهمه تكلموا مقبول صح قلت مكثر حافظ روت ثم يقال لم ولم وقد وكذا
بمهمله بمعجمات بالقاف بمثناه بفتح بالتصغير بالزاي بالمهمله بالنون بالباء بمثلثه بالمعجمه بضم
بالتحتانيه بكسر بتشديد بالموحده مولاهم مولى وثقه اتفقوا الراوي بصري كوفي دمشقي راوي راي فقيه متفق معروف قي
""".split()}
TITLES = {norm(x) for x in """
الحافظ الامام الفقيه المقرئ الكاتب القاضي الاخباري العابد المفسر الراوي المحدث المشهور الثقه الشهير
التابعي الاثبات الاعلام المؤذن الزاهد العلامه
""".split()}
# definite-article words that are really descriptions, never nisbas
NOT_NISBA = TITLES | {norm(x) for x in "الذي التي المعروف المذكور المكثر الصدوق الضعيف".split()}
MAX_TOKENS = 14


def read_unit(t, i):
    """One name unit starting at t[i]: a word, 'عبد X', or 'أبو X' (with 'أبو عبد الله')."""
    n = len(t)
    h = norm(t[i])
    end = i + 1
    if (h in ISM_JOIN or h in KUNYA_HEADS) and end < n:
        end += 1
        if h in KUNYA_HEADS and norm(t[end - 1]) in ISM_JOIN and end < n:
            end += 1
    return end


def clean_tok(tok):
    return tok.strip("()[]«»\"'،,.:;/-")


def parse_name(tokens, kind="tier"):
    """Return (name, flags, rest_index). Grammar: ISM (بن X)* (NISBA|LAQAB)* (KUNYA)?"""
    t = [x for x in tokens[:MAX_TOKENS + 4]]
    flags = []
    skipped = set()
    n = len(t)
    if n == 0:
        return "", ["empty"], 0
    out_end = read_unit(t, 0)
    j = out_end
    stop_known = None
    while j < n and j < MAX_TOKENS:
        raw = t[j]
        h = norm(clean_tok(raw))
        if h in STOP and h not in BIN:
            stop_known = True
            break
        if raw != clean_tok(raw) and raw.rstrip().endswith((":", ".", "/", "،", ",")) and not raw.startswith("("):
            # punctuation after the token closes the name (it is still part of the name)
            out_end = j + 1
            stop_known = True
            break
        if h in BIN:
            if j + 1 >= n or norm(clean_tok(t[j + 1])) in STOP:
                break
            j = read_unit(t, j + 1)
            if norm(t[j - 1]) in KUNYA_HEADS:  # "بن أبي" with no following word read
                pass
            out_end = j
            continue
        if h in TITLES:
            if j + 1 < n and norm(t[j + 1]) in KUNYA_HEADS:
                flags.append("title_skipped:" + raw)
                skipped.add(j)
                j += 1
                continue
            stop_known = True
            break
        if h == "ثم" and j + 1 < n and norm(t[j + 1]).startswith("ال") and norm(t[j + 1]).endswith("ي"):
            j += 2
            out_end = j
            continue
        if h in STOP:
            stop_known = True
            break
        if h in KUNYA_HEADS:
            j = read_unit(t, j)
            out_end = j
            continue
        if h.startswith("ابو") and len(h) > 5:  # kunya written without a space, e.g. "أبويحيى"
            j += 1
            out_end = j
            continue
        if h.startswith("ال") and h not in NOT_NISBA:
            j += 1
            out_end = j
            continue
        break  # unknown word: description starts here
    else:
        if j >= MAX_TOKENS:
            flags.append("capped")
    if stop_known is None and j < n:
        if h not in STOP and j < MAX_TOKENS:
            flags.append("unknown_stop:" + clean_tok(t[j]))
    if j >= n:
        flags.append("no_stop")
    name_tokens = [clean_tok(x) for k, x in enumerate(t[:out_end]) if k not in skipped]
    name_tokens = [x for x in name_tokens if x]
    # drop dangling "بن"
    while name_tokens and norm(name_tokens[-1]) in BIN:
        name_tokens.pop()
        flags.append("dangling_bin")
    name = " ".join(name_tokens)
    if len(name_tokens) > 9:
        flags.append("long")
    if len(name_tokens) == 1:
        flags.append("single_token")
    return name, flags, out_end


SYMS_1187 = SYMS | {norm(x) for x in "و عو م٤ ب حب مكرر متابعة ـ ع٤ خ٤ ت٤ د٤ س٤ ق٤".split()}


def strip_symbols(tokens, symset=None):
    symset = symset or SYMS
    syms = []
    i = 0
    while i < len(tokens):
        k = norm(tokens[i].strip("()[]"))
        if k in symset and (len(k) <= 6):
            syms.append(tokens[i].strip("()[]"))
            i += 1
        else:
            break
    return syms, i


# --------------------------------------------------------------------------- entry kinds
KINDS = [
    ("taswiya", r"التسوية"),
    ("shuyukh", r"تدليس الشيوخ|تدليس شيوخ|تدليس الشيخ|في الشيوخ|يدلس شيوخ|دلس شيوخ|يسمي شيوخه|يكني شيوخ|الشيوخ"),
    ("qat", r"تدليس القطع|القطع"),
    ("atf", r"تدليس العطف|العطف"),
    ("ijaza", r"الاجازة|إجازة|اجازة"),
    ("wijada", r"الوجادة"),
]
MUKHTALIT = re.compile(r"(?<![ء-ي])(اختلط|اختلاط|الاختلاط|تغير|تغيّر)")


def derive_kind(text):
    nt = norm(text)
    found = []
    for k, pat in KINDS:
        if re.search(norm(pat), nt):
            found.append(k)
    if "taswiya" in found:
        kind = "taswiya"
    elif "shuyukh" in found:
        kind = "shuyukh"
    else:
        kind = "isnad"
    return kind, found, bool(MUKHTALIT.search(norm(text)))


# --------------------------------------------------------------------------- footnotes
def load_footnotes(path):
    foot = {}
    for page, body in load_tsv(path):
        body = OTHER_TAG.sub("", body).strip()
        if body.startswith("[التعليق]") and len(body) < 400 and "\n" in body and body.count("\n") == 1:
            continue  # placeholder: "[التعليق]\n <section title>"
        if body.replace("[التعليق]", "").strip() in ("", "(*)"):
            continue
        foot[page] = body
    return foot


def split_foot_items(text):
    items = {}
    cur = None
    for line in text.split("\n"):
        m = re.match(r"\s*\((\d+|[٠-٩]+)\)\s*(.*)", line)
        if m:
            cur = to_int(m.group(1))
            items[cur] = m.group(2)
        elif cur is not None:
            items[cur] += "\n" + line
    return items


# --------------------------------------------------------------------------- 1186 parsing
TIER_WORDS = {"الاولي": 1, "الثانيه": 2, "الثالثه": 3, "الرابعه": 4, "الخامسه": 5}
COUNT_WORDS = {
    "ثلاثه وثلاثون": 33, "خمسون": 50, "اثنا عشر": 12, "اثني عشر": 12,
    "اربعه وعشرون": 24, "ثلاثه وثلاثين": 33, "اربعه وعشرين": 24, "سبعه وثلاثون": 37,
    "عشرون": 20, "سبعه وثلاثين": 37,
}
MARKER = re.compile(r"\(\s*([٠-٩0-9]+)\s*\)")
EXPECTED_COUNTS = {1: 33, 2: 33, 3: 50, 4: 12, 5: 24}   # as stated in the book itself
USER_COMMON_COUNTS = {1: 33, 2: 37, 3: 50, 4: 12, 5: 20}  # commonly cited, for comparison only


def classify_title(text):
    nt = norm(text)
    m = re.search(r"المرتبه\s+(\S+)", nt)
    if m and m.group(1) in TIER_WORDS:
        return ("tier", TIER_WORDS[m.group(1)])
    if nt.strip().startswith("ملحق"):
        return ("appendix", None)
    return (None, None)


def parse_book_1186(dump_dir):
    rows = load_tsv(dump_dir + "/1186_pages.tsv")
    stream, starts = build_stream(rows)
    offsets = [s[0] for s in starts]
    foot = load_footnotes(dump_dir + "/1186_foot.tsv")

    # segments between title spans
    segs = []
    cur = (None, None)
    last = 0
    for m in re.finditer(r"\x00(.*?)\x01", stream, re.S):
        segs.append((cur, last, m.start()))
        cur = classify_title(m.group(1))
        last = m.end()
    segs.append((cur, last, len(stream)))

    issues = []
    entries = []
    stated = {}
    expected = 1
    seen_nums = []
    ignored_markers = []

    for (section, tier), a, b in segs:
        if section is None:
            continue
        text = stream[a:b]
        if section == "tier":
            m = re.search(r"وعدتهم\s+(.*?)\s+نفسا", text)
            if m:
                stated[tier] = {"phrase": m.group(1), "n": COUNT_WORDS.get(norm(m.group(1)))}
        accepted = []  # (num, start, end_marker_pos)
        for m in MARKER.finditer(text):
            val = to_int(m.group(1))
            if val == 0:
                continue
            before = text[max(0, m.start() - 6):m.start()]
            after = text[m.end():m.end() + 3].lstrip()
            at_line_start = m.start() == 0 or text[:m.start()].rstrip(" ").endswith("\n") or \
                re.search(r"\n\s*\)?\s*$", text[:m.start()]) is not None or \
                bool(PAGE_MARK.fullmatch(text[max(0, m.start() - 8):m.start()].lstrip("\n")))
            if re.search(r"(?<![ء-ي])(ص|ج)[ \t]*$|/[ \t]*$", before) or after.startswith(":"):
                continue  # page reference such as "ص (١٦٣) :"
            if val == expected:
                accepted.append((val, m.start(), m.end(), None))
                expected += 1
            elif expected < val <= expected + 3 and at_line_start:
                issues.append("gap: expected %d, found %d (missing %s) in section %s%s" % (
                    expected, val, ",".join(str(x) for x in range(expected, val)), section,
                    "" if tier is None else " tier %d" % tier))
                accepted.append((val, m.start(), m.end(), "after_gap"))
                expected = val + 1
            elif val < expected:
                if at_line_start:
                    ignored_markers.append({"value": val, "context": PAGE_MARK.sub("", text[max(0, m.start() - 30):m.end() + 30])})
        for idx, (num, ms, me, note) in enumerate(accepted):
            end = accepted[idx + 1][1] if idx + 1 < len(accepted) else len(text)
            raw = text[me:end]
            entry_pages = [int(x) for x in PAGE_MARK.findall(raw)]
            page = page_at(starts, offsets, a + me)
            pages = [page] + [p for p in entry_pages if p != page]
            body_lines = [re.sub(r"\s+", " ", PAGE_MARK.sub(" ", ln)).strip() for ln in raw.split("\n")]
            body_lines = [re.sub(r"\(٠\)", "", ln).strip() for ln in body_lines]
            body_lines = [ln for ln in body_lines if ln]
            entries.append({
                "num": num, "tier": tier, "section": section, "_lines": body_lines,
                "page": page, "pages": pages, "_note": note,
            })

    # entry content
    for e in entries:
        lines = e.pop("_lines")
        note = e.pop("_note")
        text = " ".join(lines)
        flags = []
        editor_notes = []
        if e["section"] == "appendix":
            first = lines[0] if lines else ""
            first = re.sub(r"^-?\s*[٠-٩0-9]+\s*-\s*", "", first)
            m = re.match(r"(.*?)\s*/\s*(.*?)(?:[:：]|\.\s|\.$)", first)
            name_part, sym_part, desc_first = "", "", ""
            if m:
                name_part = m.group(1)
                sym_part = m.group(2)
                desc_first = first[m.end():].strip()
            else:
                m2 = re.match(r"(.*?)\s*[:：]\s*(.*)", first)
                if m2:
                    name_part, desc_first = m2.group(1), m2.group(2)
                else:
                    name_part, fl, _ = parse_name(first.split())
                    flags.append("appendix_no_delimiter")
                    flags.extend(fl)
            syms = [s for s in re.split(r"\s+", sym_part.strip()) if norm(s.strip("()")) in SYMS]
            name_part = name_part.replace("،", " ").replace(",", " ")
            toks = [x for x in name_part.split() if x]
            toks = [clean_tok(x) for x in toks if norm(x) != "مولاهم"]
            toks = [x for x in toks if x]
            # cut at a stop/description word
            cut = []
            for k, w in enumerate(toks):
                if norm(w) in STOP and k > 0 and norm(w) not in ("مولي", "ثم"):
                    flags.append("appendix_cut:" + w)
                    break
                if norm(w) == "مولي":
                    flags.append("appendix_mawla_cut")
                    break
                cut.append(w)
            toks = cut
            if len(toks) > 9:
                flags.append("long")
            name = " ".join(toks)
            e["symbols"] = syms
            e["name"] = name
            e["name_flags"] = flags
            e["appendix_seq"] = None
            msq = re.match(r"^-?\s*([٠-٩0-9]+)\s*-", lines[0]) if lines else None
            if msq:
                e["appendix_seq"] = to_int(msq.group(1))
            e["editor_notes"] = lines[1:]
            text = re.sub(r"^-?\s*[٠-٩0-9]+\s*-\s*", "", text)
        else:
            toks = text.split()
            syms, k = strip_symbols(toks)
            rest = toks[k:]
            lead = []
            if len(rest) > 2 and norm(rest[0]) == "الله" and norm(rest[1]) == "تعالي":
                rest = rest[2:]  # stray tail of a previous sentence ("... رضي الله تعالى")
                lead = ["fixed_leading_الله_تعالى"]
            name, nflags, _ = parse_name(rest)
            nflags = lead + nflags
            e["symbols"] = syms
            e["name"] = name
            e["name_flags"] = nflags
        e["text"] = text
        kind, found, mukh = derive_kind(text)
        e["kind"] = kind
        e["kinds_mentioned"] = found
        e["also_mukhtalit"] = mukh
        # footnote matching: marker (N) in the entry that is not the entry's own number
        marks = {to_int(x) for x in re.findall(r"\(\s*([٠-٩0-9]+)\s*\)", text)}
        foots = []
        for p in e["pages"]:
            if p in foot:
                items = split_foot_items(foot[p])
                hit = [items[mk] for mk in sorted(marks) if mk in items]
                foots.extend(hit if hit else [])
        e["foot"] = foots
        if note:
            e["numbering_note"] = note
    # stable key order
    order = ["num", "tier", "section", "symbols", "name", "kind", "also_mukhtalit", "kinds_mentioned",
             "text", "page", "pages", "foot", "name_flags", "editor_notes", "appendix_seq", "numbering_note"]
    entries = [{k: e[k] for k in order if k in e} for e in entries]

    # validation
    per_tier = {}
    for t in range(1, 6):
        nums = [e["num"] for e in entries if e["tier"] == t]
        per_tier[t] = {"count": len(nums), "first": min(nums) if nums else None,
                       "last": max(nums) if nums else None,
                       "stated_in_book": stated.get(t), "expected": EXPECTED_COUNTS[t],
                       "user_commonly_cited": USER_COMMON_COUNTS[t]}
    app = [e for e in entries if e["section"] == "appendix"]
    nums_all = [e["num"] for e in entries]
    dup = sorted({n for n in nums_all if nums_all.count(n) > 1})
    tier_nums = [e["num"] for e in entries if e["section"] == "tier"]
    gaps = [n for n in range(1, (max(tier_nums) if tier_nums else 0) + 1) if n not in set(tier_nums)]
    app_nums = [e["num"] for e in app]
    app_gaps = [n for n in range(min(app_nums), max(app_nums) + 1) if n not in set(app_nums)] if app_nums else []
    seqs = [e["appendix_seq"] for e in app]
    uncertain = [{"num": e["num"], "name": e["name"], "flags": e["name_flags"],
                  "start": e["text"][:90]} for e in entries
                 if any(f.split(":")[0] in ("capped", "no_stop", "long", "single_token", "unknown_stop",
                                            "dangling_bin", "title_skipped", "empty",
                                            "fixed_leading_الله_تعالى", "appendix_no_delimiter",
                                            "appendix_cut", "appendix_mawla_cut")
                        for f in e["name_flags"])]
    stats = {
        "total_entries": len(entries), "tier_entries": len(tier_nums), "appendix_entries": len(app),
        "per_tier": per_tier, "tier_total_expected_by_book": 152,
        "tier_numbering_gaps": gaps, "appendix_numbering_gaps": app_gaps,
        "appendix_internal_seq": seqs, "duplicate_numbers": dup,
        "numbering_issues": issues, "ignored_markers_below_expected": ignored_markers,
        "foot_pages_with_real_text": sorted(foot.keys()),
        "entries_with_foot": sum(1 for e in entries if e["foot"]),
        "uncertain_names": uncertain,
        "kinds": {k: sum(1 for e in entries if e["kind"] == k) for k in ("isnad", "shuyukh", "taswiya")},
        "also_mukhtalit": sum(1 for e in entries if e["also_mukhtalit"]),
        "entries_spanning_pages": sum(1 for e in entries if len(e["pages"]) > 1),
    }
    return entries, stats


# --------------------------------------------------------------------------- 1187 (التبيين)
def parse_book_1187(dump_dir):
    rows = load_tsv(dump_dir + "/1187_pages.tsv")
    stream, starts = build_stream(rows)
    offsets = [s[0] for s in starts]
    stream = re.sub(r"\x00(.*?)\x01", lambda m: "\n\x00" + m.group(1) + "\x01\n", stream, flags=re.S)
    # keep only the body between the preface and the closing remarks
    a = stream.find("وقد رتبتهم على حروف المعجم")
    b = stream.find("ثم اعلم أيها الواقف")
    body = stream[a:b] if a >= 0 and b > a else stream
    entries = []
    pat = re.compile(r"(?:(?<=\n)|(?<=\x03))\s*([٠-٩]+)\s*-\s+", re.M)
    ms = list(pat.finditer(body))
    for i, m in enumerate(ms):
        end = ms[i + 1].start() if i + 1 < len(ms) else len(body)
        raw = body[m.end():end]
        page = page_at(starts, offsets, a + m.end())
        text = re.sub(r"\s+", " ", PAGE_MARK.sub(" ", re.sub(r"\x00.*?\x01", " ", raw))).strip()
        toks = text.split()
        repeat = False
        if toks and norm(toks[0]) == "مكرر":  # "مكرر - ع حفص ..." = the editor's repeated number
            repeat = True
            toks = [x for x in toks[1:] if x not in ("-", "ـ")]
            text = " ".join(toks)
        syms, k = strip_symbols(toks, SYMS_1187)
        rest = toks[k:]
        paren = False
        if rest and rest[0].startswith("("):
            # parenthesised name: "(إبراهيم بن محمد) description"
            acc = []
            for w in rest:
                acc.append(w)
                if w.endswith(")"):
                    break
            inner = " ".join(acc).strip("()")
            name, flags, _ = parse_name(inner.split())
            flags = [f for f in flags if f != "no_stop"]
            paren = True
        else:
            name, flags, _ = parse_name(rest)
        entries.append({"num": to_int(m.group(1)), "symbols": syms, "name": name, "page": page,
                        "text": text[:200], "parenthesised": paren, "repeat": repeat, "name_flags": flags})
    return entries


def key_tokens(name):
    s = norm(name)
    s = re.sub(r"[()،,.:]", " ", s)
    toks = []
    for w in s.split():
        if w in ("ابن",):
            w = "بن"
        w = re.sub(r"^ال", "", w) if len(w) > 3 else w
        if w == "بن" or not w:
            continue
        toks.append(w)
    return toks


def cross_check(tab, ibn_entries):
    ibn = [(e["num"], e["name"], key_tokens(e["name"])) for e in ibn_entries]
    not_found, fuzzy, matched = [], [], 0
    for e in tab:
        if e.get("repeat"):
            continue
        kt = key_tokens(e["name"])
        s = set(kt)
        best, best_score, how = None, 0.0, None
        for num, nm, it in ibn:
            si = set(it)
            if not s or not si:
                continue
            if kt == it:
                best, best_score, how = (num, nm), 1.0, "exact"
                break
            inter = len(s & si)
            if inter >= 2 and (s <= si or si <= s):
                sc = 0.95
                how_ = "subset"
            elif len(kt) >= 2 and len(it) >= 2 and kt[:2] == it[:2] and inter >= 3:
                sc = 0.6  # same ism + father, plus another shared word (long nasab in one source)
                how_ = "ism_father"
            else:
                sc = inter / len(s | si)
                how_ = "jaccard"
            if sc > best_score:
                best, best_score, how = (num, nm), sc, how_
        # single-token names (e.g. kunya-only) need an exact token hit
        if best and best_score >= 0.95:
            matched += 1
        elif best and best_score >= 0.5:
            matched += 1
            fuzzy.append({"tabyin_num": e["num"], "tabyin_name": e["name"], "tabaqat_num": best[0],
                          "tabaqat_name": best[1], "score": round(best_score, 2)})
        else:
            not_found.append({"tabyin_num": e["num"], "name": e["name"], "symbols": e["symbols"],
                              "page": e["page"], "start": e["text"][:100],
                              "name_flags": e["name_flags"]})
    # reverse direction, as a bonus
    tk = [key_tokens(e["name"]) for e in tab if not e.get("repeat")]
    rev = []
    for num, nm, it in ibn:
        si = set(it)
        ok = any((set(k) <= si or si <= set(k)) and len(si & set(k)) >= 2 or
                 (len(si & set(k)) / max(1, len(si | set(k)))) >= 0.5 for k in tk)
        if not ok:
            rev.append({"tabaqat_num": num, "name": nm})
    return {
        "tabyin_entries": len(tab), "tabyin_repeated_entries_skipped": sum(1 for e in tab if e.get("repeat")),
        "matched": matched, "of_which_fuzzy_review": len(fuzzy),
        "not_in_tabaqat_count": len(not_found), "not_in_tabaqat": not_found,
        "fuzzy_matches_to_review": fuzzy,
        "tabaqat_tier_entries_not_in_tabyin_count": len(rev), "tabaqat_tier_entries_not_in_tabyin": rev,
        "tabyin_numbering_gaps": [n for n in range(1, max(e["num"] for e in tab) + 1)
                                  if n not in {e["num"] for e in tab}],
        "note": "Rough match on normalised name tokens (article and بن dropped); subset of >=2 tokens "
                "or Jaccard >= 0.5 counts as a match; review the fuzzy list.",
    }


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(1)
    dump_dir, out = sys.argv[1], sys.argv[2]
    entries, stats = parse_book_1186(dump_dir)
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"entries": entries, "stats": stats}, f, ensure_ascii=False, indent=1)
    print("entries:", len(entries))
    for t, v in stats["per_tier"].items():
        print("tier", t, "count", v["count"], "expected", v["expected"], "stated", v["stated_in_book"],
              "nums", v["first"], "-", v["last"])
    print("appendix:", stats["appendix_entries"], "gaps", stats["tier_numbering_gaps"],
          stats["appendix_numbering_gaps"], "dups", stats["duplicate_numbers"])
    if len(sys.argv) > 3:
        tab = parse_book_1187(dump_dir)
        ibn_entries = [e for e in entries]
        res = cross_check(tab, ibn_entries)
        res["tabyin_names"] = [{"num": e["num"], "name": e["name"], "symbols": e["symbols"],
                                "flags": e["name_flags"]} for e in tab]
        with open(sys.argv[3], "w", encoding="utf-8") as f:
            json.dump(res, f, ensure_ascii=False, indent=1)
        print("1187 entries:", len(tab), "not in 1186:", res["not_in_tabaqat_count"],
              "fuzzy:", res["of_which_fuzzy_review"])


if __name__ == "__main__":
    main()
