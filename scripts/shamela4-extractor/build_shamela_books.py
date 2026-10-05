"""Build data/shamela/<slug>/ (book.json, index.json, <n>.json) from the Shamela dumps (Phase 4 output format).

Unlike build_itqan_books.py this does NOT cut records at Shamela page rows (that causes the shifted
records of docs/shamela_migration.md 6.1).  All page bodies are concatenated into one stream, and records are
cut at the edition's own in-text hadith-number markers at paragraph starts ("380 - حدثنا", "[3936] أخبرنا",
"4100 م- ...", "6769/1- ...").  Books without in-text numbers fall back to cutting at isnad openings at paragraph
starts (reported as mode=isnad).

Inputs : <dump>/<id>_pages.tsv, _titles.tsv, _foot.tsv   (keys "<bookid>-<pageid>", bodies with literal \\n)
         <book dir>/<id % 1000, 3 digits>/<id>.db         (page(id, part, page, number, services), title(id, page, parent))
Output : <out>/<slug>/book.json, index.json, <n>.json     (git-ignored scratch data)
Usage  : python build_shamela_books.py [out_dir] [slug ...]
         env SHAMELA_DUMP, SHAMELA_BOOK_DIR, SHAMELA_MASTER override the defaults.
"""
import bisect
import json
import os
import re
import sqlite3
import sys
from collections import Counter
from pathlib import Path

# ---------------------------------------------------------------------------------------------------------------
# Book list: (slug, shamela_id, compiler).  Add the other 12 here later.
# ---------------------------------------------------------------------------------------------------------------
BOOKS = [
    ("bukhari", 1681, "محمد بن إسماعيل بن إبراهيم بن المغيرة"),
    ("muslim", 711, "مسلم بن الحجاج بن مسلم"),
    ("abudawud", 654, "سليمان بن الأشعث بن شداد"),
    ("tirmidhi", 7895, "محمد بن عيسى بن سورة"),
    ("nasai", 829, "أحمد بن شعيب بن علي"),
    ("malik", 28107, "مالك بن أنس بن مالك"),
    ("ibnmajah", 98138, "محمد بن يزيد الربعي"),
    ("ahmed", 25794, "أحمد بن محمد بن حنبل"),
    ("darimi", 36114, "عبد الله بن عبد الرحمن بن الفضل"),
    ("aladab_almufrad", 12991, "محمد بن إسماعيل بن إبراهيم بن المغيرة"),
    ("shamail_muhammadiyah", 13037, "محمد بن عيسى بن سورة"),
    ("musannaf_ibnabi_shaybah", 333, "عبد الله بن محمد بن إبراهيم بن عثمان"),
    ("musannaf_abdurrazzaq", 13174, "عبد الرزاق بن همام بن نافع"),
    ("musnad_tayalisi", 1456, "سليمان بن داود بن الجارود"),
    ("musnad_shafii", 9344, "محمد بن إدريس بن العباس"),
    ("musnad_humaydi", 8493, "عبد الله بن الزبير بن عيسى"),
    ("sunan_said_ibn_mansur", 13122, "سعيد بن منصور بن شعبة"),
    ("musnad_ishaq", 13159, "إسحاق بن إبراهيم بن مخلد"),
    ("musnad_bazzar", 12981, "أحمد بن عمرو بن عبد الخالق"),
    ("sunan_kubra_nasai", 8361, "أحمد بن شعيب بن علي"),
    ("musnad_abi_yala", 12520, "أحمد بن علي بن المثنى"),
    ("sahih_ibn_khuzaymah", 1446, "محمد بن إسحاق بن خزيمة"),
    ("mustakhraj_abi_awanah", 18144, "يعقوب بن إسحاق بن إبراهيم بن يزيد أبو عوانة"),
    ("sahih_ibn_hibban", 537, "محمد بن حبان بن أحمد"),
    ("mujam_kabir_tabarani", 1733, "سليمان بن أحمد بن أيوب"),
    ("mujam_awsat_tabarani", 28171, "سليمان بن أحمد بن أيوب"),
    ("mujam_saghir_tabarani", 13068, "سليمان بن أحمد بن أيوب"),
    ("sunan_daraqutni", 9771, "علي بن عمر بن أحمد بن مهدي"),
    ("mustadrak_hakim", 1424, "محمد بن عبد الله بن محمد بن حمدويه الحاكم"),
    ("sunan_kubra_bayhaqi", 148486, "أحمد بن الحسين بن علي بن موسى"),
    ("shuab_iman_bayhaqi", 10660, "أحمد بن الحسين بن علي بن موسى"),
]

# Books whose pages start with a displaced isnad fragment before the numbered paragraph:
#   "أخبرنا\n\n7819 - عبد الرزاق قال" means "7819 - أخبرنا عبد الرزاق قال" (the fragment is moved after the number).
LEAD_FRAG_BOOKS = {13174}

# Books whose markers carry a second number (only these use RE_MARK_X, so the other books are untouched):
#   DOUBLE_NUM_BOOKS: one block holds two hadiths, "408 - 409 - حدثنا" or "[408 - 409]": one record, number = the first,
#                     number_label = both.
#   PAIR_KEY: "91 - (52) وحدثنا": two running numbers, the first N and the parenthesised M.  Which of them is the
#             record's `number`: "second" (Muslim 711: N restarts in every kitab, M is the Abd al-Baqi number that runs
#             1..3033) or "first" (al-Darimi 36114: N runs 1..3,7xx, M restarts in every bab).  number_label keeps both.
DOUBLE_NUM_BOOKS = {1681}
PAIR_KEY = {711: "second", 36114: "first"}
# the 12 books added in Phase 4: they use RE_MARK_X (optional "* " / "• " prefix, "(م)", pair) and the extra passes below
NEW_BOOKS = {1681, 711, 654, 7895, 829, 28107, 98138, 25794, 36114, 12991, 13037, 333}
EXT_BOOKS = NEW_BOOKS
# Books where a hadith number may also sit in the middle of a line ("... كِلَانَا جُنُبٌ، 300 - وَكَانَ يَأْمُرُنِي"):
# only the numbers the line-start markers skipped (the next numbers of the sequence) are looked for, inside the
# record that should hold them.
STRICT_START = NEW_BOOKS
LOOSE_START = {13037, 36114}      # first hadith opens with "قال الحافظ أبو عيسى ... حدثنا" / a heading line: any isnad word will do
REPAIR_BOOKS = NEW_BOOKS - {711}     # Muslim: the out-of-order (M) numbers are real (a repeated hadith keeps its first Abd al-Baqi number)
INLINE_BOOKS = NEW_BOOKS

REPO_ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = Path(sys.argv[1]) if len(sys.argv) > 1 else REPO_ROOT / "data" / "shamela"
ONLY = set(sys.argv[2:])
DUMP_DIR = Path(os.environ.get("SHAMELA_DUMP", REPO_ROOT / "data" / "shamela" / "dump"))
BOOK_DIR = Path(os.environ.get("SHAMELA_BOOK_DIR", r"D:\Islamic\shamela4\database\book"))
MASTER_DB = Path(os.environ.get("SHAMELA_MASTER", r"D:\Islamic\shamela4\database\master.db"))

NL = chr(92) + "n"  # the literal two characters backslash + n used in the dumps

# private-use placeholders used inside the stream
FN_O, FN_C = "\ue000", "\ue001"   # footnote mark   FN_O <row>:<n> FN_C
T_O, T_C = "\ue002", "\ue003"     # title anchor    T_O <title id> T_C
P_O, P_C = "\ue004", "\ue005"     # printed page marker  P_O <n> P_C
# Shamela's narrator links / matn groups.  The ids are encoded with the private-use "digit" characters
# DIGCH (so no ASCII digit is added to the stream); NA_O <id> ... NA_C wraps a narrator name, G_O <id> ... G_C a matn.
NA_O, NA_C = "\ue006", "\ue007"
G_O, G_C = "\ue008", "\ue009"
DIGCH = "".join(chr(0xE100 + i) for i in range(10))
LP_CHARS = NA_O + NA_C + G_O + G_C + DIGCH
RE_LP = re.compile(f"[{LP_CHARS}]")
RE_LP_TOKEN = re.compile(f"([{NA_O}{G_O}])([{DIGCH}]*)|([{NA_C}{G_C}])")
RE_A_LINK = re.compile(r"""<a\s+href\s*=\s*["']inr://man-(\d+)["']\s*>(.*?)</a>""", re.DOTALL)
RE_G_OPEN = re.compile(r"<hadeeth-(\d+)>")
RE_G_CLOSE = re.compile(r"<hadeeth>")
RE_A_ANY = re.compile(r"inr://man-\d+")
RE_G_ANY = re.compile(r"<hadeeth-\d+>")


RE_WS_RUN = re.compile(r"[ \t\u200f\u200e]+")
RE_NL_RUN = re.compile(r" ?\n[ \n]*")


def enc_id(n):
    return "".join(chr(0xE100 + int(d)) for d in str(n))


def dec_id(s):
    return int("".join(str(ord(c) - 0xE100) for c in s))


def split_marks(s):
    """text with link placeholders -> (clean text, [[pos, kind, id]]); kind in 'NO','NC','GO','GC'."""
    out, marks, last, n = [], [], 0, 0
    for m in RE_LP_TOKEN.finditer(s):
        out.append(s[last:m.start()])
        n += m.start() - last
        last = m.end()
        if m.group(3):
            marks.append([n, "NC" if m.group(3) == NA_C else "GC", None])
        else:
            marks.append([n, "NO" if m.group(1) == NA_O else "GO", dec_id(m.group(2)) if m.group(2) else None])
    out.append(s[last:])
    return "".join(out), marks


def join_marks(clean, marks):
    parts, last = [], 0
    for pos, kind, ident in marks:      # marks are in ascending order
        parts.append(clean[last:pos])
        last = pos
        if kind == "NO":
            parts.append(NA_O + enc_id(ident))
        elif kind == "GO":
            parts.append(G_O + enc_id(ident))
        else:
            parts.append(NA_C if kind == "NC" else G_C)
    parts.append(clean[last:])
    return "".join(parts)


def sub_marked(clean, marks, pat, repl):
    """re.sub on the clean text; the marks follow their text (a mark inside a replaced run goes to its start)."""
    segs, out, last, newlen = [], [], 0, 0
    for m in pat.finditer(clean):
        out.append(clean[last:m.start()])
        newlen += m.start() - last
        segs.append((m.start(), m.end(), newlen, len(repl)))
        out.append(repl)
        newlen += len(repl)
        last = m.end()
    out.append(clean[last:])
    starts = [sg[0] for sg in segs]
    new_marks = []
    for pos, kind, ident in marks:
        i = bisect.bisect_right(starts, pos) - 1
        if i >= 0:
            a, b, ns, L = segs[i]
            pos = ns + L + (pos - b) if pos >= b else ns
        new_marks.append([pos, kind, ident])
    return "".join(out), new_marks

DIG = "٠-٩0-9"
TRANS = {ord(c): str(i) for i, c in enumerate("٠١٢٣٤٥٦٧٨٩")}
TO_AR = {ord(str(i)): c for i, c in enumerate("٠١٢٣٤٥٦٧٨٩")}
HARAKAT = re.compile("[\u064b-\u0652\u0670\u0640]")

RE_TAG = re.compile(r"</?[A-Za-z][^>]*>")
RE_SPAN = re.compile(r"<span\b([^>]*)>(.*?)</span>", re.DOTALL)
RE_TOC_ID = re.compile(r"""id\s*=\s*["']?toc-(\d+)""")
RE_FN_MARK = re.compile(r"\(\s*¬\s*([" + DIG + r"]+)\s*\)")
RE_PAGE_MARK = re.compile(r"([ \t\n]*)⦗([" + DIG + r"]+)⦘([ \t\n]*)")
RE_FOOT_ITEM = re.compile(r"^\(\s*(¬)?\s*([" + DIG + r"]+)\s*\)[ \t]*")
RE_PLAIN_MARK = re.compile(r"\(\s*([" + DIG + r"]+)\s*\)")
RE_PH_ANY = re.compile(f"[{FN_O}{FN_C}{T_O}{T_C}{P_O}{P_C}{LP_CHARS}]")

PHP = f"(?:{P_O}\\d+{P_C}[ \\t]*)*"   # zero or more page-marker placeholders
# an in-text hadith number marker at the start of a line.
#   380 -   [3936]   (12)   4100 م-   6769/1-   [5431 / ألف]   3599 -حدثنا   4302،

def mark_re(ext=False):
    """The marker regex.  ext=True (only the books that need it) also takes a second number after the first:
    "91 - (52)" (group pair).  Plain second numbers ("408 - 409 -") are chained by build_book."""
    second = (r"(?:(?P<pair>\((?:["+ DIG + r"]+|م)\))[ \t]*)?") if ext else ""
    return re.compile(
        r"^" + PHP + r"[ \t]*" +
        # Malik: the title's footnote mark sits in front of the next number ("(¬١) ٢٨/ ٩ - مالك"); the mark is dropped
        ("(?:" + FN_O + r"\d+:\d+" + FN_C + r"[ \t]*)*" if ext else "") +
        (r"(?:[*•°·][ \t]*)?" if ext else "") +   # Musnad Ahmad: "* 518 -", "• 521 -", "° 519 -"
        r"(?P<open>[\[\(])?[ \t]*"
        r"(?P<num>[" + DIG + r"]+)"
        r"(?P<suf>(?:[ \t]*(?:/[ \t]*(?:[" + DIG + r"]+|[\u0621-\u064a]+)|(?:م|ب|ألف|أ)(?![\u0621-\u064a\u064b-\u0652])|\*"+ (r"|\(م\)" if ext else "") + r"))*)"
        r"[ \t]*(?P<close>\]|\)|[-–—]+|،)?[ \t]*" + second + PHP,
        re.M)


RE_MARK = mark_re()
RE_MARK_X = mark_re(True)

VERB_START = re.compile(
    r"^[\s\u200f\u200e\[\(]*(?:[وف]\s?)?(?:حدثنا|حدثني|حدثناه|حدثنيه|أخبرنا|أخبرني|أخبرناه|أخبرنيه|أنبأنا|أنبأني|"
    r"أنبأناه|أنبأ|أنا|ثنا|نا|حدثه|أخبره|سمعت|قرأت|أخبرت|حدثت)(?![\u0621-\u064a])")
VERB_ANY = re.compile(r"(?:حدث|أخبر|أنبأ|سمعت|قرأت|بلغني)|(?<![\u0621-\u064a])(?:ثنا|نا|أنا)(?![\u0621-\u064a])")


RE_PAIR_LEAD = re.compile(r"(?:^|\n)[ \t]*[" + DIG + r"]+(?:[ \t]*م)?[ \t]*[-–—][ \t]*$")
RE_ONLY_BRACKET_NUMS = re.compile(r"^(?=.*[\d٠-٩])[ \t\n\[\]\d٠-٩]+$")   # "[٣٤]", "٦] [١٢٤]": page numbers of the print, no text
RE_ONLY_FN_MARK = re.compile(r"^(?:\(["+ DIG + r"]+\)[ \t\n.*]*)+$")        # "(١).", "(٣) *": a footnote mark alone
RE_ELIDED_NUM = re.compile(r"^(?:\(["+ DIG + r"]+\)[ \t\n]*)?(?P<lab>(?:\. ?){2,}/ ?["+ DIG + r"]+) ?[-–—] ?")   # Ahmed: "(١)\n. . . / ٣ - حدثنا" (number elided)
RE_BACK_MATTER = re.compile(r"[ \t\n]*_{5,}")      # Muslim 711: "... بمثل حديث هشيم _________" then the editor's afterword
RE_NEXT_NUM =re.compile(r"(?P<n>[" + DIG + r"]+)[ \t]*(?P<c>\]|[-–—]+)?[ \t]*")


def norm_ar(s):
    return HARAKAT.sub("", s).replace("ى", "ي")


def to_int(digits):
    return int(digits.translate(TRANS))


EXTRA_VERB = None      # per book (set in build_book): Malik's isnads open with "مالك، عن" without a verb


def has_verb(text_after):
    t = norm_ar(RE_PH_ANY.sub("", text_after[:60]))[:40]
    return bool(VERB_START.match(t) or (EXTRA_VERB and EXTRA_VERB.match(t)))


# ---------------------------------------------------------------------------------------------------------------
# loading
# ---------------------------------------------------------------------------------------------------------------
def load_tsv(path):
    """{item_id: raw body} keyed by the part after '<bookid>-'.  Order of the file is kept."""
    out = {}
    if not path.exists():
        return out
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\r\n")
            if not line:
                continue
            key, _, body = line.partition("\t")
            try:
                out[int(key.split("-", 1)[1])] = body
            except (ValueError, IndexError):
                pass
    return out


def clean_plain(raw):
    t = raw.replace(NL, " ")
    t = RE_TAG.sub("", t)
    t = RE_FN_MARK.sub("", t)
    return re.sub(r"\s+", " ", t).strip()


def parse_foot(body, stats):
    """-> (preamble text, {n: text}).  Items start with '(¬N) '; other lines continue the previous item."""
    text = body.replace(NL, "\n")
    text = RE_TAG.sub("", text)
    text = re.sub(r"[ \t]*⦗[" + DIG + r"]+⦘[ \t]*", " ", text)
    pre, items, cur = [], {}, None
    plain = set()
    for line in text.split("\n"):
        line = line.strip()
        if not line:
            continue
        m = RE_FOOT_ITEM.match(line)
        if m:
            cur = to_int(m.group(2))
            if not m.group(1):
                plain.add(cur)
            if cur in items:
                stats["foot_dup_items"] += 1
                items[cur] += "\n" + line[m.end():]
            else:
                items[cur] = line[m.end():]
        elif cur is None:
            pre.append(line)
        else:
            items[cur] += "\n" + line
    return "\n".join(pre), items, plain


def book_titles_meta(master, bid):
    try:
        row = sqlite3.connect(master).execute("select book_name from book where book_id=?", (bid,)).fetchone()
        name = row[0] if row else ""
    except sqlite3.Error:
        name = ""
    if " - " in name:
        title, edition = name.split(" - ", 1)
    else:
        title, edition = name, ""
    return title.strip(), edition.strip()


# ---------------------------------------------------------------------------------------------------------------
# row preparation: one Shamela page body -> text with placeholders
# ---------------------------------------------------------------------------------------------------------------
RE_LEAD_FRAG = re.compile(r"^([^\n" + DIG + r"][^\n]{0,44})\n+(?:⦗[" + DIG + r"]+⦘\n+)?(?=[" + DIG + r"]+[ \t]*[-–—])")


def prep_row(rowid, raw, bid, stats, span_ids, plain_ns=()):
    body = raw.replace(NL, "\n").replace("\u00a0", " ")
    body = body.lstrip("\n ")
    # a displaced isnad fragment at the very top of the page
    m = RE_LEAD_FRAG.match(body)
    if m and len(m.group(1).split()) <= 4 and not re.search(r"[.؟!»\"]\s*$", m.group(1)):
        stats["lead_frag_candidates"] += 1
        if bid in LEAD_FRAG_BOOKS:
            rest = body[m.end():]
            mm = re.match(r"([" + DIG + r"]+[ \t]*[-–—]+[ \t]*)", rest)
            if mm:
                body = mm.group(1) + m.group(1).strip() + " " + rest[mm.end():]
                stats["lead_frag_fixed"] += 1

    # titles with an anchor id become anchors on their own line; other spans (editor labels) are plain text
    def span_sub(sm):
        attrs, inner = sm.group(1), sm.group(2)
        if "data-type" in attrs and "title" in attrs:
            tm = RE_TOC_ID.search(attrs)
            if tm:
                # a title span can swallow the following numbered paragraph (Ibn Khuzaymah): give it back
                sp = re.search(r"\n+(?=[\[\(]?[" + DIG + r"]+[ \t]*[^\s" + DIG + r"]{0,3}[ \t]*[-–—\]\)])", inner)
                if sp:
                    stats["title_split"] += 1
                    stats["marks_in_titles"] += len(RE_FN_MARK.findall(inner[:sp.start()]))
                    return f"\n{T_O}{tm.group(1)}{T_C}\n" + inner[sp.end():]
                stats["marks_in_titles"] += len(RE_FN_MARK.findall(inner))
                return f"\n{T_O}{tm.group(1)}{T_C}\n"
        return inner

    body = RE_SPAN.sub(span_sub, body)
    # the '.' that follows a title span ("</span>.\n") belongs to the heading
    body = re.sub(f"({T_C}\\n)[ \\t]*[.:]?[ \\t]*(?=\\n|$)", r"\1", body)
    # narrator links and matn groups become placeholders (everything else is dropped)
    body = RE_A_LINK.sub(lambda mk: NA_O + enc_id(mk.group(1)) + mk.group(2) + NA_C, body)
    body = RE_G_OPEN.sub(lambda mk: G_O + enc_id(mk.group(1)), body)
    body = RE_G_CLOSE.sub(G_C, body)
    body = RE_TAG.sub("", body)
    body = RE_FN_MARK.sub(lambda mk: f"{FN_O}{rowid}:{to_int(mk.group(1))}{FN_C}", body)
    if plain_ns:
        # footnote marks written "(1)" (no ¬), only where this page's foot has such an item and not at line start
        def plain_sub(mk):
            n = to_int(mk.group(1))
            if n in plain_ns and mk.start() > 0 and body[mk.start() - 1] != "\n":
                if bid in PAIR_KEY and RE_PAIR_LEAD.search(body[max(0, mk.start() - 24):mk.start()]):
                    return mk.group(0)       # the "(k)" of "N - (k)" is the hadith's own number, not a footnote mark
                stats["plain_marks"] += 1
                return f"{FN_O}{rowid}:{n}{FN_C}"
            return mk.group(0)
        body = RE_PLAIN_MARK.sub(plain_sub, body)

    # printed page markers
    def pm(mk):
        before, n, after = mk.groups()
        tail = RE_LP.sub("", body[mk.end():mk.end() + 40])[:30]
        starts_number = re.match(r"^[\[\(]?[" + DIG + r"]+[ \t]*[^\s" + DIG + r"]{0,3}[ \t]*[-–—\]\)]", tail) or tail.startswith(T_O)
        prev = body[mk.start() - 1:mk.start()] if mk.start() else ""
        if starts_number or (prev and prev in ".؟!»\"" and "\n" in (before + after)) or (mk.start() == 0 and "\n" in after):
            sep = "\n"
        elif after.startswith("\n\n") and not tail[:1].isalpha():
            sep = "\n"
        elif tail[:1] in "،.؛:؟,)" or not tail:
            sep = ""
        else:
            sep = " "
        return f"{P_O}{to_int(n)}{P_C}{sep}"

    body = RE_PAGE_MARK.sub(pm, body)
    # whitespace: single spaces, single newlines
    if RE_LP.search(body):
        clean, marks = split_marks(body)
        clean, marks = sub_marked(clean, marks, RE_WS_RUN, " ")
        clean, marks = sub_marked(clean, marks, RE_NL_RUN, "\n")
        lead = len(clean) - len(clean.lstrip("\n "))
        clean = clean.strip("\n ")
        marks = [[min(max(pos - lead, 0), len(clean)), k, i] for pos, k, i in marks]
        return join_marks(clean, marks)
    body = RE_WS_RUN.sub(" ", body)
    body = RE_NL_RUN.sub("\n", body)
    return body.strip("\n ")


# ---------------------------------------------------------------------------------------------------------------
# record text -> (arabic, footnotes)
# ---------------------------------------------------------------------------------------------------------------
PUNCT_NO_SPACE = "،.؛:؟,)"
FN_OR_PAGE_RE = re.compile(FN_O + r"(\d+):(\d+)" + FN_C + "|" + P_O + r"\d+" + P_C)


def finish_text(text, foot_by_row, used_foot, stats, in_kept=True):
    """Remove placeholders, normalise spaces; returns (arabic, [(at, row, n)], marks).

    marks = [[pos, kind, id]] (kind NO/NC narrator open/close, GO/GC matn group open/close): the narrator-link and
    matn-group placeholders are transparent here, they only record their offset in the final text."""
    chunks = []
    pos = 0
    for m in FN_OR_PAGE_RE.finditer(text):
        chunks.append((text[pos:m.start()], m))
        pos = m.end()
    chunks.append((text[pos:], None))
    cur = ""
    fns = []           # [at, row, n]
    marks = []         # [pos, kind, id]
    waiting = []       # indexes in fns whose offset is not final yet
    removed = False    # a placeholder was removed right before the next chunk
    for chunk, m in chunks:
        if chunk:
            chunk, cmarks = split_marks(chunk) if RE_LP.search(chunk) else (chunk, ())
            shift = 0
            if chunk:
                if removed:
                    if cur.endswith(" ") and chunk.startswith(" "):
                        chunk = chunk[1:]
                        shift = 1
                    if cur and chunk.startswith(" ") and chunk[1:2] and chunk[1] in PUNCT_NO_SPACE:
                        chunk = chunk[1:]
                        shift += 1
                    if cur.endswith(" ") and chunk[:1] and chunk[0] in PUNCT_NO_SPACE:
                        cur = cur[:-1]
                        for mk in marks:
                            if mk[0] > len(cur):
                                mk[0] = len(cur)
                for i in waiting:
                    fns[i][0] = len(cur)
                waiting = []
                removed = False
            base = len(cur)
            for cp, kind, ident in cmarks:
                marks.append([base + max(cp - shift, 0), kind, ident])
            cur += chunk
        if m is not None:
            removed = True
            if m.group(1) is not None:
                fns.append([len(cur), int(m.group(1)), int(m.group(2))])
                waiting.append(len(fns) - 1)
    for i in waiting:
        fns[i][0] = len(cur)
    lead = len(cur) - len(cur.lstrip())
    if lead:
        cur = cur[lead:]
        for f in fns:
            f[0] = max(0, f[0] - lead)
        for mk in marks:
            mk[0] = max(0, mk[0] - lead)
    cur = cur.rstrip()
    for mk in marks:
        mk[0] = min(mk[0], len(cur))
    return cur, [(min(a, len(cur)), r, n) for a, r, n in fns], marks


SPAN_TRIM_N = "،,:؛;."


def spans_from_marks(arabic, marks, stats, carry, closes_soon):
    """-> (narrators, groups, carry_out): spans of the cleaned text, trimmed of surrounding white space.
    carry = {"N": (id, records), "G": (id, records)}: a link / matn group still open at the end of the previous record
    (a matn group can run over several records: Bukhari's mu'allaq pieces "١٨٨ - وقال أبو موسى").  A span cut by a record
    boundary becomes one piece per record, each with the same key."""
    out = {"N": [], "G": []}
    stack = {"N": [], "G": []}

    def close(t, st, en, ident):
        edge = SPAN_TRIM_N if t == "N" else ""      # a narrator span is the name only: Shamela often puts the "،" / ":" inside the link
        while st < en and (arabic[st].isspace() or arabic[st] in edge):
            st += 1
        while en > st and (arabic[en - 1].isspace() or arabic[en - 1] in edge):
            en -= 1
        if ident is None:
            stats["span_lost_key"] += 1
        elif st < en or t == "G":
            # an empty matn group (Muslim: "بِمِثْلِهِ" with the matn left out) is kept as a zero-length span, its
            # key still ties the record to the group; an empty narrator name is dropped
            out[t].append((st, max(st, en), ident))
            stats["span_empty_group"] += (st >= en)
        else:
            stats["span_empty"] += 1

    first = {}
    for pos, kind, ident in marks:
        first.setdefault(kind[0], kind)
    carry_out = {}
    for t in "NG":
        c = carry.get(t)
        if c is None:
            continue
        if t not in first and c[1] < 5 and closes_soon(t):     # the whole record lies inside the open span
            close(t, 0, len(arabic), c[0])
            carry_out[t] = (c[0], c[1] + 1)
            stats["span_continued"] += 1
        elif t in first and first[t][1] == "O":          # the source never closed it
            stats["span_unbalanced"] += 1
    for pos, kind, ident in marks:
        t = kind[0]
        if kind[1] == "O":
            stack[t].append((pos, ident))
        elif stack[t]:
            st, i = stack[t].pop()
            close(t, st, pos, i)
        else:                               # closes something opened in a previous record
            stats["span_open_before"] += 1
            close(t, 0, pos, carry[t][0] if carry.get(t) else None)
            carry_out.pop(t, None)
    for t in "NG":
        for st, i in stack[t]:              # runs on into the next record
            stats["span_open_after"] += 1
            close(t, st, len(arabic), i)
            carry_out[t] = (i, 1)
    for t in "NG":
        out[t].sort()
    return out["N"], out["G"], carry_out


# ---------------------------------------------------------------------------------------------------------------
# per book
# ---------------------------------------------------------------------------------------------------------------
def repair_numbers(records, stats):
    """Typos in the edition's numbers (the Shamela text has "65" for 653, "907" for 861): a run of up to 3 numbers that
    are far (> 20) from the last good number while a number close to it follows is set to the last good number + 1
    (or to the same number when the following good number does not leave room).  number_label keeps the printed
    text and the record gets "number_fixed": true."""
    idx = [i for i, r in enumerate(records) if r["num"] is not None]
    k, last_good = 0, None
    while k < len(idx):
        r = records[idx[k]]
        if last_good is None or abs(r["num"] - last_good) <= 20:
            last_good = r["num_end"] if r["num_end"] is not None else r["num"]
            k += 1
            continue
        j = next((j for j in range(k + 1, min(k + 4, len(idx))) if abs(records[idx[j]]["num"] - last_good) <= 20), None)
        if j is None:                       # a real jump of the numbering
            last_good = r["num_end"] if r["num_end"] is not None else r["num"]
            k += 1
            continue
        room = records[idx[j]]["num"] - last_good - 1
        for m in range(k, j):
            rr = records[idx[m]]
            rr["num"] = rr["num_end"] = last_good + 1 if room > 0 else last_good
            rr["fixed"] = True
            stats["numbers_repaired"] += 1
            last_good = rr["num"]
            room -= 1
        k = j


def build_book(slug, bid, compiler):
    global EXTRA_VERB
    EXTRA_VERB = re.compile(r"^[\s\[\(]*مالك(?![ء-ي])") if bid == 28107 else None
    stats = Counter()
    pages = load_tsv(DUMP_DIR / f"{bid}_pages.tsv")
    titles_raw = load_tsv(DUMP_DIR / f"{bid}_titles.tsv")
    foots = load_tsv(DUMP_DIR / f"{bid}_foot.tsv")
    db = sqlite3.connect(BOOK_DIR / f"{bid % 1000:03d}" / f"{bid}.db")
    page_rows = {}
    last_pg = 0
    for pid, part, pg in db.execute("select id, part, page from page order by id"):
        if pg is None:
            pg = last_pg
        last_pg = pg
        page_rows[pid] = (part if part is not None else "", pg)
    title_rows = db.execute("select id, page, parent from title order by id").fetchall()
    parent = {tid: par for tid, _, par in title_rows}
    title_page = {tid: pg for tid, pg, _ in title_rows}
    title_name = {tid: clean_plain(titles_raw.get(tid, "")) for tid, _, _ in title_rows}

    def depth(tid):
        d = 0
        seen = set()
        while parent.get(tid, 0) and tid not in seen:
            seen.add(tid)
            tid = parent[tid]
            d += 1
        return d

    depth_of = {tid: depth(tid) for tid in parent}

    # ---- which titles have inline anchors?
    span_ids = set()
    for body in pages.values():
        for m in re.finditer(r"id\s*=\s*[\"']?toc-(\d+)", body):
            span_ids.add(int(m.group(1)))
    missing_anchor = {tid for tid in parent if tid not in span_ids}
    by_page_missing = {}
    for tid in sorted(missing_anchor):
        by_page_missing.setdefault(title_page[tid], []).append(tid)
    stats["titles"] = len(parent)
    stats["links_dump"] = sum(len(RE_A_ANY.findall(body)) for body in pages.values())
    stats["groups_dump"] = sum(len(RE_G_ANY.findall(body)) for body in pages.values())
    stats["titles_no_anchor"] = len(missing_anchor)

    # ---- build the stream
    parts = []
    row_start, row_ids = [], []
    foot_by_row = {}     # rowid -> {n: text}
    last_foot_item = None
    foot_pre_lost = 0
    pos = 0
    tail = ""
    foot_continuations = 0
    for rid in sorted(pages):
        raw = pages[rid]
        pre_titles = "".join(f"{T_O}{t}{T_C}\n" for t in by_page_missing.get(rid, []))
        if not raw.strip() and not pre_titles:
            continue
        parsed_foot = parse_foot(foots[rid], stats) if rid in foots else None
        t = pre_titles + prep_row(rid, raw, bid, stats, span_ids, parsed_foot[2] if parsed_foot else ())
        if not t.strip():
            continue
        # separator between rows
        if parts:
            first = RE_LP.sub("", t[:16])[:1] if t[:1] in LP_CHARS else t[:1]
            nl = False
            if tail.endswith("\n") or first == T_O:
                nl = True
            else:
                ln = t.split("\n", 1)[0]
                plain = RE_PH_ANY.sub("", ln[:60])
                if RE_MARK.match(t[:80]) and re.match(r"^[\[\(]?[" + DIG + r"]+[ \t]*[^\s" + DIG + r"]{0,3}[ \t]*[-–—\]\)]", plain):
                    nl = True
                elif VERB_START.match(norm_ar(plain)[:40]) and not tail.endswith(("،", ":", ",")):
                    nl = True
                elif tail and tail[-1] in ".؟!»\"" and first not in "،.؛:؟,)»\"":
                    nl = True
            if nl:
                if not tail.endswith("\n"):
                    parts.append("\n")
                    pos += 1
                    tail = "\n"
            elif not tail.endswith(" "):
                parts.append(" ")
                pos += 1
                tail = " "
        row_start.append(pos)
        row_ids.append(rid)
        parts.append(t)
        pos += len(t)
        tail = t[-1:]
        if tail in LP_CHARS:       # link placeholders are transparent for the row-join decisions
            tail = RE_LP.sub("", t[-40:])[-1:]
        # footnotes of this row
        if parsed_foot:
            pre, items, _plain = parsed_foot
            if pre:
                if last_foot_item is not None:
                    foot_by_row[last_foot_item[0]][last_foot_item[1]] += "\n" + pre
                    foot_continuations += 1
                else:
                    foot_pre_lost += 1
            foot_by_row[rid] = items
            if items:
                last_foot_item = (rid, list(items)[-1])
            stats["foot_items"] += len(items)
    S = "".join(parts)
    stats["foot_continuations"] = foot_continuations
    stats["foot_pre_lost"] = foot_pre_lost

    # ---- printed page events
    events = [(s, page_rows.get(r, ("", 0))[0], page_rows.get(r, ("", 0))[1]) for s, r in zip(row_start, row_ids)]
    pm_re = re.compile(f"{P_O}(\\d+){P_C}")
    idx_row = 0
    cur_page = None
    marker_events = []
    for m in pm_re.finditer(S):
        i = bisect.bisect_right(row_start, m.start()) - 1
        vol, pg = page_rows.get(row_ids[i], ("", 0))
        base = pg
        # running page inside this row
        for s2, v2, p2 in reversed(marker_events):
            if s2 >= row_start[i]:
                base = p2
                break
        n = int(m.group(1))
        if base < n <= base + 5:
            marker_events.append((m.start(), vol, n))
    events += marker_events
    events.sort(key=lambda e: e[0])
    ev_pos = [e[0] for e in events]

    def page_at(offset):
        i = bisect.bisect_right(ev_pos, offset) - 1
        if i < 0:
            return ("", 0)
        return events[i][1], events[i][2]

    # ---- split into title-delimited segments
    seg_re = re.compile(f"\\n?{T_O}(\\d+){T_C}\\n?")
    segs = []   # (start, end, title_id)
    prev_end, prev_tid = 0, None
    for m in seg_re.finditer(S):
        segs.append((prev_end, m.start(), prev_tid))
        prev_end, prev_tid = m.end(), int(m.group(1))
    segs.append((prev_end, len(S), prev_tid))

    # ---- candidate markers
    cands = []   # (abs_start, abs_end, num, label, has_close, verb, seg_index)
    for si, (a, b, tid) in enumerate(segs):
        seg = S[a:b]
        for m in (RE_MARK_X if bid in EXT_BOOKS else RE_MARK).finditer(seg):
            close = m.group("close")
            opn = m.group("open")
            gd = m.groupdict()
            mend, num2, label2 = m.end(), None, None
            if gd.get("pair"):
                if gd["pair"] == "(م)":               # "125 - (م) أخبرنا": the extra chain of hadith 125
                    label2 = " - (م)"
                elif bid in PAIR_KEY:
                    num2, label2 = to_int(gd["pair"].strip("()")), " - " + gd["pair"]
                else:
                    mend = m.start("pair")
            elif bid in DOUBLE_NUM_BOOKS and close not in ("]", ")"):
                # chain the following numbers: "408 - 409 -", "5709 - 5710 - 5711 -", "5911 5912 -", "[408 - 409]"
                last_n = to_int(m.group("num"))
                label2 = ""
                while True:
                    mm = RE_NEXT_NUM.match(seg, mend)
                    if not mm or to_int(mm.group("n")) != last_n + 1 or not mm.group("c"):
                        break
                    last_n = num2 = to_int(mm.group("n"))
                    label2 += " - " + mm.group("n")
                    close = mm.group("c")
                    mend = mm.end()
            after = seg[mend:mend + 80]
            # bracket / paren forms need their closing bracket
            if opn and close not in ("]", ")"):
                continue
            if not opn and close in ("]", ")"):
                continue
            suf = re.sub(r"\s+", " ", m.group("suf") or "").strip()
            label = m.group("num") + ((" " + suf) if suf and not suf.startswith("/") else suf)
            numv = to_int(m.group("num"))
            sm = re.match(r"^/\s*([" + DIG + r"]+)$", suf)
            if sm and to_int(sm.group(1)) > numv and numv < 20:
                # "1/ 830 -" is hadith 830/1 written right-to-left
                numv = to_int(sm.group(1))
            num_end = numv
            if label2 and num2 is None and gd.get("pair"):
                label += label2
            if num2 is not None:
                label += label2
                if gd.get("pair"):
                    if PAIR_KEY[bid] == "second":
                        numv = num_end = num2
                else:
                    num_end = num2
            cands.append((a + m.start(), a + mend, numv, label,
                          close is not None, has_verb(after), si, num_end))

    def numbered_selection(cands):
        acc = []
        last = None
        n = len(cands)
        for i, c in enumerate(cands):
            s, e, num, label, closed, verb, si, num_end = c
            ok = False
            if last is None:
                nxt = cands[i + 1][2] if i + 1 < n else None
                ok = closed and num <= 5 and (verb or (nxt is not None and 0 < nxt - num <= 3 and bid not in STRICT_START))
                if bid in STRICT_START:      # the editor's introduction has numbered lists too: the first hadith needs an isnad verb
                    ok = closed and num <= 5 and (verb or (bid in LOOSE_START and bool(VERB_ANY.search(norm_ar(RE_PH_ANY.sub("", S[e:e + 160]))))))
            else:
                near = last < num <= last + 3
                if closed and (near or (verb and num <= last)):
                    ok = True
                elif verb and (closed or near):
                    ok = True
                elif closed and verb is False and last < num <= last + 10:
                    ok = False
                if not ok and verb and closed and num > last + 3:
                    nxt = cands[i + 1][2] if i + 1 < n else None
                    ok = nxt is None or 0 < nxt - num <= 3
            if ok:
                acc.append(c)
                last = num_end
        return acc

    def recover_inline(selected):
        out = []
        for i, c in enumerate(selected):
            out.append(c)
            nxt = selected[i + 1] if i + 1 < len(selected) else None
            if nxt is None or c[7] is None or nxt[2] is None:
                continue
            limit = nxt[0] if nxt[6] == c[6] else segs[c[6]][1]     # a title ends the search window
            first_missing, last_missing = c[7] + 1, nxt[2] - 1
            if not (0 < last_missing - first_missing + 1 <= 15):
                continue
            pos = c[1]
            for want in range(first_missing, last_missing + 1):
                if PAIR_KEY.get(bid) == "second":
                    rx = (r"(?<![" + DIG + r"])(?P<n>[" + DIG + r"]+)[ \t]*[-–—]+[ \t]*(?P<p>\((?:" + str(want)
                          + "|" + str(want).translate(TO_AR) + r")\))[ \t]*")
                else:
                    rx = (r"(?<![" + DIG + r"])(?P<n>" + str(want) + "|" + str(want).translate(TO_AR)
                          + r")[ \t]*[-–—]+[ \t]*(?P<p>\([" + DIG + r"]+\)[ \t]*)?")
                m = re.compile(rx).search(S, pos, limit)
                if not m or (m.start() > 0 and S[m.start() - 1] not in " \n،.؛:»)]\"" + FN_C + P_C + NA_C + G_C):
                    continue
                label = m.group("n") + ((" - " + m.group("p").strip()) if m.group("p") else "")
                if PAIR_KEY.get(bid) == "second":
                    label = m.group("n") + " - " + m.group("p")
                out.append((m.start(), m.end(), want, label, True, has_verb(S[m.end():m.end() + 80]), c[6], want))
                stats["inline_recovered"] += 1
                pos = m.end()
        return out

    mode = "numbers"
    selected = numbered_selection(cands)
    if bid in INLINE_BOOKS:
        selected = recover_inline(selected)
    if len(selected) < 50:
        mode = "isnad"
        selected = []
        for si, (a, b, tid) in enumerate(segs):
            seg = S[a:b]
            for m in re.finditer(r"^[ \t]*", seg, re.M):
                if has_verb(seg[m.end():m.end() + 80]):
                    selected.append((a + m.start(), a + m.end(), None, None, False, True, si, None))
    stats["candidates"] = len(cands)
    if os.environ.get("SHAMELA_DEBUG_CANDS"):
        for c in cands[:int(os.environ["SHAMELA_DEBUG_CANDS"])]:
            print("CAND", c[2], repr(c[3]), "closed" if c[4] else "open", "verb" if c[5] else "noverb", repr(S[c[1]:c[1] + 40]))

    # ---- cut records
    # records: dict(start, end, text_start, num, label, tid)
    sel_by_seg = {}
    for c in selected:
        sel_by_seg.setdefault(c[6], []).append(c)
    raw_recs = []
    started = False
    for si, (a, b, tid) in enumerate(segs):
        cs = sel_by_seg.get(si, [])
        first_start = cs[0][0] if cs else b
        if started and first_start > a:       # prose right after a title, before the first numbered hadith
            raw_recs.append(dict(start=a, ts=a, end=first_start, num=None, num_end=None, label=None, tid=tid))
        for j, c in enumerate(cs):
            end = cs[j + 1][0] if j + 1 < len(cs) else b
            raw_recs.append(dict(start=c[0], ts=c[1], end=end, num=c[2], num_end=c[7], label=c[3], tid=tid))
            started = True

    # ---- clean + footnotes
    foot_total_marks = 0
    records = []
    carry = {}
    for rr in raw_recs:
        text = S[rr["ts"]:rr["end"]]
        arabic, fns, marks = finish_text(text, foot_by_row, None, stats)
        if not arabic:
            stats["empty_dropped"] += 1
            continue
        if rr["num"] is None and len(arabic.replace("﷽", "").strip()) < 4:
            stats["basmala_dropped"] += 1
            continue
        if bid in NEW_BOOKS and rr["num"] is None and RE_ONLY_BRACKET_NUMS.match(arabic):
            # Musannaf Ibn Abi Shayba: "[٣٤]" alone after a chapter title is a page number of the print, not text
            stats["page_marks_dropped"] += 1
            continue
        fnotes = []
        for at, rowid, n in fns:
            stats["marks_in_records"] += 1
            items = foot_by_row.get(rowid, {})
            if n in items:
                stats["marks_matched"] += 1
                stats["_used_" + f"{rowid}:{n}"] += 1
                fnotes.append({"at": at, "text": items[n]})
            else:
                stats["marks_no_foot"] += 1
        vol, p1 = page_at(rr["start"])
        _, p2 = page_at(max(rr["start"], rr["end"] - 1))
        records.append(dict(num=rr["num"], num_end=rr["num_end"], label=rr["label"], tid=rr["tid"], vol=vol, page=p1,
                            page_end=max(p1, p2), arabic=arabic, footnotes=fnotes, marks=marks))
    # junk and elided-number records (new books only)
    kept = []
    for rec in records:
        if bid in NEW_BOOKS and rec["num"] is None and RE_ONLY_FN_MARK.match(rec["arabic"]):
            stats["fn_mark_records_dropped"] += 1
            continue
        if rec["num"] is None:
            me = RE_ELIDED_NUM.match(rec["arabic"])
            if me and bid in NEW_BOOKS:
                k = me.end()
                rec["label"] = re.sub(r"\s+", " ", me.group("lab")).strip()
                rec["arabic"] = rec["arabic"][k:]
                rec["footnotes"] = [{"at": max(0, f["at"] - k), "text": f["text"]} for f in rec["footnotes"]]
                rec["marks"] = [[max(0, pos - k), kd, i] for pos, kd, i in rec["marks"]]
                stats["elided_number_records"] += 1
        kept.append(rec)
    records[:] = kept
    # back matter: the editor's afterword glued to the book's last hadith (Muslim 711), cut at the "_____" rule
    if bid in NEW_BOOKS:
        for rec in reversed(records):
            if rec["num"] is None:
                continue
            mb = RE_BACK_MATTER.search(rec["arabic"])
            if mb:
                cut = mb.start()
                stats["back_matter_chars"] = len(rec["arabic"]) - cut
                rec["arabic"] = rec["arabic"][:cut]
                rec["footnotes"] = [f for f in rec["footnotes"] if f["at"] <= cut]
                rec["marks"] = [[min(pos, cut), k, i] for pos, k, i in rec["marks"] if pos < cut]
            break
    # narrator / matn-group spans (a pass of its own: a span may run over several records)
    first_kind = [{} for _ in records]
    for k, rec in enumerate(records):
        for pos, kind, ident in rec["marks"]:
            first_kind[k].setdefault(kind[0], kind[1])
    carry = {}
    for k, rec in enumerate(records):
        def closes_soon(t, k=k):
            for j in range(k + 1, min(k + 6, len(records))):
                fk = first_kind[j].get(t)
                if fk:
                    return fk == "C"
            return False
        narr, grp, carry = spans_from_marks(rec["arabic"], rec["marks"], stats, carry, closes_soon)
        stats["narrators_emitted"] += len(narr)
        stats["groups_emitted"] += len(grp)
        rec["narrators"] = [{"start": a, "end": b, "man": i} for a, b, i in narr]
        rec["groups"] = [{"start": a, "end": b, "key": i} for a, b, i in grp]
        del rec["marks"]
    # marks in skipped text (intro, titles) and total marks
    all_marks = set()
    for m in re.finditer(f"{FN_O}(\\d+):(\\d+){FN_C}", S):
        all_marks.add((int(m.group(1)), int(m.group(2))))
        stats["marks_total"] += 1
    used = {k[6:] for k in stats if k.startswith("_used_")}
    stats["marks_skipped_text"] = stats["marks_total"] - stats["marks_in_records"]
    unused_items = 0
    for rowid, items in foot_by_row.items():
        for n in items:
            if (rowid, n) not in all_marks:
                unused_items += 1
    stats["foot_items_no_mark"] = unused_items
    for k in [k for k in stats if k.startswith("_used_")]:
        del stats[k]

    if bid in REPAIR_BOOKS:
        repair_numbers(records, stats)
    return dict(slug=slug, bid=bid, compiler=compiler, mode=mode, records=records, stats=stats, parent=parent,
                linked=bool(stats["links_dump"] or stats["groups_dump"]),
                depth_of=depth_of, title_name=title_name, S_len=len(S))


def assign_chapters(b):
    """Pick the chapter depth (top-level titles; descend while there are fewer than 3) and bab names."""
    recs, parent, depth_of, tname = b["records"], b["parent"], b["depth_of"], b["title_name"]

    def anc(tid, d):
        while tid is not None and depth_of.get(tid, 0) > d:
            tid = parent.get(tid) or None
        return tid

    max_depth = max(depth_of.values()) if depth_of else 0
    chosen = 0
    for d in range(0, max_depth + 1):
        chapters = {anc(r["tid"], d) for r in recs if r["tid"] is not None}
        chosen = d
        if len(chapters) >= 3:
            break
    b["chapter_depth"] = chosen
    for r in recs:
        tid = r["tid"]
        r["chapter_id"] = anc(tid, chosen) if tid is not None else None
        r["bab"] = tname.get(tid) if (tid is not None and depth_of.get(tid, 0) > chosen and tname.get(tid)) else None
    return chosen


def write_book(b, title, edition):
    slug = b["slug"]
    recs = b["records"]
    assign_chapters(b)
    out = OUT_DIR / slug
    out.mkdir(parents=True, exist_ok=True)
    for f in out.glob("*.json"):
        f.unlink()
    order = []
    for r in recs:
        if r["chapter_id"] not in order:
            order.append(r["chapter_id"])
    index = []
    nrec = 0
    for ci, cid in enumerate(order, 1):
        items = []
        for r in recs:
            if r["chapter_id"] != cid:
                continue
            nrec += 1
            r["id"] = nrec
            items.append(r)
        out_items = []
        for r in items:
            kind = "hadith" if (r["num"] is not None or has_verb(r["arabic"])) else "text"
            r["kind"] = kind
            out_items.append({
                "id": r["id"], "number": r["num"], "number_label": r["label"], "kind": kind, "bab": r["bab"],
                "vol": r["vol"], "page": r["page"], "page_end": r["page_end"], "arabic": r["arabic"],
                "footnotes": r["footnotes"]}
            )
            if r.get("fixed"):
                out_items[-1]["number_fixed"] = True
            if b["linked"]:
                out_items[-1]["narrators"] = r["narrators"]
                out_items[-1]["groups"] = r["groups"]
        (out / f"{ci}.json").write_text(json.dumps(out_items, ensure_ascii=False, indent=1), encoding="utf-8")
        name = b["title_name"].get(cid, "") if cid is not None else ""
        index.append({"chapter": ci, "file": f"{ci}.json", "name_ar": name or "مقدمة", "count": len(items)})
    # ids must follow book order, not chapter order: renumber (chapters are contiguous in the stream anyway)
    (out / "index.json").write_text(json.dumps(index, ensure_ascii=False, indent=1), encoding="utf-8")
    numbered = sum(1 for r in recs if r["num"] is not None)
    (out / "book.json").write_text(json.dumps({
        "slug": slug, "title": title, "shamela_id": b["bid"], "edition": edition, "compiler": b["compiler"],
        "records": len(recs), "numbered": numbered}, ensure_ascii=False, indent=1), encoding="utf-8")


def self_check(b):
    recs, st = b["records"], b["stats"]
    nums = [r["num"] for r in recs if r["num"] is not None]
    ends = [r["num_end"] if r["num_end"] is not None else r["num"] for r in recs if r["num"] is not None]
    gaps = gap_missing = dups = ooo = 0
    seen = set()
    for i, n in enumerate(nums):
        if n in seen:
            dups += 1
        seen.add(n)
        if i:
            if n < nums[i - 1]:
                ooo += 1
            elif n > ends[i - 1] + 1:
                gaps += 1
                gap_missing += n - ends[i - 1] - 1
    hadith = [r for r in recs if r["num"] is not None or has_verb(r["arabic"])]
    noverb = [r for r in hadith if not VERB_ANY.search(norm_ar(r["arabic"][:200]))]
    kinds = Counter("hadith" if (r["num"] is not None or has_verb(r["arabic"])) else "text" for r in recs)
    print(f"  mode={b['mode']} chapter_depth={b['chapter_depth']} records={len(recs)} numbered={len(nums)} "
          f"text={kinds['text']} empty_dropped={st['empty_dropped']} candidates={st['candidates']}")
    print(f"  numbers: gaps={gaps} (missing {gap_missing}) dups={dups} out_of_order={ooo} "
          f"first={nums[0] if nums else None} last={nums[-1] if nums else None}")
    print(f"  no isnad verb in first 200 chars: {len(noverb)} of {len(hadith)} hadith records")
    print(f"  footnotes: marks_total={st['marks_total']} in_records={st['marks_in_records']} matched={st['marks_matched']} "
          f"no_foot_item={st['marks_no_foot']} in_skipped_text={st['marks_skipped_text']} marks_in_titles={st['marks_in_titles']}; "
          f"foot_items={st['foot_items']} no_mark={st['foot_items_no_mark']} continuations={st['foot_continuations']} "
          f"lost_continuations={st['foot_pre_lost']}")
    if b["linked"]:
        print(f"  links: in dump {st['links_dump']} anchors / {st['groups_dump']} matn groups -> emitted "
              f"{st['narrators_emitted']} narrators / {st['groups_emitted']} groups; empty={st['span_empty']} "
              f"open_before={st['span_open_before']} open_after={st['span_open_after']} "
              f"continued={st['span_continued']} unbalanced={st['span_unbalanced']} lost_key={st['span_lost_key']} empty_groups={st['span_empty_group']}")
    print(f"  back_matter_cut={st['back_matter_chars']} page_marks_dropped={st['page_marks_dropped']} inline_recovered={st['inline_recovered']} numbers_repaired={st['numbers_repaired']}")
    print(f"  titles={st['titles']} without_inline_anchor={st['titles_no_anchor']} "
          f"lead_frag_candidates={st['lead_frag_candidates']} fixed={st['lead_frag_fixed']}")
    return noverb, gaps, dups, ooo


def main():
    sample_dir = os.environ.get("SHAMELA_NOTES")
    meta_title = {}
    for slug, bid, compiler in BOOKS:
        if ONLY and slug not in ONLY:
            continue
        print(f"== {slug} ({bid})")
        b = build_book(slug, bid, compiler)
        title, edition = book_titles_meta(MASTER_DB, bid)
        write_book(b, title, edition)
        noverb, *_ = self_check(b)
        if sample_dir:
            os.makedirs(sample_dir, exist_ok=True)
            with open(os.path.join(sample_dir, f"{slug}_noverb.txt"), "w", encoding="utf-8") as f:
                for r in noverb[:60]:
                    f.write(f"[{r['num']}] p{r['page']}: {r['arabic'][:200]}\n")


if __name__ == "__main__":
    main()
