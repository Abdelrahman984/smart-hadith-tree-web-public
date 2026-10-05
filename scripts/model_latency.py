#!/usr/bin/env python3
"""Time the configured chat model directly, to see where the latency of the AI features goes.

It calls the same OpenAI-compatible endpoint the API uses and reports, per call, the wall time, the token counts the
provider returns (prompt, completion, and reasoning tokens when it reports them) and whether the answer carries a
separate reasoning text. A "Flash" model that spends most of its time on reasoning tokens is slow for a reason the
code cannot change; a model that answers a one-word prompt in about a second but a 10-passage prompt in 20 s points
to output length.

Reads the same settings as the API: environment variables first
    Together__ApiKey (or TOGETHER_API_KEY), Together__Model (default zai-org/GLM-5.3-Flash),
    Together__Endpoint (default https://api.together.xyz/v1)
and, when the key is not in the environment, the .NET user secrets of src/SmartHadithTree.Api
(`dotnet user-secrets set "Together:ApiKey" ...`). The key is never printed.

Usage:
    python scripts/model_latency.py                         # tiny prompt, then the realistic search-check prompt
    python scripts/model_latency.py --repeat 3 --parallel 3 # mimic the evaluation runner
    python scripts/model_latency.py --max-tokens 400
    python scripts/model_latency.py --extra '{"reasoning": {"enabled": false}}'   # provider-specific option: check the provider's docs
Standard library only.
"""
import argparse
import concurrent.futures
import json
import os
import re
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request

PASSAGES = [
    "قال رسول الله ﷺ: «إنما الأعمال بالنيات وإنما لكل امرئ ما نوى، فمن كانت هجرته إلى الله ورسوله فهجرته إلى الله ورسوله»",
    "حدثنا سفيان عن عمرو عن أبي قابوس عن عبد الله بن عمرو يبلغ به النبي ﷺ: «الراحمون يرحمهم الرحمن ارحموا من في الأرض يرحمكم من في السماء»",
    "عن أبي هريرة قال: قال رسول الله ﷺ: «الكلمة الطيبة صدقة، وكل خطوة تمشيها إلى الصلاة صدقة»",
    "قال: قلت يا رسول الله أي الإسلام أفضل؟ قال: «من سلم المسلمون من لسانه ويده»",
    "عن النعمان بن بشير قال سمعت رسول الله ﷺ يقول: «الحلال بين والحرام بين وبينهما أمور مشتبهات»",
    "عن ابن عباس قال: قال رسول الله ﷺ: «لا ضرر ولا ضرار»",
    "قال رسول الله ﷺ: «من غشنا فليس منا»",
    "عن أبي مالك الأشعري أن رسول الله ﷺ قال: «الطهور شطر الإيمان»",
    "قال رسول الله ﷺ: «إنما بعثت لأتمم صالح الأخلاق»",
    "قال رسول الله ﷺ: «الجنة أقرب إلى أحدكم من شراك نعله والنار مثل ذلك»",
]

# Same shape as SearchJudgeService.BuildPrompt (kept short here; update both if the prompt changes).
JUDGE_PROMPT = """أنت مساعد يفحص نتائج بحث في متون الأحاديث. لديك عبارة بحث، وقائمة مقاطع مرقمة من متون أحاديث.
مهمتك فقط الحكم على كل مقطع بواحد من ثلاثة مستويات:
- match: المقطع نفسه هو الحديث أو الرواية التي تقرر معنى عبارة البحث (ولو اختلف اللفظ يسيراً).
- partial: عبارة البحث أو معناها ترد في المقطع جزءاً من حديث أطول أو في سياق مختلف، أو تُذكر عرضاً في تعليق أو كلام عن الحديث وليس متنه.
- scattered: كلمات البحث موجودة لكن المقطع في موضوع آخر أو حديث مختلف.

قواعد صارمة:
- لا تحكم على صحة الحديث أو ضعفه.
- لا تضف معلومة من خارج المقاطع.
- لكل مقطع اكتب Quote: عبارة من ثلاث إلى سبع كلمات منسوخة حرفياً من المقطع نفسه تدعم حكمك، واكتبها بدون تشكيل.
- اجعل Reason أقل من ست كلمات.
- أعطِ حكماً واحداً لكل رقم، ولا تكرر رقماً ولا تترك رقماً.

عبارة البحث: الطهور شطر الإيمان

المقاطع:
{passages}

أرجع مصفوفة JSON صالحة فقط بدون أي نص إضافي أو علامات Markdown:
[{{"Index": 1, "Level": "match", "Quote": "عبارة حرفية", "Reason": "جملة قصيرة بالعربية"}}]"""


def read_user_secrets():
    """Together settings stored with `dotnet user-secrets` for the API project, as {"ApiKey": ..., "Model": ..., "Endpoint": ...}."""
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    try:
        out = subprocess.run(["dotnet", "user-secrets", "list", "--project", os.path.join("src", "SmartHadithTree.Api")],
                             cwd=root, capture_output=True, text=True, timeout=60).stdout
    except (OSError, subprocess.SubprocessError):
        return {}
    return {m.group(1): m.group(2).strip() for m in re.finditer(r"^Together:(\w+)\s*=\s*(.*)$", out, re.MULTILINE)}


def call(endpoint, key, model, prompt, max_tokens, extra):
    body = {"model": model, "messages": [{"role": "user", "content": prompt}]}
    if max_tokens:
        body["max_tokens"] = max_tokens
    body.update(extra)
    req = urllib.request.Request(
        endpoint.rstrip("/") + "/chat/completions", data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json", "Authorization": f"Bearer {key}",
                 # Python's default urllib User-Agent is rejected by Cloudflare in front of the API (HTTP 403, error code 1010).
                 "User-Agent": "Mozilla/5.0 (compatible; smart-hadith-tree-latency/1.0)", "Accept": "application/json"},
        method="POST")
    start = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=180) as res:
            data = json.load(res)
    except urllib.error.HTTPError as e:
        return {"error": f"HTTP {e.code}: {e.read().decode('utf-8', 'replace')[:300]}", "seconds": time.perf_counter() - start}
    except (urllib.error.URLError, OSError) as e:
        return {"error": f"connection failed: {e}", "seconds": time.perf_counter() - start}
    seconds = time.perf_counter() - start
    msg = (data.get("choices") or [{}])[0].get("message", {})
    usage = data.get("usage") or {}
    details = usage.get("completion_tokens_details") or {}
    reasoning_text = msg.get("reasoning") or msg.get("reasoning_content") or ""
    return {
        "seconds": seconds,
        "prompt_tokens": usage.get("prompt_tokens"),
        "completion_tokens": usage.get("completion_tokens"),
        "reasoning_tokens": details.get("reasoning_tokens"),
        "reasoning_chars": len(reasoning_text),
        "answer_chars": len(msg.get("content") or ""),
        "answer": (msg.get("content") or "")[:200].replace("\n", " "),
    }


def show(label, r):
    if "error" in r:
        print(f"{label}: {r['seconds']:.1f}s  ERROR {r['error']}")
        return
    print(f"{label}: {r['seconds']:.1f}s  prompt_tokens={r['prompt_tokens']} completion_tokens={r['completion_tokens']} "
          f"reasoning_tokens={r['reasoning_tokens']} reasoning_text_chars={r['reasoning_chars']} answer_chars={r['answer_chars']}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repeat", type=int, default=2)
    ap.add_argument("--parallel", type=int, default=1)
    ap.add_argument("--max-tokens", type=int, default=0)
    ap.add_argument("--extra", default="{}", help="JSON merged into the request body (provider-specific options)")
    ap.add_argument("--show-answer", action="store_true")
    args = ap.parse_args()

    key = os.environ.get("Together__ApiKey") or os.environ.get("TOGETHER_API_KEY")
    secrets = {} if key else read_user_secrets()
    key = key or secrets.get("ApiKey")
    if not key:
        print("No Together key found: set Together__ApiKey (or TOGETHER_API_KEY) in the environment, or store it with\n"
              '  dotnet user-secrets set "Together:ApiKey" "<key>" --project src/SmartHadithTree.Api\n'
              "The key is never printed.")
        return 2
    model = os.environ.get("Together__Model") or os.environ.get("TOGETHER_MODEL") or secrets.get("Model") or "zai-org/GLM-5.3-Flash"
    endpoint = os.environ.get("Together__Endpoint") or secrets.get("Endpoint") or "https://api.together.xyz/v1"
    extra = json.loads(args.extra)
    print(f"model={model} endpoint={endpoint} max_tokens={args.max_tokens or 'default'} extra={extra} parallel={args.parallel}")

    tiny = "أجب بكلمة واحدة فقط: ما عاصمة مصر؟"
    judge = JUDGE_PROMPT.format(passages="\n".join(f"[{i}] {p}" for i, p in enumerate(PASSAGES, 1)))

    for name, prompt in (("tiny prompt", tiny), ("search-check prompt (10 passages)", judge)):
        print(f"\n== {name}")
        with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, args.parallel)) as pool:
            results = list(pool.map(lambda _: call(endpoint, key, model, prompt, args.max_tokens, extra), range(args.repeat)))
        for i, r in enumerate(results, 1):
            show(f"  run {i}", r)
            if args.show_answer and "answer" in r:
                print(f"     answer: {r['answer']}")
        times = [r["seconds"] for r in results if "error" not in r]
        if times:
            print(f"  median {statistics.median(times):.1f}s, max {max(times):.1f}s")
    print("\nHow to read it: reasoning_tokens (or reasoning_text_chars) much larger than the answer means the time goes to "
          "reasoning; the tiny prompt being slow too means queueing or network; slow only on the long prompt means output length.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
