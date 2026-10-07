"""Where do the resolved chains break? A baseline for the resolver work (docs/backlog.md §1), read from the files
`export_chains_shamela.py` wrote, so it needs no database.

Run from data/shamela_rijal:
    PYTHONIOENCODING=utf-8 python ../../scripts/shamela4-extractor/rijal_pilot/chain_report.py [chains_dir] [--top 45]

For every book and overall, over the chains with at least two resolved narrators:
  ->companion   the last name the resolver decided is a Companion (rank 1 in registry.json)
  cut           it is someone else: a Successor or later (often a real mawquf report in the musannafs)
  junk at end   undecided names after the last decided one (the first words of the matn read as a name; the loader
                skips them, so this is harmless)
  gap inside    an undecided name before the last decided one. The loader makes no link across it
                («A <- ? <- C» must not become «A <- C»), so the chain stops there in the app.
Then the most frequent undecided names inside chains, which is what to fix first.
"""
import argparse
import glob
import json
import os
import sys
from collections import Counter


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("chains_dir", nargs="?", default="chains")
    ap.add_argument("--top", type=int, default=45)
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")

    reg = {r["id"]: r for r in json.load(open("registry.json", encoding="utf-8"))}
    total, gaps = Counter(), Counter()
    rows = []
    for f in sorted(glob.glob(os.path.join(args.chains_dir, "*.json"))):
        data = json.load(open(f, encoding="utf-8"))
        if not isinstance(data, dict) or "chains" not in data:
            continue
        n = Counter()
        for ch in data["chains"]:
            names = ch.get("names") or []
            resolved = [x for x in names if x.get("id")]
            if len(resolved) < 2:
                n["no_chain"] += 1
                continue
            last = names.index(resolved[-1])
            n["companion" if reg.get(resolved[-1]["id"], {}).get("rank") == 1 else "cut"] += 1
            if last < len(names) - 1:
                n["junk"] += 1
            inside = [x["n"] for x in names[:last] if not x.get("id")]
            if inside:
                n["gap"] += 1
                gaps.update(inside)
        rows.append((data["book"], n))
        total.update(n)

    def pct(n, k):
        t = n["companion"] + n["cut"] + n["no_chain"]
        return 100 * n[k] / t if t else 0

    print(f"{'book':30} {'chains':>7} {'->companion':>11} {'cut':>5} {'junk at end':>11} {'gap inside':>10} {'no chain':>9}")
    for book, n in rows + [("ALL", total)]:
        t = n["companion"] + n["cut"] + n["no_chain"]
        print(f"{book:30} {t:7} {pct(n, 'companion'):10.0f}% {pct(n, 'cut'):4.0f}% {pct(n, 'junk'):10.0f}% "
              f"{pct(n, 'gap'):9.0f}% {pct(n, 'no_chain'):8.0f}%")

    all_gaps = sum(gaps.values())
    print(f"\n{all_gaps} undecided names inside chains, {len(gaps)} distinct; the top 50 cover "
          f"{100 * sum(k for _, k in gaps.most_common(50)) / max(all_gaps, 1):.0f}%, the top 200 "
          f"{100 * sum(k for _, k in gaps.most_common(200)) / max(all_gaps, 1):.0f}%")
    for name, k in gaps.most_common(args.top):
        print(f"  {k:6} {name}")


if __name__ == "__main__":
    main()
