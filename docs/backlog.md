# Backlog: open tasks and ideas

One place for work that is known but not done. The history of the Shamela migration stays in `docs/shamela_migration.md`; this file only tracks what is left. **Last updated: 2026-10-05**, after four hand-reviewed pages (see the review log at the end).

## Start here (for a new session)

**State.** Branch `feature/shamela-rijal` (not merged into `master`). The app (Api and Etl `appsettings.json`) runs on the database `SmartHadithTree_ShamelaV5`; `SmartHadithTree_Shamela` is v4, kept for rollback. Newest backup: `backups/SmartHadithTree_Shamela_v5_2026-10-05.bak`. Phases 0-8 are done (`docs/shamela_migration.md`). 160 tests pass.

**What the owner wants.** The branch exists because the Itqan-built data gave errors on every hadith page; Shamela was meant to fix that. It still gives errors on every page, so the work now is to find the **general** causes and fix those. Do **not** fix one hadith at a time: classify each error by cause first, count it in the review log, then fix the cause that removes the most errors.

**Rules of work.**
- Read this file, then `docs/shamela_migration.md` (§9 pitfalls) before touching the resolver, the loader or the Ilal rules.
- Confirm before anything that is hard to undo (dropping a database, a rebuild, a merge). Commit only when asked.
- A resolver change needs a rebuild into a **fresh empty database** and a new numbered backup. Group such items and rebuild once.
- Every rule or resolver change is measured on a fresh sample (not the one it was tuned on) and a hand-read sample, as in `docs/shamela_migration.md` §9.
- Tests: `dotnet test src/SmartHadithTree.Tests -c Release` (use Release; a running Api locks the Debug DLLs). Frontend: `npx tsc --noEmit` and `npx eslint` in `frontend/`.
- The owner writes in Arabic (Egyptian dialect); answer in the language they use. Keep answers short and say what was and was not verified.

**How to review a `/takhreej` page** (this is how the review log was made):
1. The page ids are the hadith Guids of the **current** database. Links saved against v4 return 404 on V5 (the Guids are new). Map old to new by `BookName` + `HadithNumber` + the start of `NormalizedMatn` (numbers repeat across volumes).
2. Fetch the data: `curl -s "http://localhost:5147/api/Takhreej?ids=<id1>,<id2>,..."` (Api on 5147, frontend on 3000). Read `sources[].matnArabic`, `ilalReport.findings[]` (`type`, `severity`, `confidence`, `evidenceAr`), `ilalReport.turuq`, `ilalReport.madars`, `calculatedGrade`.
3. Read each source's own text and compare it with the stored chain (`Transmissions` by `StepOrder`; step 1 is the compiler's student). Look for: a missing or wrong narrator, a chain that stops before the Companion, text that does not belong to the matn (commentary, editor notes), and a finding that the texts do not support.
4. Classify every finding or chain error under a cause in this file, or add a new cause. Add one row to the review log, and update the counts in «Suggested order».
5. Do not fix during the review unless asked; the point is to measure.

**Tool gotchas.**
- `sqlcmd` on Windows: Arabic literals inside a `-i` file arrive mangled (a `LIKE N'...'` finds nothing), so select by Guid or number and filter in Python. Write results with `-u -o file` and decode as UTF-16 (the console output is `????`). Windows Python needs `PYTHONIOENCODING=utf-8` to print Arabic.
- Bash heredocs mangle Arabic in some cases; write Arabic scripts with the Write tool and run them.
- The single-tree SQL (`HadithChainRepository`) is raw SQL, so a new `IsnadNodeDto` property must be added to its select list; the unit tests do not run that query. Check `/api/Tree/<id>` after such a change.

**Status marks.** `[ ]` open, `[~]` in progress, `[x]` done (then move the line to **Done** with the date and commit).

## How to use

- **Needs a rebuild** = the change is in the Python resolver (`scripts/shamela4-extractor/rijal_pilot/`). It takes effect only after `pipeline.py dump`, `export_chains_shamela.py --books all` and a load into a **fresh empty database** (`docs/data_ingestion.md`, `docs/shamela_migration.md` §8). Group these items and rebuild once, then take a new numbered backup (`backups/README.md`).
- **No rebuild** = backend or frontend only.
- Measure resolver changes with `bench.py <tag> --books all --texts shamela --review 0 --base <tag>` and read a hand sample before and after (`docs/shamela_migration.md` §9).

## Suggested order (from the four pages reviewed so far)

Counts below come from the review log at the end; they are small samples, so re-count on new pages before trusting the order.

| # | Cause | Seen on | Needs a rebuild | Why first |
|---|---|---|---|---|
| 1 | A short text that is a subset of a longer one counted as «omission» | 11 of 17 findings on the Tahur page | no | one Ilal rule, removes the most false «قادحة» |
| 2 | Matn commentary, editor notes and symbols stay in the matn | 3 pages | yes (books) | feeds Nakarah, Idtirab and Ziyadah with fake differences |
| 3 | Unranked narrator treated as weak | 3 pages | no | one rule, false «ضعيف» in Nakarah |
| 4 | Chains cut before a Companion, dropped or wrong narrators (tahwil forms, parentheses, undecided kunya; the ~860 names in the carried-over table) | 3 pages | yes (resolver) | wrong chains, not just wrong warnings |
| 5 | Marfu detection, ziyadah on repeated words | 1 page each | no | small rules |
| 6 | Missing relations and missing grades for late narrators (§2) | 15 findings on 4 pages | no fix in code | tag as «بيانات ناقصة» (see §2), do not present as a finding |
| 7 | Shahid vs tariq in the comparative page | Tahur page | no | see §3 |

## 1. Resolver gaps (needs a rebuild; do them as one batch)

| Item | Where it shows | Note |
|---|---|---|
| A narrator dropped in «أخبرني X أنه سمع Y» (after a «ح» part) | Bayhaqi 15101 (Yahya ← Alqama, Muhammad ibn Ibrahim lost) | segmentation, not an undecided name; causes a false «لم يثبت اللقاء». Count how many chains skip a narrator this way |
| A tahwil head with no verb: «… عن الأعمش، عن شقيق، عن عبد الله. وعبد الرحمن، عن سفيان، عن منصور …» | Ahmad 4216 (the second chain is lost) | a second head written as «و» + name, no «حدثنا»; `tahwil.py` does not read this form |
| A wrong join between tahwil parts | Shu'ab al-Iman 9762 (A'mash ← al-Alawi; Abu Hamid ibn al-Sharqi dropped; Sufyan ← Mansur/A'mash missing) | this contradicts the Phase 8 note «wrong joins did not occur» (34 branched records read); the sample was too small. Count wrong joins on a bigger sample, and make `join_heads` stricter for 3+ heads |
| An editor's parenthesis inside the isnad: «عن زيد (عن أبي سلام) عن أبي مالك» | Ibn Abi Shayba 37 (Abu Sallam dropped, «زيد» resolved to **زيد بن نعيم**, a wrong narrator) | the parenthesis is cut, and the bare name then picks a neighbour-compatible but wrong narrator |
| The chain stops after Zayd ibn Sallam; Abu Sallam and the Companion Abu Malik are missing | Muslim 223, Shu'ab 12 and 2548, Darimi 661, Ahmad 22908 and 22909, Abu Awana 38 and 669 (only Bayhaqi 187, written «جده ممطور», and Ibn Abi Shayba 37 reach Abu Malik) | likely the bare kunya «أبي سلام» stays undecided and the chain is cut there instead of continuing (not verified). Measure how many chains end before a Companion, per book |
| «وحدثنيه X» keeps its verb in the name | Muslim | `VERBS` in `gap_test.py` lacks «حدثنيه» |
| «إبراهيم بن موسى» left undecided | Mustadrak 493, second chain | a name-matching gap, not a segmentation one |
| «كلهم عن X بهذا الإسناد» tails are not followed | Muslim, Bayhaqi | the tail belongs to several heads |
| A head whose last narrator is unresolved cannot join its tail | Tabarani 3733 | `join_heads` needs a resolved narrator |
| More than 8 chains in one record keeps only 8 | tahwil records | `MAX_BRANCHES` in `tahwil.py` |
| «أبيه» that Shamela links to someone else (91) and «ابن X» cuts (45) | Phase 4b list | not looked at yet |
| An undecided name still leaves a gap in the chain | everywhere | known regression, `docs/shamela_phase6_comparison.md` |

Port to C# (Phase 5b) is still open and now also has to cover `tahwil.py`, `kin_form` / `grandfather_of`, `compiler_items.py` and the opener rules in `compare_current.py`.

### Carried over from `docs/shamela_migration.md`

Open items of the migration, kept in that file with their numbers. One line each here; do the work from that file's description and tick it there too. Line numbers are those of the file on 2026-10-05.

| Item | Where (migration doc) | Size | Why it matters |
|---|---|---|---|
| Names resolved to another person: full names (190), shaykh-book duplicates chosen over the Tahdhib entry (204, «طاوس»), bare ism / kunya / nisba (467) | Phase 4b, lines 537, 539, 540 | ~860 names | the same kind of error as «زيد» → زيد بن نعيم: a wrong narrator in the chain. Review the largest names first, then rebuild |
| Bare «سفيان» / «حماد» / «عطاء» ties (deferred by the owner until after Phase 7) | Phase 2, line 461 | hides 15% of ikhtilat findings | the Phase 7 condition is met; decide whether to take it now |
| «أم X» read as a kunya (34); bare-name ties in the linked books (Shamela's links); the 91 «أبيه» linked to someone else and the 45 «ابن X» cuts | lines 538, 541, 542 | small | batch with the rows above |
| Real ties left undecided: «عبد الله» bare (146), «أبو معاوية» (103), «أبو إسحاق» (31); later namesakes chosen by chain (17, needs death dates) | closed, lines 433, 439 | ~300 names | closed as known residuals; reopen only if the review log shows them |
| Lisan al-Mizan (`lisan.json`, parsed, not merged into the resolver) | closed, line 425 | 9,170 entries | kept for narrator grading: use it with the Siyar / Thiqat idea in §2 |
| Manual review of ~30 famous isnads, the frontend in a browser, the AI summary | Phase 6, line 579 | — | partly done by the review log at the end of this file; add the AI summary check |
| Port the resolver to C# (5 steps) | Phase 5b, lines 569-573 | large | an architectural decision (a second implementation to keep in sync); decide yes or no, leave the plan in the migration doc |

## 2. Data gaps (not fixable in the resolver)

Seen on the 11-route Mughira page (`/takhreej`), where «لم يثبت اللقاء» shows 3 findings because the teacher-student list in Tahdhib al-Kamal lacks the link:

- Ibn Ulayya ← Muhammad ibn Amr
- Qutayba ← Qays ibn Unayf (al-Hakim's shaykh, outside the corpus)
- Ibn Ayyub ← al-Sayduni

Seen on the other pages: the teachers of Bayhaqi's and al-Hakim's shaykhs (4th-5th century narrators) have no lists at all: Ibn Hajar's Tahdhib does not cover them. Those «لم يثبت اللقاء» findings are 5 of 13 on the Niyyat page, 5 of 17 on the Tahur page and 2 of 5 on the Jannah page.

Also: Mu'awiya ibn Sallam ← «أخيه» Zayd (Tabarani 3424) gives «لم يثبت اللقاء». Probably a relation Tahdhib writes with the kin word «أخيه» that `link_tahdhib.py` does not read (not verified; the kin forms handled so far are «أبيه» and «جده»). That one is a resolver item if it holds.

**Narrators without a grade.** Ibn Hajar's rank (Taqrib) covers 7,882 of the 23,502 narrators, so late narrators (the shaykhs of Tabarani, Bayhaqi and al-Hakim) show no grade: 9 of the 45 nodes on the Mughira page (Sulayman ibn Ahmad, Ahmad ibn al-Husayn, Muhammad ibn Abd, Muhammad ibn Ishaq, Qays ibn Unayf, Ubayd ibn Ghannam, Muhammad ibn Yaqub, ...). They count as tier 7 («مجهول») in the route grade and as «غير محرر» in Nakarah/Ziyadah (see §3, unranked narrator counted as weak). Ideas, in the order I would try them:
1. **Read the two books that were left unparsed** (`docs/shamela_migration.md` §7: السير 10906 and الثقات ممن لم يقع في الكتب الستة 96165; their dumps are in `data/shamela_rijal/dump/`). They were closed because extra biography books add namesakes to the **resolver** (Lisan: 182 names gained, 221 lost). Grading is a different use: attach a verdict only to a narrator the registry already resolved, by full-name match, and never add narrators to the resolver. A text search for the eight ungraded narrators above found all but one in the Siyar and three in the Thiqat (mentions only, not yet entries). The Siyar gives free-text verdicts to map to ranks; the Thiqat's title already says «ثقة». Measure first: how many of the ~15,600 ungraded registry narrators have an entry there by full name.
2. Read a grade from the stored critics' quotes (`ScholarEvaluations`, 116,670) when there is no Taqrib rank.
3. Show «غير مُقيَّم» in the UI instead of an implied weakness.

Possible sources for these: more shaykh books (`parse_shaykh_books.py` covers 6 compilers) or a manual list of confirmed links.

## 3. Ilal and Taqwiyah ideas (no rebuild)

**False «قادحة» findings (do first):**

- **A short text that is a subset of a longer one is an abridgement, not an omission.** On the Tahur hadith (Ibn Abi Shayba 37, Bayhaqi 188 «فذكره بإسناده», Shu'ab 12 give only «الطهور شطر الإيمان»), the longer texts were flagged as «زيادة» over the short one: 3 Nakarah (قادحة), 5 Ziyadah and 3 Shudhudh, 11 of the 17 findings on that page. Rule idea: if the shorter text is contained in the longer one (after framing words), do not call the rest an addition by a narrator; a compiler's abridgement says nothing about the narrator. Real wording changes (Darimi «الوضوء ضياء», Awanah «الصوم برهان») stay as variants. The same applies to Ahmad 18171, flagged as «شذوذ» for a shared preface it cuts short («فذكر الحديث», «بنحوه»).
- **Unranked narrator counted as weak.** A narrator with no rank («غير محرر», tier 7) can make a version «منكرة لضعف راويها» (Nakarah, قادحة): al-Awza'i's route on the Niyyat hadith (Tabarani 40), Muhammad ibn Ghalib on the Tahur and Jannah pages. Unknown should mean «no verdict», not «weak». Also check why al-Awza'i's registry entry has no rank (he is in Taqrib).
- **Ziyadah on near-identical texts.** Bukhari 1, Humaydi 28 and Layth/Yazid read the same, yet «زيادة ثقة» is reported over Ibn Uyayna because of the repeated «إلى الله ورسوله». Repetition inside the matn should not count as an addition.
- **Marfu detection is too narrow.** Bayhaqi 184 reads «سمعت رسول ﷺ» (a typo in the printed text, no «الله»), so `isMarfu=false` and a false RafWaqf «وقفه الثوري» appears, probably with a WaslIrsal tanbih from the same cause.
- **Madar attribution when the chain is cut with «وبه»** (Tabarani 7050): a Shudhudh finding names Muhammad ibn Ibrahim against Yahya; not verified, look at how the texts are assigned to narrators.

**Comparative page:**

- **Tell a shahid from a tariq.** A comparison page collects every route of the wording, and that is right (a takhreej gathers short and long forms). But a source with another Companion is a **shahid**, not a route of the same hadith: Ibn Abi Shayba 38 and 32451 («حدثنا علي… الطهور شطر الإيمان», via Hujr ibn Adi) sit beside Abu Malik al-Ash'ari's routes. Mark each source by its Companion, show shawahid apart, and keep them out of the madar and narrator comparison.

**Panel:**

- The edge styling on the canvas (`getIlalEdgeDecorations`) still draws low-confidence findings. Decide whether it should follow the panel and skip them.
- The threshold of what the panel folds away (`LOW_CONFIDENCE_THRESHOLD = 0.3`, `frontend/src/features/ilal/utils/ilalLabels.ts`) is a guess; review it on more pages.
- Missing teacher-student relations of late narrators (15 «لم يثبت اللقاء» findings on four pages, §2) cannot be fixed in code. Show them as «بيانات ناقصة» (or skip them) when either narrator is a late one with no relation list, instead of presenting them as a finding.

## 4. Matn extraction (needs a rebuild of the books, not the resolver)

- A hadith's own commentary stays in the matn: Bayhaqi 15101 ends with «رواه البخاري في الصحيح عن الحميدي ورواه مسلم …», which became a Nakarah (قادحة) finding; Shu'ab al-Iman 9762 («لفظ حديثهما سواء. رواه البخاري … وفي رواية الفقيه …») did the same. Cut trailing «رواه / أخرجه / لفظ حديثهما» comments before the matn is stored.
- **Editor notes and markers in the matn:** «[حكم حسين سليم أسد]: إسناده صحيح» (Abu Ya'la 5280) became a «زيادة ثقة»; «[٦٦١]» (Ibn Hibban), «[عن الأعمش]» (Bayhaqi 6578), al-Bazzar's own remark on the mawquf routes (1663). These made the Idtirab (قادحة) finding on the Jannah hadith. Strip bracketed editor text and page markers, and keep a source's own comment out of the comparison. Darimi 661 also carries the printed-edition symbols «ب د ع ف م تحفه اتحاف» and a stray «**».
- **Isnad pieces inside the matn:** Ahmad 4216 starts «الجنة، وقال: وكيع، عن شقيق، عن عبد الله، قال …». Check the matn start detection on these.
- Count how many matns carry such text per book (Bayhaqi, Shu'ab, Hakim, Tabarani, Abu Ya'la, Bazzar). It was in 3 of the 5 false Qadihah/Ghayr-qadihah findings on the second and third pages.

## 5. Project housekeeping

- `feature/shamela-rijal` and `claude/happy-johnson-5vqndl` are merged into `master` (2026-10-05, `6523dd7`, `48fd260`). The old master is kept as the tag `legacy-master-itqan` and the branch `legacy/itqan-master` (code only; it needs the Itqan database `SmartHadithTree` and the file `GawamiTbxReader.cs`, which only the Shamela branch has).
- Challenge work that was on hold until the merge (README/LICENSE, sources and licenses, the golden test set and Ilal accuracy measurement, presentation numbers, sample data/seed script, repository clean-up) is listed in `docs/challenge/gap-analysis.md` and is no longer blocked.
- The hadith ids of `SmartHadithTree_ShamelaV5` are new Guids, so saved `/takhreej?ids=` and `/tree/<id>` links made against the v4 database return 404. A rebuild always changes them. Map by `BookName` + `HadithNumber` + the start of `NormalizedMatn` when old links must be kept (numbers repeat across volumes).
- The Itqan columns `ItqanId` / `ItqanGrade` on `Narrators` remain until a later migration (the grade fallback still reads them).

## Review log (hadith pages checked by hand)

Each page is classified by cause. Add a row per page; if a cause shows up again, count it here before fixing it.

| Hadith (book number) | Findings checked | Known causes | New causes |
|---|---|---|---|
| Mughira «أبعد المذهب» (11 routes) | 7 | 3 missing relations (Ibn Ulayya, Qays, al-Sayduni), abridged text flagged as shudhudh | none left after Phase 8 |
| «إنما الأعمال بالنيات» (8 sources, 13 routes) | 13 | 5 missing relations (late narrators), unranked narrator, ziyadah false positive | matn commentary, dropped narrator, marfu typo |
| «الجنة أقرب إلى أحدكم من شراك نعله» (9 sources, 10 routes) | 5 | 2 missing relations (late narrators), unranked narrator counted weak (Muhammad ibn Ghalib, al-Bukhari) | matn commentary and editor notes (3 of 5 findings), tahwil head without a verb, wrong tahwil join |
| «الطهور شطر الإيمان» (16 sources, 22 routes) | 17 | 5 missing relations (late narrators), unranked narrators counted weak (3 Nakarah) | abridged text counted as omission (11 of 17 findings), matn commentary and editor symbols, parenthesis in the isnad, chain cut at Zayd (8 chains), «أخيه» relation |

## Done

| Date | Item | Commit |
|---|---|---|
| 2026-10-05 | Phase 8: grade from route tiers, compiler links, kin words, tahwil chains, travel notes, page fixes; app on V5 | `95fdedc` … `e897569`, docs `4b6f3cd` |
| 2026-10-05 | B5: low-confidence findings (Tanbih below 0.3) fold into a collapsed group; tab badges count only the visible ones | `HEAD` of the commit that added this file |
| 2026-10-05 | Scratch database `SmartHadithTree_ScratchC1` and `data/shamela_rijal_c1/` dropped (V5 and its v5 backup hold the same data) | not in git |
