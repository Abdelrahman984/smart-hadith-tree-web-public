#!/usr/bin/env python3
"""Measure /api/Search ranking against docs/challenge/search-eval-cases.json.

For every case the runner searches, finds the rank of the hadith the user is looking for (a matn that contains
'target_contains'), and reports hit@1/3/10 and MRR. With --judge it also runs the opt-in AI check on the page and
re-orders by its labels (match, partial, not judged, scattered), reporting how often the target was labelled
'scattered' (the risk of hiding a correct hadith) and whether the model answered.

Usage:
    python scripts/eval_search.py --dry-run                        # validate the cases file
    python scripts/eval_search.py --save current.json              # the running API
    python scripts/eval_search.py --judge --save with_ai.json      # + AI check
    python scripts/eval_search.py --compare baseline.json          # rank changes against a saved run

To get a BEFORE number, run the same command against an API built from the commit before the relevance change
(git worktree add ../baseline <commit>; dotnet run there) and save it as baseline.json.
Standard library only.
"""
import argparse
import concurrent.futures
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CASES_PATH = ROOT / "docs" / "challenge" / "search-eval-cases.json"
LEVEL_ORDER = {"match": 0, "partial": 1, "not-judged": 2, "scattered": 3}
MARKS = re.compile("[ؐ-ًؚ-ٰٟۖ-ۭـ]")


def norm(text):
    t = MARKS.sub("", text or "")
    t = re.sub("[أإآ]", "ا", t).replace("ة", "ه").replace("ى", "ي")
    return re.sub(r"[^ء-ي]+", " ", t).strip()


def load_cases():
    cases = json.loads(CASES_PATH.read_text(encoding="utf-8"))["cases"]
    ids = set()
    for c in cases:
        assert c["id"] not in ids, f"duplicate id {c['id']}"
        ids.add(c["id"])
        assert c["query"].strip() and (c["target_contains"].strip() or c.get("probe")), f"{c['id']}: empty query or target"
    return cases


def http_json(url, body=None):
    """GET (or POST with a JSON body). Waits and retries when the server's per-client rate limit answers 429."""
    for attempt in range(8):
        req = urllib.request.Request(
            url, data=None if body is None else json.dumps(body).encode("utf-8"),
            headers={"Content-Type": "application/json"}, method="GET" if body is None else "POST")
        start = time.perf_counter()
        try:
            with urllib.request.urlopen(req, timeout=180) as res:
                data = json.load(res)
            return data, (time.perf_counter() - start) * 1000
        except urllib.error.HTTPError as e:
            if e.code != 429:
                raise
            wait = 8 * (attempt + 1)
            print(f"      rate limited (429), waiting {wait}s (raise RateLimit__AiPermitPerMinute for evaluation runs)")
            time.sleep(wait)
    raise RuntimeError("still rate limited after several retries")


def rank_of(results, needle):
    n = norm(needle)
    for i, r in enumerate(results, 1):
        if n and n in norm(r.get("matnArabic") or r.get("matnSnippet") or ""):
            return i
    return None


def review_block(case, results, level, reasons, judge_info="", top=10):
    """One Markdown section: the query, the target and the top results with their scores and AI labels."""
    lines = [f"## {case['id']}: {case['query']}", f"Looking for: «{case['target_contains']}»"]
    if judge_info:
        lines.append(judge_info)
    lines.append("")
    for i, r in enumerate(results[:top], 1):
        label = level.get(r["id"], "-")
        reason, quote = reasons.get(r["id"], ("", ""))
        text = (r.get("matnArabic") or r.get("matnSnippet") or "").replace("\n", " ")
        lines += [
            f"### {case['id']}.{i} {r.get('bookName')} #{r.get('hadithNumber')}  |  relevance {r.get('relevancePercent')}%  |  AI: {label}",
            f"- reason (relevance): {r.get('relevanceReason')}",
            f"- AI reason: {reason}" if reason else "- AI reason: -",
            f"- AI quote: «{quote}»" if quote else "- AI quote: -",
            f"- matn: {text[:500]}",
            "- reviewer verdict (match / partial / scattered / unsure): ",
            "",
        ]
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5147/api")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--judge", action="store_true", help="also run the AI check and re-order by its labels")
    ap.add_argument("--judge-top", type=int, default=10, help="how many of the top results the AI check receives (default 10, the size of the review pack)")
    ap.add_argument("--only", help="comma-separated case ids to run, for example S01,S07")
    ap.add_argument("--workers", type=int, default=3, help="cases processed in parallel (default 3)")
    ap.add_argument("--save")
    ap.add_argument("--compare")
    ap.add_argument("--review-pack", help="write a Markdown file with the top results, scores and AI labels for a reviewer (use with --judge)")
    args = ap.parse_args()

    cases = load_cases()
    if args.only:
        wanted = {x.strip() for x in args.only.split(",")}
        cases = [c for c in cases if c["id"] in wanted]
    if args.dry_run:
        print(f"OK: {len(cases)} cases valid")
        return 0

    api = args.api.rstrip("/")

    def run_case(c):
        """Search (and optionally AI-check) one case. Returns everything the report needs."""
        started = time.perf_counter()
        level, reasons, judge_info, judged = {}, {}, "", None
        qs = urllib.parse.urlencode({"query": c["query"], "scope": c.get("scope", 1), "match": c.get("match", 0), "page": 1, "pageSize": 50})
        results, ms = http_json(f"{api}/Search?{qs}")
        probe = bool(c.get("probe"))
        rank = None if probe else rank_of(results, c["target_contains"])
        entry = {"rank": rank, "results": len(results), "probe": probe}
        if rank:
            entry["relevance"] = results[rank - 1].get("relevancePercent")
        decoys = [rank_of(results, d) for d in c.get("decoys_contains", [])]
        decoy_above = bool(rank and any(d and d < rank for d in decoys))

        jr = None
        if args.judge and results:
            sent = results[:args.judge_top]
            judged, _ = http_json(f"{api}/Search/ai-judge", {"query": c["query"], "ids": [r["id"] for r in sent]})
            level = {i["id"]: i["level"] for i in judged["items"]}
            reasons = {i["id"]: (i.get("reason", ""), i.get("quote", "")) for i in judged["items"]}
            judge_info = f"AI check status: {judged['status']}; {judged.get('diagnostics')}"
            # Only the results the AI saw are re-ordered; the rest keep their place after them.
            reordered = sorted(sent, key=lambda r: LEVEL_ORDER.get(level.get(r["id"], "not-judged"), 2)) + results[args.judge_top:]
            jr = None if probe else rank_of(reordered, c["target_contains"])
            entry["rank_after_ai"] = jr
            entry["ai_status"] = judged["status"]
            entry["ai_diagnostics"] = judged.get("diagnostics")
            if rank:
                entry["target_level"] = level.get(results[rank - 1]["id"])
        # Which of the top results contain a relevant wording (when the case lists some), and what the AI said about them.
        relevant = [norm(x) for x in c.get("relevant_contains", [])]
        confusion = None
        if relevant:
            top = results[:args.judge_top]
            flags = [any(x in norm(r.get("matnArabic") or r.get("matnSnippet") or "") for x in relevant) for r in top]
            entry["precision_top"] = round(sum(flags) / max(1, len(top)), 2)
            if judged is not None:
                confusion = {"rel_match": 0, "rel_other": 0, "nonrel_match": 0, "nonrel_other": 0, "not_judged": 0}
                for r, is_rel in zip(top, flags):
                    lv = level.get(r["id"], "not-judged")
                    if lv == "not-judged":
                        confusion["not_judged"] += 1
                    elif lv == "match":
                        confusion["rel_match" if is_rel else "nonrel_match"] += 1
                    else:
                        confusion["rel_other" if is_rel else "nonrel_other"] += 1
                entry["ai_vs_relevant"] = confusion
        if judged is not None:
            entry["labels"] = {k: sum(1 for v in level.values() if v == k) for k in ("match", "partial", "scattered", "not-judged")}
        elapsed = time.perf_counter() - started
        block = review_block(c, results, level, reasons, judge_info) if args.review_pack else ""
        return {"case": c, "entry": entry, "ms": ms, "rank": rank, "jr": jr, "decoy_above": decoy_above,
                "judged": judged, "block": block, "elapsed": elapsed}

    print(f"{len(cases)} cases, {args.workers} in parallel" + (f", AI check on the top {args.judge_top}" if args.judge else ""))
    done = {}
    t0 = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.workers)) as pool:
        futures = {pool.submit(run_case, c): c for c in cases}
        for fut in concurrent.futures.as_completed(futures):
            c = futures[fut]
            r = fut.result()
            done[c["id"]] = r
            e = r["entry"]
            print(f"[{len(done)}/{len(cases)}] {c['id']}  rank={r['rank']}  results={e['results']}  relevance={e.get('relevance')}"
                  + (f"  after_ai={e.get('rank_after_ai')} level={e.get('target_level')} ai={e.get('ai_status')}" if args.judge else "")
                  + f"  ({r['elapsed']:.1f}s)")
            if args.judge and e.get("ai_status") not in (None, "reviewed"):
                print(f"      {e.get('ai_diagnostics')}")
    print(f"Done in {time.perf_counter() - t0:.0f}s")

    out, ranks, judged_ranks, latencies = {}, [], [], []
    precision, conf_total, label_total = [], {}, {}
    scattered_targets, model_status, decoy_above, pack = [], {}, [], []
    for c in cases:  # report in the order of the cases file
        r = done[c["id"]]
        out[c["id"]] = r["entry"]
        if not c.get("probe"):
            ranks.append(r["rank"])
        if "precision_top" in r["entry"]:
            precision.append(r["entry"]["precision_top"])
        for k, v in (r["entry"].get("ai_vs_relevant") or {}).items():
            conf_total[k] = conf_total.get(k, 0) + v
        for k, v in (r["entry"].get("labels") or {}).items():
            label_total[k] = label_total.get(k, 0) + v
        latencies.append(r["ms"])
        if r["decoy_above"]:
            decoy_above.append(c["id"])
        if r["judged"] is not None:
            model_status[r["judged"]["status"]] = model_status.get(r["judged"]["status"], 0) + 1
            if not c.get("probe"):
                judged_ranks.append(r["jr"])
            if r["entry"].get("target_level") == "scattered":
                scattered_targets.append(c["id"])
        if args.review_pack:
            pack.append(r["block"])

    def summary(label, rs):
        total = len(rs)
        hit = lambda k: sum(1 for r in rs if r and r <= k)
        mrr = sum(1 / r for r in rs if r) / total
        print(f"{label:22} hit@1 {hit(1)}/{total}  hit@3 {hit(3)}/{total}  hit@10 {hit(10)}/{total}  MRR {mrr:.3f}  not found {sum(1 for r in rs if not r)}")

    print()
    summary("search order", ranks)
    if precision:
        print(f"Precision of the top results (cases with relevant_contains): {sum(precision)/len(precision):.2f} over {len(precision)} cases")
    if args.judge:
        summary("after AI labels", judged_ranks)
        print(f"AI status per case: {model_status}")
        print(f"AI labels over all results sent: {label_total}")
        if conf_total:
            print("AI vs relevant wording (cases with relevant_contains): "
                  f"relevant labelled match={conf_total.get('rel_match', 0)}, relevant labelled partial/scattered={conf_total.get('rel_other', 0)}, "
                  f"NOT relevant labelled match={conf_total.get('nonrel_match', 0)}, NOT relevant labelled partial/scattered={conf_total.get('nonrel_other', 0)}, "
                  f"not judged={conf_total.get('not_judged', 0)}")
        print(f"Target labelled 'scattered' (would be hidden if the user hides): {len(scattered_targets)} {scattered_targets}")
        if any(s != "reviewed" for s in model_status):
            print("WARNING: the model did not fully answer for some cases; this run does not measure its value.")
    if decoy_above:
        print(f"Decoy ranked above the target: {decoy_above}")
    print(f"Latency ms: median {sorted(latencies)[len(latencies)//2]:.0f}, max {max(latencies):.0f}")

    if args.save:
        Path(args.save).write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Saved to {args.save}")
    if args.review_pack:
        Path(args.review_pack).write_text("# Search review pack\n\n" + "\n".join(pack), encoding="utf-8")
        print(f"Review pack written to {args.review_pack}")
    if args.compare:
        before = json.loads(Path(args.compare).read_text(encoding="utf-8"))
        changes = {i: (before[i]["rank"], e["rank"]) for i, e in out.items() if i in before and before[i]["rank"] != e["rank"]}
        print(f"Rank changed vs {args.compare} (before, now): {changes if changes else 'none'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
