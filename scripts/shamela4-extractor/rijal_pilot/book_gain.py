"""Which candidate book can resolve how many of OUR missing names? (run from data/shamela_rijal, after dumping the books
with ShamelaLuceneDumper into dump/ and exporting the chains: CHAINS dir below)

Missing = the undecided names with no candidate (how == 'missing'), multi-word, inside chains (chains_s5).
A book 'has' a name when an entry heading of the book (a title, or the start of an entry in the page text) contains all
the name's tokens and starts with its first token. Counted per distinct name and per occurrence."""
import csv, glob, json, os, re, sys
from collections import Counter, defaultdict
sys.stdout.reconfigure(encoding="utf-8")
csv.field_size_limit(10**9)
D = "dump"
BOOKS = {1714: "تاريخ أصبهان", 11077: "تاريخ جرجان", 1079: "المنتخب من السياق (إيران)", 12528: "المنتخب من السياق (فكر)",
         4017: "تلخيص تاريخ نيسابور", 1005: "نيسابور: شيوخ الحاكم", 843: "الإكمال (المعلمي)", 22659: "الإكمال (العلمية)",
         5797: "تكملة الإكمال (ابن نقطة)", 5812: "توضيح المشتبه", 9076: "المتفق والمفترق", 4182: "طبقات المحدثين بأصبهان",
         12581: "المؤتلف والمختلف (الدارقطني)", 12531: "معجم شيوخ الذهبي", 12750: "معجم شيوخ ابن عساكر", 1583: "تذكرة الحفاظ",
         10906: "سير أعلام النبلاء", 96165: "الثقات لابن حبان", 736: "تاريخ بغداد (مستعمل)", 36357: "لسان الميزان (محلَّل لم يُدمج)"}
STOP = {"بن", "ابن", "ابو", "ابي", "ابا", "بنت", "ال"}

def norm(s):
    s = re.sub(r"[ً-ْٰـ]", "", s)
    s = re.sub("[أإآ]", "ا", s).replace("ى", "ي").replace("ة", "ه")
    s = re.sub(r"\bال(?=\S)", "", s)
    s = re.sub(r"[^ء-ي ]", " ", s)
    return re.sub(r"\s+", " ", s).strip()

def toks(s):
    return [w for w in norm(s).split() if w not in ("بن", "ابن")]

ENTRY_START = re.compile(r"(?:^|\\n|\n)\s*\(?[0-9٠-٩]{1,6}\)?\s*[-–.ـ:]\s*([^\n\\]{4,140})")

def heads(b):
    out = []
    tf = f"{D}/{b}_titles.tsv"
    if os.path.exists(tf):
        out += [r[-1] for r in csv.reader(open(tf, encoding="utf-8"), delimiter="\t") if r]
    pf = f"{D}/{b}_pages.tsv"
    if os.path.exists(pf):
        for r in csv.reader(open(pf, encoding="utf-8"), delimiter="\t"):
            if r:
                out += ENTRY_START.findall(r[-1])
    return out

# the missing names
miss = Counter()
for f in sorted(glob.glob("chains_s5/*.json")):
    c = json.load(open(f, encoding="utf-8"))
    if not isinstance(c, dict) or "chains" not in c:
        continue
    for ch in c["chains"]:
        names = ch.get("names") or []
        res = [x for x in names if x.get("id")]
        if len(res) < 2:
            continue
        last = names.index(res[-1])
        for x in names[:last]:
            if x.get("id") or x.get("how") != "missing":
                continue
            n = x["n"].strip()
            t = toks(n)
            if len(t) >= 2 and not re.search(r"[\[\]()ز]|حدثنا|اخبرنا|يحدث|قال|انه|وبه|وبإسناده", n) and n not in ("رجل",):
                miss[n] += 1
tot_n, tot_o = len(miss), sum(miss.values())
print(f"missing multi-word names: {tot_n} distinct, {tot_o} occurrences")

index = {}
for b in BOOKS:
    hs = heads(b)
    if not hs:
        continue
    byfirst = defaultdict(list)
    for h in hs:
        t = toks(h)[:14]
        if t:
            byfirst[t[0]].append(set(t))
    index[b] = byfirst
    print(f"  {b:6} {BOOKS[b]:34} headings read: {len(hs):6}")

print(f"\n{'book':36} {'names':>7} {'occurrences':>12} {'%occ':>6}")
hit = {}
for b, byfirst in index.items():
    dn = do = 0
    ok = set()
    for n, k in miss.items():
        t = toks(n)
        if any(set(t) <= h for h in byfirst.get(t[0], ())):
            dn += 1; do += k; ok.add(n)
    hit[b] = ok
    print(f"{BOOKS[b]:36} {dn:7} {do:12} {100*do/tot_o:5.1f}%")
allok = set().union(*hit.values())
print(f"{'any of the above':36} {len(allok):7} {sum(miss[n] for n in allok):12} {100*sum(miss[n] for n in allok)/tot_o:5.1f}%")
new_only = [b for b in index if b not in (736, 36357)]
u = set().union(*(hit[b] for b in new_only))
print(f"{'any except Baghdad/Lisan':36} {len(u):7} {sum(miss[n] for n in u):12} {100*sum(miss[n] for n in u)/tot_o:5.1f}%")
# greedy order: which book adds the most NEW occurrences
print("\ngreedy order (new occurrences each adds):")
covered = set(hit.get(736, set()))
rest = [b for b in index if b != 736]
while rest:
    best = max(rest, key=lambda b: sum(miss[n] for n in hit[b] - covered))
    add = sum(miss[n] for n in hit[best] - covered)
    if add == 0:
        break
    covered |= hit[best]
    rest.remove(best)
    print(f"  +{add:6}  {BOOKS[best]}  (total {sum(miss[n] for n in covered)}, {100*sum(miss[n] for n in covered)/tot_o:.1f}%)")
