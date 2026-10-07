#!/usr/bin/env python3
"""Measure the Ilal findings on a fixed set of well-known hadiths, so a rule, resolver or data change can be compared
with the run before it. Standard library only; needs the API running (default http://localhost:5147).

    python scripts/eval_ilal_pages.py --save before.json
    ... change something, restart the API ...
    python scripts/eval_ilal_pages.py --save after.json --compare before.json

The pages are found by a matn search, so a rebuilt database (new Guids) still works. A page is the first --limit
results of the search, which is what the takhreej page gathers for that wording. The numbers that matter for the
backlog (docs/backlog.md, «Suggested order»): matn findings (Ziyadah, Nakarah, Shudhudh, Idtirab), how many of them
are «قادحة», the calculated grade, and how many narrations were set apart as shawahid.
"""
import argparse
import json
import sys
import urllib.parse
import urllib.request
from collections import Counter

PAGES = {
    "tahur": "الطهور شطر الإيمان",
    "niyyat": "إنما الأعمال بالنيات",
    "jannah": "الجنة أقرب إلى أحدكم من شراك نعله",
    "mughira": "كان إذا ذهب المذهب أبعد",
    "nasiha": "الدين النصيحة",
    "halal": "الحلال بين والحرام بين",
    "darar": "لا ضرر ولا ضرار",
    "kadhib": "من كذب علي متعمدا فليتبوأ مقعده من النار",
    "hayaa": "الحياء من الإيمان",
}
MATN_TYPES = ("Ziyadah", "Nakarah", "Shudhudh", "Idtirab")


def get(url, timeout=300):
    return json.load(urllib.request.urlopen(url, timeout=timeout))


def measure(api, query, limit):
    found = get(f"{api}/api/Search?" + urllib.parse.urlencode({"query": query, "scope": "Matn"}), 120)
    ids = ",".join(h["id"] for h in found[:limit])
    tree = get(f"{api}/api/Takhreej?ids={ids}")
    report = tree.get("ilalReport") or {}
    findings = report.get("findings", [])
    matn = [f for f in findings if f["type"] in MATN_TYPES]
    return {
        "sources": len(tree.get("sources", [])),
        "turuq": len(report.get("turuq", [])),
        "shawahid": sum(1 for t in report.get("turuq", []) if t.get("isShahid")),
        "grade": tree.get("calculatedGrade"),
        "findings": len(findings),
        "matn": len(matn),
        "matnQadihah": sum(1 for f in matn if f["severity"] == "Qadihah"),
        "byType": dict(Counter(f"{f['type']}/{f['severity']}" for f in findings)),
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--api", default="http://localhost:5147")
    ap.add_argument("--limit", type=int, default=30, help="narrations per page (default 30)")
    ap.add_argument("--save", help="write the measurements to this JSON file")
    ap.add_argument("--compare", help="a file written by --save: print the change against it")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    before = json.load(open(args.compare, encoding="utf-8")) if args.compare else {}
    now = {}
    print(f"{'page':9} {'src':>4} {'shw':>4} {'grade':8} {'all':>4} {'matn':>5} {'matn قادحة':>11}")
    for name, query in PAGES.items():
        m = now[name] = measure(args.api, query, args.limit)
        line = f"{name:9} {m['sources']:4} {m['shawahid']:4} {str(m['grade']):8} {m['findings']:4} {m['matn']:5} {m['matnQadihah']:11}"
        if name in before:
            b = before[name]
            line += f"   was: all {b['findings']}, matn {b['matn']}, قادحة {b['matnQadihah']}, grade {b['grade']}"
        print(line)

    total = {k: sum(p[k] for p in now.values()) for k in ("findings", "matn", "matnQadihah")}
    print(f"{'total':9} {'':4} {'':4} {'':8} {total['findings']:4} {total['matn']:5} {total['matnQadihah']:11}", end="")
    if before:
        old = {k: sum(p[k] for p in before.values() if k in p) for k in total}
        print(f"   was: all {old['findings']}, matn {old['matn']}, قادحة {old['matnQadihah']}", end="")
    print()

    if args.save:
        json.dump(now, open(args.save, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print("saved", args.save)


if __name__ == "__main__":
    main()
