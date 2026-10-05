# Plan: relevance scoring and AI judging on `/search`

**Status: implemented (2026-10-05), not yet measured on the real corpus. See "Implementation status" at the end.** Branch `claude/friendly-keller-81748m`. The deferred `/verify` feature is in `docs/verify-feature-handoff.md`.

## Context

The owner's idea: let the AI judge every search result and give a percentage showing whether the hadith really matches what was searched. The problem it targets: long hadiths that contain the searched words far apart from each other.

What the code does today (`src/SmartHadithTree.Application/Services/HadithSearchService.cs`):

- With several words, `EvaluateTermCluster` looks for all words inside an 800-character window around the longest word, ranks by exact phrase first and then by smallest span (`ThenBy ClusterSpan`), and drops mega-records whose words are scattered. The test `SearchHadithsAsync_FiltersOutScatteredMegaRecord_AndRanksExactPhraseFirst` covers it.
- The span is measured in characters, not words, and says nothing about meaning (a negation, a different subject).
- **Likely recall problem (read from the code, not yet reproduced):** candidates are fetched with `OrderBy(MatnArabic.Length)` then `Take(skip + max(150, pageSize*3))` *before* the proximity ranking. For a broad query, the 150 shortest matching matns are ranked and longer ones never reach the ranking. No score can fix a hadith that was never fetched.
- The result DTO (`HadithSearchResultDto`) has no score field. The page (`frontend/src/app/search/page.tsx`, `HadithCard.tsx`) shows no relevance.

## Why not a literal "AI percentage on every result"

- A percentage from a language model is not calibrated: it looks precise, changes between runs and measures nothing definite. It would weaken the "reliability" criterion.
- Latency: one model review in `/verify` took 10–25 s. 50 results per page is not acceptable inside a search.
- Cost on every keystroke-driven search.

## Recommended design (two layers)

### Layer A: relevance percentage computed by code (instant, explainable)

New pure class `RelevanceScorer` (in `Application/Services/`), unit-tested, used for every result of both the standard and the advanced path.

`percent = 100 x coverage x (0.25 + 0.50 x proximity + 0.25 x phrase/order)` (the first draft added the parts; multiplying by coverage keeps a hadith with missing words low).

| Component | Meaning |
|---|---|
| coverage | share of the query words found in the matn (after the project's normalization) |
| proximity | smallest window **in words** that contains all found words; `min(1, (words + gaps allowed) / window)`, one filler word between words is free |
| phrase/order | 1 if the exact phrase is present, 0.5 if the words appear in the query order, else 0 |

Output per result: `relevancePercent` (0–100) and `relevanceReason` (one Arabic line, for example "كل الكلمات (3 من 3) داخل 5 كلمات"). Both are added to `HadithSearchResultDto` and `frontend/src/types/api.ts`. Sorting uses the score first, then the existing tie-breakers (so current behavior is kept where scores tie). The weights are a starting point to tune on the evaluation set, not a measured truth.

### Layer B: opt-in AI judging of the results on the page

The user chooses to use it; it never runs on its own.

- New endpoint `POST /api/Search/ai-judge` with `{ query, ids }`: the ids of the current page's results (at most **50**, the page size). The server loads the matn from the database (client text is not trusted) and sends, for each result, only a **window of about 300–400 characters around the searched words**, not the whole matn. The call count is an implementation detail: one call up to about 20 results; above that, split into chunks of 15–20 sent **in parallel**, so total time stays close to one call. (The payload size and the time per call are estimates, not measured; measure them in step 4.)
- The model returns, per result, one of three levels: `match` (يطابق), `partial` (يطابق جزئياً), `scattered` (كلمات متفرقة), plus a short reason that quotes a phrase from the matn. Three levels, not a percentage.
- Validation in code, same pattern as `/verify`: every index must come back, be in range and occur once; the quote must occur verbatim in that matn (reuse `AiEvaluationService.IsQuoteGrounded`). A result that comes back missing, duplicated or with an unverified quote is marked `not judged` (لم يُحكم عليه), never counted as match or non-match. Prompt rules: judge only whether the matn matches the meaning of the query; never grade authenticity; add no content.
- Time limit per call (env `SEARCH_AI_TIMEOUT_SECONDS`, default 20) and a status per response like `/verify` (`reviewed|partial|timeout|unavailable`); in-memory cache by normalized query and hadith id. On failure the page keeps the Layer A score and says the AI check did not finish.
- **Public-demo protection:** the endpoint calls a paid model without login, so cap the ids per request (50), add ASP.NET rate limiting per client, and use the cache. Without this, anyone can run up the cost.
- UI (`app/search/page.tsx`, `HadithCard.tsx`): a button "تحقق بالذكاء الاصطناعي من نتائج هذه الصفحة" above the results; while it runs, the Layer A scores stay usable; badges appear on the cards when the answer arrives; `AiNotice` is shown. After the check, a switch **"إخفاء غير المطابق"** (off by default, the user's choice) hides results labelled `scattered`, with a permanent counter "تم إخفاء N نتيجة (عرضها)". **Nothing is deleted or hidden silently**: a model mistake must never make a correct hadith disappear without the user knowing. Results marked `not judged` are never hidden.
- Only the current page is judged; changing the page, query or filters clears the labels.

## Steps (in order)

0. **Baseline before changing anything.** Run the new search evaluation (step 6) against the current API and save `search_baseline.json`. (Owner runs it; about 5 minutes.)
1. **Reproduce and fix the candidate cap.** Add a test (in-memory database, like the existing ones in `HadithSearchServiceTests.cs`) with more than 150 short matches plus one long matn containing the exact phrase; show it is dropped. Fix: always include the SQL matches of the exact normalized phrase (cheap `Contains`) in the candidate set in addition to the shortest N, and raise N if latency allows. Re-run the existing tests.
2. **`RelevanceScorer` + tests** (coverage, proximity in words, phrase/order; scattered long matn scores low; exact phrase scores high; diacritics and hamza forms do not matter). Reuse `ArabicNormalizer` and the tokenizer from `HadithVerificationService` instead of writing a new one.
3. **Wire the scorer into `HadithSearchService`** (both paths), add DTO fields, show a bar and the reason on `HadithCard.tsx`. Keep the existing sort tie-breakers.
4. **AI judge endpoint + tests** with a fake kernel (valid answer; quote not in the matn; index out of range, missing or duplicated; chunking of more than 20 results; timeout; no model; request over 50 ids; rate limit). Measure the real payload size and time for 20 and 50 results with the configured model before choosing the chunk size.
5. **Search page button, badges and the "hide non-matching" switch with its counter.** Mutation through react-query like the existing search hook; Arabic labels; `AiNotice`; labels are cleared when the page, query or filters change.
6. **Evaluation set and runner.** `docs/challenge/search-eval-cases.json` (about 20 queries, each with a target identified by a distinctive matn substring so it does not depend on book numbers, plus decoys: long hadiths with the words scattered) and `scripts/eval_search.py` reporting the rank of the target, hit@1/3/10 and MRR for: baseline, Layer A, Layer A + B. Report `modelStatus` and warn when the model did not answer, so a timeout is not read as "the model added nothing".

## Files

- Backend: `HadithSearchService.cs` (candidates, scoring wiring), new `RelevanceScorer.cs`, `ApiDtos.cs` (`HadithSearchResultDto`), new `SearchJudgeService.cs` + DTOs, `SearchController.cs` (endpoint), `Program.cs` (registration), `HadithSearchServiceTests.cs` and new tests.
- Frontend: `features/search/components/HadithCard.tsx`, `app/search/page.tsx`, `lib/api.ts`, `types/api.ts`, reuse `components/AiNotice.tsx`.
- Eval: `docs/challenge/search-eval-cases.json`, `scripts/eval_search.py`.

## Verification

- `dotnet test src/SmartHadithTree.Tests -c Release` (use `DOTNET_ROLL_FORWARD=Major` in the cloud sandbox), `npx tsc --noEmit` and `npx eslint` in `frontend/`.
- Run `scripts/eval_search.py` on the owner's machine with the corpus: before (step 0) and after each layer. Nothing about accuracy is claimed without these numbers.
- Manual check in the browser on 5–10 queries, including one where a long hadith has the words far apart.

## Risks and decisions for the owner

1. **Time.** Submission closes 2026-10-06 23:59 (Riyadh) and the Live Demo, video and presentation are still open (`docs/challenge/gap-analysis.md`). Rough effort: steps 0–3 and 6 (deterministic, no model) about 3–4 hours; steps 4–5 (AI judge) about 2–3 more. Recommended cut if time is short: do steps 0–3 and 6, keep the AI judge for later.
2. **Model speed.** The AI check should finish in roughly 5–10 seconds for a page of results. If the configured model needs 10–25 seconds even for one review, set `Together__ExtraBody={"reasoning_effort":"low"}` (measured: 2.8–4.3 s for ten passages versus 8–27 s; the other tried fields `reasoning.enabled`, `chat_template_kwargs.enable_thinking` and `thinking.type` did not stop the reasoning on Together), or switch `Together__Model` to a faster non-reasoning model, or the button will feel broken. Longer lists also make models skip or mis-number items, which is why the code checks that every result came back.
3. **Fairness of the comparison.** Layer A is the "simpler alternative" the AI is compared against. If Layer A alone solves the cases, say so; the AI judge then stays an optional second opinion.
4. **Weights are a guess** until tuned on the evaluation set; the evaluation set is written by the assistant and the owner should extend it with their own queries.
5. **Hiding results is a recall risk.** A wrong `scattered` label would hide a correct hadith. That is why hiding is opt-in, reversible and always counted, and why `not judged` is never hidden. The evaluation (step 6) must report how often the AI labels a target hadith `scattered`.
6. **Cost abuse on the public demo** (see the protection above).
7. **Search ordering changes** affect everyone using `/search`; keep the existing tests green and compare the baseline run before merging.

## Implementation status (2026-10-05)

Built and unit-tested (208 tests pass on Linux, .NET 10 SDK with `DOTNET_ROLL_FORWARD=Major`); UI checked in a browser against a mock API; **not yet run against the real corpus**.

| Step | Status |
|---|---|
| 0. Baseline | **Owner to run** (needs the corpus), see below |
| 1. Candidate cap | Reproduced by a test that fails on the old code, fixed: when the cap is reached, exact-phrase hits are fetched too (`HadithSearchService`, `ExactPhraseCandidateCap = 300`) |
| 2. `RelevanceScorer` + tests | Done (`Services/RelevanceScorer.cs`, `RelevanceScorerTests.cs`) |
| 3. Wiring and card bar | Done: standard and advanced paths return `relevancePercent` and `relevanceReason`; results sort by relevance first, then the old tie-breakers; the card shows a bar and the reason. Not computed for Isnad-only scope or when no word is in the matn |
| 4. AI judge endpoint | Done: `POST /api/Search/ai-judge`, `SearchJudgeService`, 10 tests with a fake chat model (valid answer, quote not in matn, quote under three words, repeated / missing / out-of-range index, no model, timeout, chunking 20+20+5, cache, invalid input, fenced JSON). Rate limit `RateLimit:AiPermitPerMinute` (default 10 per client address per minute) on this endpoint only |
| 5. Page button, badges, hide switch | Done (`app/search/page.tsx`, `HadithCard.tsx`): the labels belong to one page of results and disappear when the results change; "not judged" is never hidden; the hide switch is off by default and the hidden count is always shown with a link to show them |
| 6. Evaluation | `docs/challenge/search-eval-cases.json` (18 queries) and `scripts/eval_search.py`. The list has no scattered-decoy cases yet: only the owner can find long hadiths in the corpus where the words are far apart; add them under `decoys_contains` |

### How to measure (owner)

```powershell
# BEFORE: an API built from the commit before the relevance change
git worktree add ..\baseline 6fcc33c     # the "docs: search plan - opt-in AI judging" commit
# run that API (dotnet run --project ..\baseline\src\SmartHadithTree.Api), then:
python scripts/eval_search.py --save baseline.json

# AFTER: this branch's API
python scripts/eval_search.py --save current.json --compare baseline.json
python scripts/eval_search.py --judge --save with_ai.json      # opt-in AI check; needs the model key
```

Read the numbers with the usual care: the targets were written from memory (check the wording if one is never found), the set is small, and the weights of the score are a first guess to tune after seeing these numbers. With `--judge`, the line "Target labelled 'scattered'" is the cost of hiding: if it is above zero, say so. The runner warns when the model did not answer.

### Settings

`SEARCH_AI_TIMEOUT_SECONDS` (5-90, default 20 per call), `RateLimit__AiPermitPerMinute`, and the model keys as for `/verify` (`Together__ApiKey`, `Together__Model`). If the model needs more than 5-10 seconds for a chunk of 20, the button will feel slow: use a faster non-reasoning model.

### Search starts on demand (added 2026-10-05)

`/search` no longer searches while typing (it used to wait 350 ms after the last keystroke). The search runs when the user presses the **بحث** button or Enter, or picks a suggestion; the text must have at least 3 characters (`MIN_QUERY_LENGTH` in `useHadithSearch.ts`). Reasons: every search scans the matns of the whole corpus with `Contains`, partial words gave useless intermediate queries, and the AI labels belong to one page of results, which used to change under the user at every keystroke. Behavior worth knowing: editing the box keeps the current results and labels until the next search; the clear button empties both the box and the results; pressing search while advanced (Shamela) conditions are active replaces them with the simple search. Checked in a browser with a mock API: 0 requests while typing, 1 after the click, 1 while editing, 2 after Enter.

### Review packs (for an outside reviewer)

`python scripts/eval_search.py --judge --review-pack search_review.md` and `python scripts/eval_verify.py --review-pack verify_review.md` write a Markdown file with, for each case, the query, the top results with their relevance score, the AI label, the AI reason and quote, the matn, and an empty line for the reviewer's verdict. Hand the file to a human reviewer (preferably a specialist) and, if you want a second opinion from the assistant, upload it to the session. The assistant's reading of the matn is a check on the AI's labels, not ground truth: authenticity and rulings are outside what it judges, and a model checking a model shares blind spots, so disagreements should go to the human reviewer.

### Findings of the first real runs (2026-10-05, owner's machine)

Full run of `eval_search.py --judge` (18 queries, AI check on the top 10, 3 cases in parallel, 108 s):

- Search order: hit@1 15/18, hit@3 16/18, hit@10 16/18, MRR 0.865. Misses: S02 (rank 2), S03 (rank 20), S11 (rank 47).
- **S03 is mostly a benchmark artifact:** the top results are other narrations of the same hadith ("من سلم المسلمون من لسانه ويده") and the target is one specific wording written from memory.
- **S11 is a real miss of the score:** "الحلال بين الحرام بين" put the hadith "فصل ما بين الحلال والحرام الصوت" (the duff) first. The scorer treated the repeated word "بين" as one. Fixed: a repeated query word must occur that many times (test with both texts). The AI check timed out on this query, so the case that would show whether the AI separates different hadiths on the same words has not been observed yet.
- AI check: 146 labels, **all "match"** (no partial, no scattered); 26 results not judged, 25 of them from 3 timeouts at 30 s (S11, S14, S17), 1 because the answer failed verification (S02.1, a commentary that does not state the phrase). The rank gain after the AI (MRR 0.865 to 0.893) comes only from that one result being demoted as not judged, not from a "scattered" verdict.
- Borderline labels read by the assistant: S03.8 (Hajj hadith where the phrase is a condition) and S09.7 (al-Bazzar's comment about the routes of the hadith) are "partial" at best. The prompt now defines the levels more strictly (match = the passage is the hadith that states the meaning; partial = part of a longer or different context, or mentioned in a comment; scattered = words present, other subject) and asks for a short reason. To be re-measured.
- Model latency: 7-27 s per call of 10 results (median about 17 s). Too slow for a button users wait on; a faster non-reasoning model is the main lever.
- Open: the AI never produced "partial" or "scattered" on this set, so its ability to catch wrong results is still unproven. Add decoy cases (`decoys_contains`) such as S11's duff hadith and re-run.

### Second run after the fixes (2026-10-05, subset S03 S09 S11 S14 S17, time limit 60 s)

- **S11 is fixed in substance.** Before, all ten top results were the hadith about the duff ("فصل ما بين الحلال والحرام الصوت"); now eight are the Nu'man hadith and two are Ibn Mas'ud's advice on judging that quotes it. The strict `rank` (33) only reflects the rare exact wording of the target, not the quality of the top results; the runner now also reports the precision of the top results through `relevant_contains`.
- **The stricter prompt makes the AI use "partial".** Five results were labelled partial (S03.8 Hajj hadith, S03.9 and S03.10 where the phrase is one answer in a longer series, S11.8 and S11.10 where the phrase is quoted inside Ibn Mas'ud's judging advice); the assistant agrees with all five. Everything else was "match". **Still no "scattered"**: no top-10 contained a hadith that only shares the words, so the label is unproven. The new probe cases D01-D03 (queries of common words) exist to produce such results; check them in the review pack.
- **One timeout at 60 s (S09)** after 28 s for the same query in an earlier run: the model's latency is erratic (7 to over 60 s for ten short passages, with three requests in parallel). The reasons returned by the model contain corrupted words ("مت الحديث" for "متن الحديث", earlier "فيors"), which points to the model, not to the code. Changing `Together__Model` to a faster, more reliable model is the main lever and it needs the owner's decision.
- The runner now reports, with `--judge`: AI labels over all results, how the AI labels relate to the `relevant_contains` wordings (relevant labelled match / partial-scattered, NOT relevant labelled match / partial-scattered) and the precision of the top results. These are the numbers to quote about the AI check, together with the number of timeouts.
