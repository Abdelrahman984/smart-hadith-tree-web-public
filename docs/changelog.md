# Changelog

## 2026-10-07: Getting started points at the v7 backup

- **Problem**: the README downloaded and restored the v5 backup into `SmartHadithTree_ShamelaV5`, while the Api and Etl connect to `SmartHadithTree_ShamelaV7`. Someone following it literally got an empty database.
- **Fix**: README, `deploy/deployment.md` and `backups/README.md` now give the v7 Drive link, file name and restore command; `docker-compose.yml` connects to `SmartHadithTree_ShamelaV7` too (`docker-compose.prod.yml` is unchanged and still uses `SmartHadithTree`). The README also states that the AI key is optional, that Windows + a local SQL Server are needed, that the test project needs the .NET 10 SDK, and links the live demo.
- **Not checked**: the Docker restore with the v7 backup (the logical file names in the `MOVE` clause are assumed; confirm with `RESTORE FILELISTONLY`), and the Drive link from a signed-out browser.

---

## 2026-10-07: React warning «Cannot update a component while rendering» on the workspace (frontend)

- **Bug**: `HadithWorkspace` called `useWorkspaceStore.getState().init(...)` while rendering, which sets the store while other components (the workspace of the page being left) are subscribed to it. React logged «Cannot update a component (`HadithWorkspace`) while rendering a different component». Not related to the narrator names.
- **Fix** (`HadithWorkspace.tsx`): the defaults are set in a `useLayoutEffect` on mount, so they are still in place before the first paint (the panel does not slide open) and no longer during render. Type check and lint are clean; **not run in a browser** (no Api or database in the build environment): open `/takhreej` and `/tree/<id>` once and check the console and that the panel starts open on a wide screen.

## 2026-10-07: Narrators are named as the chosen matns name them

- **Nisbah added**: after the form is chosen, the narrator's own nisbah (any «ال…» word of his full name, also after «ويقال») is appended when an isnad writes it right after the name: «علقمة بن وقاص الليثي», «محمد بن إبراهيم التيمي», «يحيى بن سعيد الأنصاري». The nisbah also separates the two Yahya ibn Sa'id cards, so the frontend no longer swaps them for the three-part name («… بن قيس»).

- **Also fixed**: the single-tree query (`HadithChainRepository.GetIsnadTreeAsync`) is raw SQL and did not select the new `MentionedName` column, which would have broken `/api/Tree/<id>` (the unit tests do not run that query). It now selects `NULL AS MentionedName`; the single-hadith tree gets its mentioned name in `HadithSearchService`. **Not run against a real database**: check `/api/Tree/<id>` once.

- **Problem**: the tree showed the registry name (a long full name, or a name the books rarely use) while the isnads of the chosen hadiths write him as «سفيان» or «ابن عيينة».
- **Fix** (`NarratorMentionedName.cs`, `HadithSearchService`, `buildIsnadGraph.ts`): for each narrator the API builds candidate forms of his name (first name, first name + father, + grandfather, «ابن X», the nisbah, each `KnownAs` part), counts them in the isnad texts (`FullIsnadText`) of the hadiths he appears in, and returns the most frequent one as `mentionedName` (ties go to the longer form). A bare first name followed by «بن» counts for the longer name, so «محمد بن يحيى» is not read as «محمد». Diacritics, alef/ya/ta marbuta variants and «أبي/أبا» are ignored when matching. The cards use `mentionedName`, and fall back to the old formatting when no isnad names him (e.g. the compiler). Two cards that end with the same name are still told apart by the longer name. The narrator's `Kunyah` is also a candidate, and «عبد الله» / «عبدالله» match each other.
- **Found on real data** (takhreej of «إنما الأعمال بالنيات», 8 hadiths): shared forms gave wrong names, e.g. «أبو بكر» for al-Bayhaqi, al-Humaydi and three others, «أبو عبد الله» for محمد بن يعقوب, «محمد» for ابن يحيى القطان. Now (1) a form that two narrators of the same tree share is never used (`FormKeys`), and (2) a form without «بن» followed by «بن» in the text no longer counts («أبو بكر بن أبي شيبة» is not «أبو بكر»). Such narrators fall back to the old formatting, and two cards with the same name are told apart as before.
- **Second round on the same data**: a form shared by narrators of one tree now goes to the narrator whose isnads use it clearly most (الأنصاري gets «يحيى بن سعيد», ابن عيينة gets «سفيان», الثوري gets «الثوري»); nobody gets it on a tie. The leading «ال» («الليث») and a joined «و» («وسفيان») no longer stop a match. A form right after «بن» or «أبو» is part of a nasab or kunyah, not a mention («أبو عبد الله» did not name al-Humaydi «عبد الله»).

---

## 2026-10-07: A compiler is not drawn twice (frontend)

- **Bug**: when the first narrator of a chain was the compiler himself (أحمد بن الحسين for al-Bayhaqi, محمد بن يزيد for Ibn Majah), his narrator card sat right above the source card of his own book, the same person twice. Most source cards had it.
- **Fix** (`buildIsnadGraph.ts`, `bookCompilers.ts`): the compiler of each book is known from the resolver's `compiler` of the book's chains (`bookCompilers.ts`, the start of the registry name). A first narrator who is the compiler of every book he starts a chain in gets no narrator card: the source card stands in his place and his students link to it. A first narrator who is only the compiler's sheikh keeps his card, and the compiler as an intermediate sheikh in another book keeps his card there.

---

## 2026-10-07: Verbs with a final alef maqsura (resolver) and the V7 database

- **Bug**: al-Bayhaqi's edition writes «أخبرنى / حدثنى / أنبأنى» with ى; `VERBS` and the tahwil patterns knew only the ya forms, so «حدثنا يحيى بن سعيد، أخبرنى محمد بن إبراهيم التيمي» stayed one segment and Muhammad ibn Ibrahim was lost: the tree drew Yahya ibn Sa'id hearing straight from Alqama (Sunan al-Kubra 15101). `normalize_verbs` (`gap_test.py`, used by `chain_segments_verbs` and `tahwil._clean`) turns these verbs into the ya forms before matching. The same text also polluted names («أخبرنى عمى عمارة», «حدثنى أبى», «وحدثنى على»).
- **Measured** on all 274,597 chains, before and after: 3,549 records change, 3,512 of them in Sunan al-Kubra (+3,081 names, +4,533 resolved, 2,014 records longer); the other 30 books change by 1 to 14 records. 57 records get shorter, because a name gained at the front pushes the tail past the 8-name cap of a single chain (found in 4 of 10 sampled; the cap is old, see `docs/backlog.md`). A random sample of 10 changed records read by hand: 9 clearly better, 1 unchanged.
- **Database `SmartHadithTree_ShamelaV7`** built from the new chains (registry unchanged, so no pipeline run): 1,266,022 transmissions (v6: 1,262,178), narrators, hadiths and Ilal data as in v6. Backup `backups/SmartHadithTree_Shamela_v7_2026-10-07.bak` (verified). The nine Ilal pages: findings 121 -> 119, matn 47 -> 46, Qadihah 10 -> 9, every grade unchanged (`docs/challenge/evaluation/ilal-pages-v7-2026-10-07.json`); the niyyat page 11 -> 9 findings, Qadihah 1 -> 0. The Api and Etl `appsettings.json` now point at V7. Every hadith Guid differs from v6, so saved `/takhreej?ids=` and `/tree/<id>` links of v6 do not work.

---

## 2026-10-07: Source card split from the first narrator (frontend)

- **Bug**: the card of a chain's first narrator was drawn as «المصدر والمُخَرِّج» and named after a lookup on his full name, so Yahya ibn Sa'id al-Ansari showed as «مالك بن أنس» («... بن مالك بن النجار» matched «مالك») and Ahmad ibn Ishaq as «أحمد بن حنبل». Sufyan ibn Uyaynah showed as «البخاري».
- **Fix** (`buildIsnadGraph.ts`, `formatFamousReferenceName.ts`): the first narrator is an ordinary narrator card. The book gets its own source card (book, number, compiler named from the title alone, «للطبراني» expanded to «ل الطبراني» so it matches) below him; in the comparative tree one card per book, listing the numbers of its hadiths and linked to every first narrator of their chains (al-Bayhaqi's three hadiths are one card, «أرقام 184, 2287, 15101»). Source cards open no narrator details and are left out of the narrator find box.
- **Same short name, different people**: two narrators with different ids but the same two-part name (Yahya ibn Sa'id al-Ansari and al-Qattan) looked like one card drawn twice. Such cards now show the name down to the grandfather («يحيى بن سعيد بن قيس» / «... بن فروخ»); unique names stay short.
- Tests: `graphBuilders.spec.ts` updated; `graphPages.spec.ts` counts updated; the whole suite passes against the mock API.

---

## 2026-10-06: Siyar as a second fallback registry; the fallback made complete (resolver, applies at the next rebuild)

- **`parse_siyar.py`** writes `extra_siyar.json` from the dumped Siyar (5,772 entries, shaykh and student lists, headers from the table-of-contents titles), marked `fallback`.
- **Fallback fixes** (`link_tahdhib.py`, `chain_resolver.py`, `gap_test.py`): the name indexes are split before the Taqrib and cross-reference aliases are attached; `DEFAULT_FOR` and the compiler entry ignore fallback entries; fallback entries merge only with each other.
- **Measured** on all 274,597 chains, the two books on or off, branch by branch: 11,267 names gained, 4,145 gaps closed against 511 opened, 104 names lost, 257 real swaps; undecided names inside chains 89,497 to 83,935 (-6.2%). Bench: 428 gained, 14 lost, agreement unchanged. Details and the earlier failed attempts: `docs/backlog.md` §2.

---

## 2026-10-06: Lisan al-Mizan as a fallback registry; the same-person merge made deterministic (resolver, applies at the next rebuild)

- **Fallback entries** (`link_tahdhib.py`): entries marked `fallback` get their own name indexes, used only when a name matches nobody among the other entries, so their namesakes no longer turn «أنس بن مالك» or «بندار» into ties. `make_extra_lisan.py` writes `extra_lisan.json` from `lisan.json` (9,072 entries, headers cut at the first narrating word).
- **Measured** on all 274,597 chains (scratch export, V5 chains untouched): 2,477 gaps closed against 787 opened, 6,108 chains gained narrators, 831 lost some; undecided names inside chains 89,497 to 86,486. Bench: 274 names gained against 85 lost, agreement with the current system unchanged. Details and the history of the earlier attempts: `docs/backlog.md` §2.
- **Fix**: `merge_same_person` gave 611 or 612 merged entries depending on the hash seed, so the cached indexes could disagree with the live ones (`IndexError` in `father_of`). Fixed order; the same count in six seeds.

---

## 2026-10-06: Registry, same narrator in two books (resolver, applies at the next rebuild)

- **`link_tahdhib.py`**: entries of different books that are one narrator are merged (`merge_same_person`): three generations of the nasab agree (or two, with the same nasab tail or a rare shared nisba), a shared nisba, kunya or the same nasab confirms it, and no nisba, kunya, death date or great-grandfather contradicts. Never two entries of one book or of Tahdhib. 212 entries merged (23,502 to 23,290); the merged narrator keeps the other header's words in the matching indexes; `registry.json` gets `merged_ids`; `link_ilal.py` follows an override id that was merged away. Review with `SHOW_MERGED=<seed>`; `NO_MERGE=1` switches it off.
- **Measured** on all 274,597 chains (scratch export): 719 gaps closed against 57 opened, 1,344 gained narrators, 48 lost some; undecided names inside chains 90,730 to 89,497. Ilal links unchanged (158 mudallisin, 131 mukhtalitun). Details and the analysis of what the remaining ties are: `docs/backlog.md` §1.

---

## 2026-10-06: Isnad segmenter (resolver, applies at the next rebuild)

- **`gap_test.py`**: brackets and parentheses in the isnad are dropped with their content kept (an editor's restored narrator); «يحدث» and «قرئ على / قرأت على / كنت أسمع» are verbs; «ز-» is dropped; «النبى» / «نبي الله» end the isnad; «أنه / أنها» at the end of a segment and «بلغه / بلغني» are not part of a name, and «بلغه أن…» ends the isnad.
- **Measured** on all 274,597 chains (scratch export): 18,002 changed, 4,701 gaps closed against 943 opened, 11,327 gained narrators, 413 lost some; undecided names inside chains 95,652 to 90,730. Ibn Abi Shayba 37 now resolves «زيد» to Zayd ibn Sallam (was Zayd ibn Nu'aym). Details and what is not fixed: `docs/backlog.md` §1.
- **Tools**: `rijal_pilot/segment_checks.py` (14 fast checks of the segmenter, no registry) and `rijal_pilot/chain_report.py` (where the resolved chains break). Nothing is loaded into a database yet.

---

## 2026-10-06: Ilal matn comparison, fewer false findings (backlog §3 and §4)

- **Abridged texts are not omissions** (`MatnAtMadarRule`): a text with no content word of its own and at most 3 in all (`MaxFragmentWords`) that sits wholly inside a longer one is an abridgement, not an addition or omission. `Classify` returns `None` for it, and the rule leaves it out of the clusters so it counts as support for no version. «يقول» is now a framing word. `MatnVariation` (variant highlighting on the comparative page) calls `Classify`, so a fragment is no longer shown as a variant there.
- **Notes and noise are cut from the matn before comparing** (`MatnText.ExtractBody`, no rebuild needed): `[...]` spans (editor notes, page markers); «رواه / أخرجه / لم يرو / تفرد به / حكم حسين / هذا الحديث»; pointers («فذكر مثله», «وذكر الحديث», «بمثل حديث»); the printed edition's symbols; and a second isnad pasted into the record (only once the matn start is known, and not «حدثنا رسول الله»). The TypeScript port `frontend/src/features/isnad-tree/utils/matnDiff.ts` is kept in step.
- **Scattered one-word swaps are not a contradiction** (`Classify`): the old rule counted two scattered single-word swaps (خشبه / خشبته) as a contradiction while ignoring one; only a multi-word site counts now.
- **Measured** on 9 pages (4 reviewed, 5 not tuned on), live API, before and after: matn findings 83 to 68, of which «قادحة» 42 to 33. Tahur 11 to 4 (3 «قادحة»), Halal 16 to 10 «قادحة», Jannah 3 to 1. Nasiha rose from 18 to 21 matn findings (11 «قادحة» before and after): more Ziyadah (5 to 7) now that abridgements no longer hide them. What is left is mostly real wording and length differences between versions, an unranked narrator counted weak (backlog §3) and one-off commentary the cleanup does not know.
- **Tests**: 243 backend (`AbridgedMatnTests`, `MatnCleanupTests`), 10 in `tests/matnDiff.spec.ts`.
- **A narrator with no grade is not weak** (`MatnAtMadarRule`, backlog §3 cause 3): `IlalNarrator.IsRanked` / `NarratorGradeScale.IsRanked` tell a verdict (Ibn Hajar's rank or a recognized legacy grade) from the placeholder tier 7. Only a side whose students are all graded and weak is «منكرة»; an unranked contradicting student gives a «تنبيه» («مخالفة في المتن، وراويها غير محرر», confidence 0.35, shown in the panel), and an unranked adder gives «زيادة تحتاج إلى نظر» worded «غير محرر», not «دون الثقة». Branch strength (`Compare`) still uses the placeholder tier, and RafWaqf and WaslIrsal still weigh an unranked student as tier 7.
- **Measured again** on the same 9 pages: Nakarah 13 to 0 (all 13 were unranked narrators), matn «قادحة» 33 to 20 (baseline 42 before this work). Ranked weak narrators are still «منكرة» (test). The real fix for the missing grades is data (backlog §2: the Siyar and Thiqat books, the critics' quotes).
- **Tests**: 251 backend.

### Same day, second batch: the code-only items of the backlog (no database change)

- **Marfu** (`MatnText.IsMarfu`): «سمعت رسول ﷺ» (a printed edition without «الله») is marfu: the honorific after «رسول» says whom it means. «رسول كسرى» is not.
- **Additions and omissions are a stretch, not scattered words** (`MatnAtMadarRule.Classify`): an addition, omission or two-sided contradiction needs one run of 3 or more content words with little or nothing opposite it (`LongestExcessRun`). A repeated phrase that the alignment pairs with the wrong occurrence, or many small wording changes, no longer add up to a «زيادة». A run with 2 words or more opposite it is a substitution and is decided by its site, after the addition and omission checks (so a real omission is not turned into a contradiction by two adjacent swapped words). «لفظ حديثهما» joins the commentary that is cut.
- **A side weighed only on a placeholder tier is not «قادحة»** (`RafWaqfRule`, `WaslIrsalRule`, `IsnadBranching.AllUnranked`): when every student of the weaker side has no grade the finding is a «تنبيه» and says so.
- **Shawahid are kept out of the comparison, by the server** (`Shawahid`, `IlalContext.WithChains`): a narration that reaches a graded Companion other than the one most narrations reach is a witness. The rules and the madars see the routes only; `IlalTariqDto` carries `CompanionId`, `CompanionName` and `IsShahid`, and the summary says how many were set apart. A chain that merely stops short of a Companion stays a route (the frontend-only version of this called such a chain a shahid). Witnesses still count for the grade. `groupSources.ts` uses the server's split and falls back to reading the chains' tops when the API does not send it.
- **Missing teacher-student links of ungraded narrators are «بيانات ناقصة»** (`HiddenInqitaRule`): when either narrator has no grade (a late narrator outside the biographical books) the finding is titled «لم يثبت اللقاء (بيانات ناقصة)» with confidence 0.2, so the panel folds it away. On the 9 pages, 40 of 69 such findings involved an ungraded narrator.
- **An ungraded narrator is not a weak link in the grade** (`IlalAnalysisService`, `TaqwiyahService`): `WeakestTier` counts graded narrators only and `UnratedNarratorCount` says how many were left out. A route through an ungraded narrator is judged only when its graded narrators already make it weak; otherwise it is left out. A hadith none of whose judged routes is sound, with routes left out, is «غير محرر», not «ضعيف».
- **Checked, no change needed**: the canvas edge styling already follows the panel's low-confidence toggle; the 0.3 threshold sits between the two clusters of Tanbih confidences (0.2 and 0.25 for data gaps, 0.35 and above for the rest) on the 9 pages. Tabarani 7050 («وبه») is stored correctly (Yahya, Muhammad ibn Ibrahim, Alqama, Umar); the false «شذوذ» that named it comes from Bayhaqi 15101, whose chain drops Muhammad ibn Ibrahim (a resolver item, backlog §1).
- **`scripts/eval_ilal_pages.py`**: the nine-page measurement as a script (`--save`, `--compare`), to be run again after the database rebuild.
- **Measured** (the 9 pages, against the last commit): findings 176 to 127, matn «قادحة» 33 to 11 (42 before this work), grades unchanged on all nine. 271 backend tests, 133 frontend tests.

---
## 2026-10-05: Production deploy and CI/CD

- Live at https://smart-hadith.idealisticsolutions.com (one hostname: `/api/*` goes to the API), behind the host's shared edge Caddy. Database restored from `SmartHadithTree_Shamela_v5_2026-10-05.bak` as `SmartHadithTree`.
- **Auto deploy on merge to `master`**: `.github/workflows/deploy.yml` (tests + lint, then SSH) -> `deploy/ssh-deploy-gate.sh` (forced command, only commits on `master`) -> `deploy/deploy-remote.sh` (pre-migration backup, build, health checks, automatic rollback). Runbook: `deploy/devops-handoff.md` §6.

---

## 2026-10-05: Tree and takhreej pages, step 4b: matns tied to the graph, shawahid

- **Matn cards act on the graph.** A card's book badge highlights that book's routes (and the card gets a ring); «الأصل» marks the first route; every other card has «قارن بالأصل», which shows a word-level comparison under it with the same `MatnDiffView` the Ilal findings use (additions in green, omissions struck through, similarity %).
- **The comparison cuts the isnad first.** `utils/matnDiff.ts` is a TypeScript port of the server's `MatnText.ExtractBody` / `NormalizeForComparison` and `MatnAligner` (LCS), so the browser's diff aligns the same words the Ilal rules do; without it two different isnads would drown the matn. Its tests reuse the expected strings of the server's `MatnAlignerTests.cs`; keep the two in step.
- **Turuq and shawahid** (`utils/groupSources.ts`): a narration whose chain ends at another Companion than most is shown apart under «الشواهد» with «شاهد: <الصحابي>», marked «شاهد» on its chip, and counted «(منها شاهد)» in the summary. This is display only: the tree, the madar and the Ilal findings still treat it as a route (the separation is a server change, backlog §3).
- **The toolbar no longer covers the first row of cards**: the fit reserves its height as it already reserved the legend's width.
- **Tests**: 129 in all (the diff port against the server's cases, grouping, the matn cards, shawahid), and the fixture API has a witness narration (`ids=h1,h2,h4`).

---

## 2026-10-05: Tree and takhreej pages, step 4a: one toolbar

- **`GraphToolbar`** replaces the weak-highlight/export panel, the React Flow zoom controls and the legend's own collapsed button: find, «إبراز الضعفاء», card detail, minimap, legend, zoom in/out/fit and export are one bar (icon buttons with labels for screen readers and tooltips, scrolling sideways on a phone).
- **Find a narrator**: a combobox over the graph's narrators. It ignores diacritics, hamza and taa marbuta (`قتيبه` finds `قتيبة`), ranks the phrase as typed first, and Enter selects the card and brings it into view.
- **Card detail**: «مضغوط/مفصّل». Compact cards show the name and grade only; the choice is automatic above 25 narrators. Changing it lays the graph out again with the new card sizes without the graph blinking out.
- **Minimap**: on by default on a wide screen when the graph has more than 25 narrators; switchable from the toolbar; bottom-left, so it never covers the legend.
- **Dynamic title** for takhreej («تخريج صحيح البخاري 1 و2 روايات أخرى»), built from book and number because a stored matn can still begin with the isnad.
- **Resizable side panel**: drag its edge (or use arrow keys, Home/End) between 320 and 640px; the width and the last tab are remembered.
- **Tree header**: the book name links to its chapters, and on a phone the ruling note collapses behind a «تفاصيل» button (a first version wrapped the evidence badge in a button, which nested buttons and caused a hydration error).
- **Tests**: 111 in all, including 12 for the toolbar, search, density and minimap, and a fixture graph of 35 narrators (`ids=huge`).

---

## 2026-10-05: Tree and takhreej pages, step 3: one workspace

- **`HadithWorkspace`** is the page both screens share: site header, a header block, an optional summary strip, the graph, and a side panel of real ARIA tabs (`role=tablist/tab/tabpanel`). `TreeWorkspace` and the takhreej page only say what goes in each slot. The takhreej sidebar became the «المتون» and «العلل» tabs; panel and tab state live in `useWorkspaceStore`, so the summary strip, chips and findings open tabs without prop plumbing.
- **The narrator panel no longer covers the graph.** From 1024px up, a selected narrator opens as an «الراوي» tab of the side panel (no backdrop; the card stays selected); below that it is still the drawer over the graph. `NarratorDrawer` was split into `NarratorDetails` (the body) and a thin modal wrapper.
- **The address keeps the view**: `?tab=ilal`, `?narrator=<id>` and `?book=<name>` are written with `history.replaceState` (other parameters such as `ids=a,b` are left exactly as they were) and restored on load, so a link reopens the same view.
- **Tree page**: tabs «المتن» (full text, copy, link to the book) and «العلل» (fetched the first time the tab is shown, as before); a primary «التخريج المقارن» button gathers the related routes and opens the comparison; «فحص العلل» opens its tab. The ruling note stays in the header. `IlalLauncher` (the old fixed drawer) was deleted. On a phone «عودة للبحث» is icon-only so the buttons fit one row.
- **Escape peels one layer per press**: narrator details, then the selection, then the book focus. It listens in the capture phase, because React Flow deselects a focused card on Escape and that update removed the old handler before it ran.
- **Tests**: 92 in all (panel, tablet drawer, address round-trip, the tree workspace, the phone header row).

---

## 2026-10-05: Tree and takhreej pages, step 2: selection, clearer data, a summary

- **Tap, click or Enter selects a narrator and keeps its chain highlighted** (sheikhs above, students below) until Escape or a click on empty canvas; hovering only previews while nothing is selected. Enter on a focused card opens the details like a click. Touch screens no longer depend on hover.
- **«غير مُقيَّم»**: a narrator with no grade is shown neutrally (dashed card and badge, also in the drawer and the legend) and is not treated as weak by «إبراز الضعفاء». Before, a missing grade looked like nothing was said.
- **Quieter edges**: on a graph of more than 12 links, edge labels («انقطاع», «عنعنة مدلس», …) become small icon markers with a tooltip, and the full label prints on the chain you select or hover.
- **Takhreej summary strip** above the graph: routes and books, the madar (a chip that selects it on the graph), the automatic grade with its «يتطلب تحققاً» badge, and the findings by severity (a button that opens the «العلل» tab). Before, the grade was only in the sidebar.
- **Source chips are interactive**: click one to highlight that book's routes (and scroll to its matn when the sidebar is beside the graph); ✕ removes a narration by updating `?ids=` (two must remain). The page keeps the current graph while the next one loads (`keepPreviousData`) instead of replacing everything with a spinner.
- **Findings act on the graph**: picking one in the panel brings its narrators into view; on a phone it also closes the panel/sidebar so they can be seen (takhreej and tree pages).
- **Low-confidence findings are not drawn on edges** unless the panel's «ملاحظات منخفضة الثقة» group is open (backlog §3, "Panel"); that state now lives in `useIlalStore`.
- **The legend no longer covers the graph**: while it is open beside the graph, the graph is fitted into the space left; its open state moved to `useGraphViewStore`.
- **Tests**: 13 more e2e tests and 8 more unit tests; the fixture API honours `ids` (and serves a 14-link graph for `ids=big,h1`). `next.config.ts` gets `allowedDevOrigins: ["127.0.0.1"]`: since Next 16.3.8 `next dev` blocks dev resources from that origin, which Playwright uses, and the page never hydrated.

---

## 2026-10-05: Tree and takhreej pages, step 1: one graph core

- **One canvas, pure builders.** `IsnadGraphCanvas` now draws both the single tree and the takhreej tree; `TreeCanvas` and `ComparativeTreeCanvas` are 17-line wrappers. The transmissions-to-graph logic left the canvases for pure functions, `buildSingleGraph` and `buildComparativeGraph` (`utils/buildIsnadGraph.ts`), with the parent lookup now a `Map` instead of a scan per transmission. `ComparativeNarratorNode` was merged into `NarratorNode` (badges in `NarratorBadges.tsx`), so a madar ring now also shows on the single tree once its Ilal report is loaded.
- **One edge rule table** (`utils/edgeStyle.ts`): انقطاع, then اختلاف باللفظ, then a link shared by several books, then one book's colour, then grey; Ilal findings are laid over that. A broken link now stays red and shows both labels instead of taking the finding's colour.
- **Fix:** when the same narrator pair appeared in hadiths of different books, its edge kept the first book's colour; it is now drawn as a shared link, as the legend says.
- **View state** (`store/useGraphViewStore.ts`): «إبراز الضعفاء», the hovered narrator and the focused book no longer rewrite every node's data; the legend takes its grade colours from `gradeStyle.ts`.
- **Tests without the database**: `tests/fixtures/mock-api.mjs` is a fixture API (started by `playwright.config.ts` on :5147; the graph specs skip themselves when a real API answers there). New `graphBuilders.spec.ts` (11 unit tests) and `graphPages.spec.ts` (13 tests: both pages, the drawer, the Ilal overlay, book focus, the phone layout). Assertions now wait up to 15 s because `npm run dev` compiles pages on first request.

---

## 2026-10-05: Logo, theme tokens and the sources page

- **Logo**: the "logo" was a generic lucide icon. There is now an original mark, an isnad tree (a teal root branching into narrator nodes on a navy square): `components/Logo.tsx`, used in the header and footer, plus `app/icon.svg`, `apple-icon.png`, a multi-size `favicon.ico` (the default Next.js icon was replaced) and `viewport.themeColor`. The five unused create-next-app SVGs in `public/` were removed; `public/.gitkeep` stays because the Dockerfile copies `public/`.
- **Theme tokens** (`globals.css`): `surface`, `surface-muted`, `line`, `ink`, `ink-muted`, `ink-subtle` replace about 300 raw `bg-white`/`border-slate-200`/`text-slate-*` classes (same values, so no visual change). Teal used as text is now `brand-teal-ink` (`#0B7A83`, 5.1:1 on white); the old `#00C2CB` was 2.2:1. The graph legend reads its grade colours from `gradeStyle.ts`. The look stays light; see `docs/design-system.md`.
- **`/sources`** (new, «المصادر والمنهج»): where the hadith texts, the الجرح والتعديل data and the العلل rules come from, what is taken from each book, the method and the known limits. It lists تهذيب الكمال, Shamela's narrator encyclopedia, تقريب التهذيب, the compilers' shaykh books, طبقات المدلسين, الكواكب النيرات and المختلطين, the six Ilal rules with their severities, and how branches are weighed. Content is in `features/sources/sourcesData.ts` (figures dated 2026-10-05, from `docs/shamela_migration.md` and `docs/ilal.md`). Linked from the header, the footer, the narrator drawer («مصادر الأقوال»), the Ilal panel («مصادر العلل وقواعدها») and the glossary.
- **Header**: five sections now; the wordmark shows from 1024px and the items shrink below 360px, so it fits from 320px up (tested at six widths).
- **Footer**: small-print text contrast raised (`slate-500` to `slate-400` on navy).
- **Tests**: `sources.spec.ts`, and `navigation.spec.ts` covers `/sources` and the header at six widths.

---

## 2026-10-05: Frontend layout and UI/UX fixes

- **Broken flows**: on `/verify`, «التخريج والعلل» sent a single id to `/takhreej`, which needs two; it now gathers the related narrations first, the same way as «تخريج فوري» on search (`useAutoTakhreej`), and opens the single tree when there are none. The narrator drawer's grade badge was always green, even for كذاب/متروك. One map, `features/narrator-details/utils/gradeStyle.ts`, now colours the drawer, both node types and the legend's grades. On search, the book filter and the count say when they cover only the current page, and they reset on a new search or page. `alert()` was replaced by an inline message.
- **One site header** (`components/SiteHeader.tsx`, `NavLink.tsx`) on every page, with a compact variant on the tree and takhreej screens. It shows search, books, verify and glossary, marks the current page with `aria-current`, and stays on one row on phones. The footer is shared too (`SiteFooter.tsx`). Every page has its own `<title>` (a template in `layout.tsx`); search, takhreej and verify are server `page.tsx` files that render a client component from `features/`.
- **Books**:
  - `/books` shows the same grouped catalogue as the home page (`features/books/CorpusCatalog.tsx`, data in `lib/corpus.ts`), with no API call.
  - The book and chapter pages are server pages with breadcrumbs. Hadith rows are links, so they work from the keyboard and open in a new tab.
- **Route states**:
  - `tree/[hadithId]` has `loading.tsx` and `not-found.tsx`. `getIsnadTree` throws `NotFoundError` on 404; any other failure shows `app/error.tsx` with a retry button.
  - There is a site-wide `app/not-found.tsx`.
  - Loading, error and empty blocks share `components/StateViews.tsx`.
- **Phones**:
  - The takhreej sidebar starts closed below 768px and opens over the canvas.
  - The narrator and Ilal drawers take the full width.
  - The legend starts collapsed.
  - The tree header clamps the matn ("عرض المتن كاملاً").
  - A node's anomaly reason and travel note also show in the narrator drawer, since hover tooltips don't work on touch screens.
- **Accessibility**:
  - Nodes show the grade as text.
  - The مدلس/اختلط badges have readable contrast.
  - The narrator drawer, the Ilal panel and the advanced-search modal share `useDialogFocus`: focus moves into the dialog and stays there, Escape closes it, and focus goes back to the element that opened it.
  - Only one side drawer is open at a time.
  - Selecting matn text no longer toggles a search card.
- **Polish**: `tw-animate-css` was added, so the existing `animate-in …` classes now run. `scrollbar-none`, which isn't a Tailwind class, was removed.
- **Tests**: `home.spec.ts` and `search.spec.ts` looked for text that is no longer on the page and were updated. The new `navigation.spec.ts` checks, without the API:
  - the header is on each page;
  - no page scrolls sideways at 375px;
  - the current-page marker;
  - the 404 page;
  - the takhreej page with fewer than two ids.

---

## 2026-10-05: Verify, search relevance and optional AI check

- **`/verify`** (`POST /api/Verify`): checks a pasted text against the 31 books (retrieval by word stems and word windows, ordered word-pair scoring, a negation guard in code, a restricted model review with a verified quote). Answers: exact, variant, not-found, invalid; it never invents a source or a ruling. Notes in `docs/verify-feature-handoff.md`; 29 evaluation cases in `docs/challenge/verify-eval-cases.json` run by `scripts/eval_verify.py`.
- **`/search`**: every result has a relevance score and a reason computed by the server (`RelevanceScorer`: coverage, closeness in words, phrase/order, repeated query words must repeat); a long hadith that contains the exact phrase is no longer cut by the shortest-first candidate cap; the search runs on the search button or Enter, not while typing (3 characters minimum).
- **Optional AI check of a page of results** (`POST /api/Search/ai-judge`, button on `/search`): labels match / partial / scattered with a quote verified in the matn; an unverifiable answer is "not judged"; hiding "scattered" results is the user's switch and is always counted. Evaluation: `scripts/eval_search.py`, plan and findings in `docs/search-relevance-plan.md`.
- **Operations**: the AI endpoint is rate limited per client (`RateLimit__AiPermitPerMinute`, default 10 per minute); the API now reads `X-Forwarded-For`/`X-Forwarded-Proto` from the reverse proxy (`ForwardedHeaders__Enabled`, default on, last hop only), otherwise every visitor behind Caddy/nginx would share one rate-limit bucket. Time limits: `SEARCH_AI_TIMEOUT_SECONDS` (default 20), `VERIFY_REVIEW_TIMEOUT_SECONDS` (default 8). `scripts/model_latency.py` times the configured chat model directly. `Together__ExtraBody` (a JSON object; Docker: `TOGETHER_EXTRA_BODY`) is merged into every chat request. `{"reasoning_effort":"low"}` removes the reasoning tokens of GLM-5.3-Flash on Together: ten passages take 2.8–4.3 s instead of 8–27 s (measured with `model_latency.py --extra`; three other fields did not work); the search-check prompt now asks for short undiacritized quotes and reasons to cut output tokens.

---

## 2026-10-05: Shamela data and review fixes (merged into `master`)

- **Data source**: all 31 books and the narrator registry are built from Shamela 4 (Tahdhib al-Kamal, Taqrib and the compilers' shaykh books); Itqan is no longer read. Database `SmartHadithTree_ShamelaV5`: 274,597 hadiths, 1,217,978 transmissions, 23,502 narrators, 116,670 quotes, 115,816 relations. The migration is documented in `docs/shamela_migration.md`.
- **Chains**: multi-chain isnads (tahwil) are kept as several chains; first-person «أبي» and «جده» are resolved; the compilers' own teacher lists were added.
- **Grading**: the overall grade is computed from each route's weakest narrator above the compiler.
- **Ilal and the comparative page**: fewer false findings (the compiler's link, matn framing words, a medoid text as the base, the order of the requested hadiths); geographic mismatch is a travel note, not a break; low-confidence findings fold into a group; fixes to the header badges and the refit on resize.
- **Challenge work merged** (`claude/happy-johnson-5vqndl`): evidence-status badges, disagreement between critics, a glossary, an AI notice, accessibility fixes, Docker packaging and the challenge documents.
- **Docs**: `docs/backlog.md` (open work and how to review a page), `docs/challenge/sources-and-licenses.md`.
- The previous master is kept as the tag `legacy-master-itqan` and the branch `legacy/itqan-master`.

---

## 2026-09-06: Advanced features integration (Itqan-based, superseded)

**Date:** September 6, 2026
**Summary:** This session focused on integrating a robust dataset (Itqan) and building advanced analytical, AI, and interactive features on top of the base graph architecture.

## 1. Itqan Dataset Integration (ETL)
Replaced legacy/manual data ingestion with a fully automated ETL pipeline for the open-source `Itqan` repository.
- **Data Extracted**: Over 115,000 unified narrators, 7,200 Hadiths from Sahih Al-Bukhari, 22,000 transmission links, and 106,000 scholar evaluations (Jarh wa Ta'deel).
- **Backend Changes**: Added `ItqanId` and `ItqanGrade` to the `Narrator` EF Core entity. Built `ItqanDatasetParser.cs` to ingest and map the complex JSON schema directly into SQL Server.

## 2. RAG & AI Integration (Phase 1)
Implemented a Retrieval-Augmented Generation (RAG) feature using Microsoft Semantic Kernel.
- **Backend (`AiEvaluationService`)**: Created a service that pulls all classical scholar evaluations (e.g., Al-Dhahabi, Ibn Hajar) from the database and injects them into an LLM prompt. The LLM acts as an expert Hadith scholar, synthesizing the conflicting quotes into a single, cohesive Arabic verdict.
- **Frontend (`NarratorDrawer.tsx`)**: Wired up a UI button to trigger the API endpoint and stream the AI summary to the user.

## 3. Advanced Full-Text Search (Phase 2)
Polished the global search functionality to allow researchers to find Hadiths by their raw Arabic text (`MatnArabic`) and book source.
- **Implementation**: The backend normalizes Arabic search inputs and uses `Contains` to find exact matches across texts and narrator aliases.
- **UI**: A dedicated `/search` page routes users to the interactive tree view for any matched Hadith.

## 4. Graph Analytics & Inqita' Detection (Phase 3)
Added chronological anomaly detection to the Isnad engine to flag mathematically impossible transmission chains.
- **Backend Logic**: Post-processes the recursive CTE output to compare a Student's `BirthYearHijri` against their Sheikh's `DeathYearHijri`. If the student was born after the sheikh died, the node is flagged with `IsAnomaly = true`.
- **Frontend Visualization**: The React Flow engine (`TreeCanvas.tsx`) renders anomalous edges as thick, dashed red lines labeled "انقطاع" (Inqita'). The `NarratorNode` component also displays a `lucide-react` warning icon with the exact reason on hover.

## 5. Interactive UI Filters & Exports (Phase 4)
Gave users more control over the complex graph canvas:
- **Filter**: Added a "Highlight Weak Links" toggle via `GraphControls.tsx`. This dynamically adjusts the opacity of all `reliable` narrators down to 30%, isolating problematic/weak links in the chain for quick visual inspection.
- **Export**: Integrated `html-to-image` to capture the entire React Flow viewport, allowing academics to download high-resolution, perfectly scaled PNGs of the Isnad trees for sharing.

## 6. Book Exploration & ETL Cleanup (Phase 5)
Implemented an end-to-end flow to hierarchically browse the Hadith corpus and finalized the adoption of the Itqan dataset.
- **Backend (`BooksService`)**: Created a dedicated service to dynamically aggregate available Books and Chapters directly from the `HadithText` metadata, ordering chapters sequentially by their starting `HadithNumber`.
- **Frontend Pages**: Added a hierarchical browsing flow (`/books` -> `/books/[bookId]` -> `/books/[bookId]/chapters/[chapterId]`) allowing users to drill down from a Book to a Chapter to a list of Hadiths.
- **Data Cleanup**: Ran a SQL migration to normalize the `FawazAhmed` generated "كتاب 1" placeholders in the database to their authentic Arabic Sahih al-Bukhari names (e.g., "كتاب الإيمان").
- **ETL Optimization**: Purged legacy parsers (`FawazAhmedParser`, `ShamelaAuthorParser`, `JsonHadithParser`) and their raw data files from the repository. Upgraded the `ItqanDatasetParser` with a hardcoded static map of all 97 Bukhari chapters to ensure future ingestion runs natively produce Arabic chapter metadata.

## 7. Full Itqan Corpus Ingestion (All 18 Sunni Hadith Collections)
**Date:** September 7, 2026
Expanded the repository from 4 collections to the complete 18 Sunni Hadith collections in the `Itqan` dataset.
- **Data Ingested**: Ingested 88,839 additional Hadiths and 148,188 transmission links across 1,418 chapters. The total corpus now stands at **112,813 Hadiths** and **225,807 Transmissions**, fully unified with the 115,735 narrators.
- **ETL Parser Upgrade (`ItqanDatasetParser.cs`)**:
  - Automatically loads existing narrator ID mappings directly from the database to prevent duplicate narrator insertions and preserve foreign-key integrity.
  - Detects already imported collections (`صحيح البخاري`, `صحيح مسلم`, `سنن أبي داود`, `جامع الترمذي`) and skips them cleanly.
  - Dynamically discovers all book subdirectories in `data/itqan/sunni/` and uses `index.json` to extract authentic Arabic chapter titles for every chapter without hardcoding.
  - Maps compiler Itqan IDs for all 18 collections and falls back to chain-initiating sheikhs if the compiler is uncatalogued.
- **Collections Active**:
  1. مصنف ابن أبي شيبة (37,943 hadiths)
  2. مسند أحمد (26,539 hadiths)
  3. صحيح مسلم (7,368 hadiths)
  4. صحيح البخاري (7,277 hadiths)
  5. سنن النسائي (5,905 hadiths)
  6. سنن أبي داود (5,276 hadiths)
  7. مشكاة المصابيح (4,447 hadiths)
  8. سنن ابن ماجه (4,321 hadiths)
  9. جامع الترمذي (4,053 hadiths)
  10. سنن الدارمي (2,953 hadiths)
  11. موطأ مالك (1,860 hadiths)
  12. بلوغ المرام (1,767 hadiths)
  13. الأدب المفرد (1,326 hadiths)
  14. رياض الصالحين (1,245 hadiths)
  15. الشمائل المحمدية (411 hadiths)
  16. الأربعون النووية (42 hadiths)
  17. أربعون شاه ولي الله (40 hadiths)
  18. الأربعون القدسية (40 hadiths)

---

# Changelog: Ilal Engine (علل الحديث)

**Date:** October 2, 2026
**Summary:** Added a rule-based engine that detects hidden defects (علل) across the turuq of a hadith, with an optional AI explanation and canvas overlays. The design is described in `docs/ilal.md`.

## Backend
- **Domain**
  - `Narrator` gained `MudallisTier` and `IkhtilatNote`.
  - New entities `NarratorRelation` (teacher/student graph) and `MukhtalitHearing` (before/after ikhtilat).
  - New enums `IllahType` and `IllahSeverity`.
  - New utilities `MatnText` and `MatnAligner` (word-level LCS diff).
- **Migration `AddIlalEngine`.** Adds the new tables and columns. It also adds the drift columns that earlier commits put in the model without a migration (`ItqanId`, `ItqanGrade`, the Gawami columns, `HadithClusters`). Those statements use `IF NOT EXISTS`, so the migration also works on databases that were patched by hand.
- **Application.** Six rules in `Services/Ilal/Rules`: `TadlisRule`, `IkhtilatRule`, `HiddenInqitaRule`, `MatnAtMadarRule`, `RafWaqfRule` and `WaslIrsalRule`.
  - `IlalAnalysisService` runs them.
  - `IlalExplanationService` writes the Gemini explanation.
  - `NarratorGradeScale` is now the grade-to-tier mapping shared with `TaqwiyahService`.
  - `TaqwiyahService` downgrades the grade to "ضعيف (معلول)" when every tariq has a decisive defect.
- **API**
  - New endpoints `GET /api/ilal?ids=`, `GET /api/ilal/{hadithId}` and `POST /api/ilal/explain`.
  - `GET /api/takhreej` now includes `ilalReport`.
  - Isnad nodes now carry `isMudallis` and `hasMukhtalit`.
- **ETL.** The new `seed-ilal` mode imports Itqan teacher/student relations and applies the curated `Seeds/mudallisin.json` and `Seeds/mukhtalitun.json`.
- **Fix.** Added a stub `GawamiImporterService`, so the API builds again after the Gawami commit.
- **Fix: compiler IDs.** `ItqanDatasetParser` and `ChainReprocessingService` disagreed on the Itqan IDs of the compilers, and several were wrong in both (for example, 57802 is a Companion, not al-Nasa'i). Both now use one table in `ItqanDatasetParser.BookMetadata`, checked against the Itqan rijal profiles: Bukhari 336, Muslim 618, Abu Dawud 74, al-Tirmidhi 297, al-Nasa'i 134, Ibn Majah 514, Ahmad 353, Malik 664, al-Darimi 168, Ibn Abi Shaybah 748. Re-run `reprocess-chains` to rebuild the chains with the correct compilers.

## Frontend
- New `features/ilal` module with:
  - `IlalPanel`: findings grouped by type, with severity colors.
  - `MatnDiffView`: aligned matn comparison.
  - `IlalAiExplanation` and `IlalLauncher`.
  - A Zustand store that shares the report and the selected finding with the canvas.
- **Takhreej page.** The sidebar now has an "العلل" tab. Selecting a finding highlights its narrators on the tree.
- **Tree page.** A "فحص العلل" button gathers the hadith's turuq and analyzes them.
- **Canvas**
  - The مدلس / اختلط badges are now populated.
  - Tadlis, unproven-meeting and ikhtilat links get distinct edge styles, and the legend lists them.

## Tests
- Added 33 test cases for the rules, the aligner, the Taqwiyah integration, DB loading and `IlalController`. All 44 tests pass.


---

# Changelog: 31-Book Corpus Expansion & Shamela 4 Local Lucene Ingestion

**Date:** October 2, 2026
**Summary:** Expanded the Smart Hadith Tree corpus from 12 Sunni collections to **31 complete canonical Sunni collections** (`233,224` Hadiths and `1,078,668` Isnad Transmissions) by building a high-speed local Shamela 4 Lucene 10.4.0 + SQLite extractor, enhancing `ItqanDatasetParser` and `ContextualDisambiguator`, creating a full verified SQL Server backup, and redesigning the Frontend Home page and book theme registry.

## 1. Shamela 4 Local Lucene + SQLite Extraction Pipeline (`scripts/shamela4-extractor/`)
- **Architectural Discovery**: Determined that Shamela 4 separates structural metadata (`database/book/<id%1000>/<id>.db` containing `page` and `title` tables) from the Arabic text (`database/store/page` and `database/store/title` stored in Apache Lucene 10.4.0 indices).
- **`ShamelaLuceneDumper.java`**: Built a reflection-based Java bulk extractor that invokes `ws.shamela.LuceneBulk.queryRows` on Shamela 4's bundled OpenJDK 21 runtime, dumping all `183,659` pages and `32,380` chapter titles across the 19 missing books in **34.6 seconds**.
- **`build_itqan_books.py`**: Joined the SQLite `page.number` and `title` hierarchy with the Lucene text dumps, stripped HTML markup and leading numbers, concatenated multi-page continuations, and generated standardized `index.json` + numbered chapter `.json` files inside `data/itqan/sunni/<slug>/` for all 19 books (`128,661` complete Hadiths).

## 2. ETL & Contextual Disambiguation Upgrades (`src/SmartHadithTree.Etl/`)
- **`ItqanDatasetParser.BookMetadata`**: Expanded to map all **31 canonical Sunni collections** to their authentic Arabic book titles, compiler names, and verified Itqan Rijal profile IDs (`Abd al-Razzaq: 44`, `al-Tayalisi: 171`, `al-Shafi'i: 2734`, `al-Humaydi: 82`, `Sa'id ibn Mansur: 1959`, `Ishaq ibn Rahawayh: 695`, `al-Bazzar: 196`, `Abu Ya'la: 462`, `Ibn Khuzaymah: 278`, `Abu Awanah: 1123`, `Ibn Hibban: 706`, `al-Tabarani: 202`, `al-Daraqutni: 460`, `al-Hakim: 10`, `al-Bayhaqi: 34`).
- **`ContextualDisambiguator.cs`**: Added regional and era-specific contextual overrides for ambiguous narrator names (e.g., resolving `سفيان` to `سفيان الثوري (434)` when narrated by `عبد الرزاق (44)` or `وكيع (112)`, vs. `سفيان بن عيينة (192)` when narrated by `الشافعي (2734)` or `الحميدي (82)`; resolving `حماد` to `حماد بن سلمة (138)` for `الطيالسي (171)` and `عفان (279)` vs. `حماد بن زيد (128)` for `سليمان بن حرب (161)`).
- **Bulk Ingestion**: Ingested **128,661 new Hadiths** and **655,980 new Isnad Transmissions** (`784,641` total records) in `146.3s`. Cleaned up temporary test records so the database holds exactly **31 collections**, **233,224 Hadiths**, and **1,078,668 Transmissions**.
- **Unit Tests (`src/SmartHadithTree.Tests/Etl/`)**: Added unit tests in `ContextualDisambiguatorTests.cs` and `ShamelaSqliteParserTests.cs` (all passing).

## 3. Database Backup (`backups/`)
- Created and verified (`RESTORE VERIFYONLY`) a full SQL Server backup at `backups/SmartHadithTree_31Books_Full.bak` (`1,390.14 MB`), since renamed to `SmartHadithTree_v1_2026-10-02.bak` (see `backups/README.md`).
- Added `backups/` and `*.bak` to `.gitignore`.

## 4. Frontend Home Page & Book Theme Registry (`frontend/`)
- **`frontend/src/lib/bookTheme.ts`**: Registered all 31 canonical collections with their traditional Hadith scholarly abbreviations (`خ`, `م`, `عب`, `ش`, `طي`, `شاف`, `حميد`, `سع`, `راه`, `بز`, `كب`, `يع`, `خز`, `عو`, `حب`, `طب`, `طس`, `طص`, `قط`, `كم`, `هق`, `شعب`, etc.) and distinct color badges.
- **`frontend/src/app/page.tsx`**: Upgraded the Home page with a direct search bar, quick search examples, live corpus statistics (`31` books, `233,224` hadiths, `1,078,668` transmissions, `115,735` narrators), 6 core feature cards, and a categorized 31-book library showcase.


---

# Changelog: Migration from Itqan to Shamela 4 (Phases 0-7)

**Date:** October 5, 2026
**Summary:** The hadith texts, the narrator registry, the isnad chains and the Ilal data now all come from a local Shamela 4 installation. The app runs on `SmartHadithTree_Shamela` (274,597 hadiths, 1,125,644 transmissions, 23,502 narrators, 116,670 critics' quotes). Details, measurements and pitfalls: `docs/shamela_migration.md`; the comparison with the old database: `docs/shamela_phase6_comparison.md`.

- **Registry and resolver (Python, `scripts/shamela4-extractor/rijal_pilot/`)**: narrators from Tahdhib al-Kamal, Taqrib and the compilers' shaykh books; names matched and isnads resolved jointly; 97.0% precision on a 500-name review.
- **Texts**: 31 books rebuilt from Shamela with the edition's own numbering, records cut at in-text hadith numbers (this fixes the shifted records of the old data), isnad / matn parts, footnotes.
- **Ilal**: mudallisin from Ibn Hajar's tiers (158) and mukhtalitun with the books' severity and hearings (131); `IkhtilatRule` weighs severity; grades come from Ibn Hajar's ranks.
- **Phase 6**: on 4,650 paired hadiths the Ilal engine finds tadlis in 793 (275 before) and ikhtilat in 341 (0 before); 74% position agreement on 220,603 paired chains, with the Shamela side right in 9 of 20 sampled disagreements.
- **Phase 7**: removed `ItqanDatasetParser`, `ContextualDisambiguator`, `ShamelaSqliteParser`, `IlalSeedService`, `ChainReprocessingService`, the old seeds, their tests and `data/itqan/rijal`. The Api and Etl connection strings point at `SmartHadithTree_Shamela`. Backups: v3 (the old database, before the switch) and v4 (the Shamela database).

## Resolver: «الثقات» لابن قطلوبغا as a third fallback (not yet loaded)
- `parse_thiqat.py` (Shamela 96165) writes `extra_thiqat.json` (10,117 entries, fallback). `link_tahdhib.py` keeps a Thiqat entry only when Lisan and Siyar have no narrator with the same first three names (the first try turned 929 names they had decided into ties).
- Measured branch by branch on 306,874 branches against the Lisan+Siyar export: 4,145 names gained, 1,013 lost, 388 swapped (mostly fallback to fallback), gaps closed 1,584 / opened 981. A thin net gain (gaps 99,344 -> 98,892); the swaps and the opened gaps are not hand-read yet.

## Resolver: a late narrator listed twice with his nasab cut short is one narrator
- `merge_same_person` (link_tahdhib.py) has a pair pass: two-unit names («أحمد بن سلمة») that begin exactly two entries of the whole registry, neither Tahdhib's, are merged unless a nisba, kunya or death date differs or a header only points elsewhere («هو فلان», «في ترجمة», «شيخ», «والد»). Sources may be shared here (the late books list one man under several headings). `SHOW_PAIRS=1` prints the pairs.
- Tried and not kept: «أبيه/جده» for a son with a long nasab but several candidates (+178 names, 109 unverified swaps, gaps −58); the pair pass for names of three units or more (it made «محمد بن جعفر» = غندر a tie with two late narrators nicknamed غندر: 569 names lost).
- Measured branch by branch on 306,889 branches against the Thiqat export: 1,960 names gained, 37 lost, gaps closed 1,691 / opened 94 (98,899 -> 97,310). The ~800 id changes are mostly the root of a merged pair (tuhfa:445 -> rijal_hakim:265); a hand read of 45 pairs found about 4 doubtful, all pointer headers, now excluded.
- The pair pass also requires that the generations both headers name are the same words (`_generations`): «محمد بن هشام بن أبي حرة» is not «محمد بن هشام بن أبي الدميك». Cost: gained 1,960 -> 1,893, lost 37 -> 36, gaps 98,899 -> 97,371. A spelling difference («المساور» / «مساور») now blocks a merge too.

## Database `SmartHadithTree_ShamelaV6` built (2026-10-07)
- Fresh database from all the resolver work above: 44,282 narrators (was 23,502), 1,262,178 transmissions (was 1,217,978), 165 mudallisin. Backup v6 (`backups/`, verified). The Api and Etl settings still point at V5 until the user checks V6.
- Isnad gaps over 295,952 comparable branches: 108,066 -> 94,032 (-13%); 27,578 names gained, 694 lost, 3,996 narrators changed. Nine Ilal pages: findings 127 -> 121, matn Qadihah 11 -> 10, grades unchanged.
- The Api and Etl `appsettings.json` now point at `SmartHadithTree_ShamelaV6`; AGENTS.md, CLAUDE.md, README.md and docs/backlog.md say so. The deploy docs still describe the v5 backup that was deployed.

## How the V6 rebuild was run (for the next one)
From `data/shamela_rijal`, after the dump step (the three fallback files are generated and git-ignored; `pipeline.py` does not make them and picks up every `extra_*.json`):
```bash
python ../../scripts/shamela4-extractor/rijal_pilot/make_extra_lisan.py          # lisan.json -> extra_lisan.json
python ../../scripts/shamela4-extractor/rijal_pilot/parse_siyar.py               # dump/10906 -> extra_siyar.json
PYTHONPATH=../../scripts/shamela4-extractor/rijal_pilot python ../../scripts/shamela4-extractor/rijal_pilot/parse_thiqat.py   # dump/96165 -> extra_thiqat.json
rm -f cache/*.pkl
python ../../scripts/shamela4-extractor/rijal_pilot/pipeline.py dump .           # registry.json, ilal.json
python ../../scripts/shamela4-extractor/rijal_pilot/export_chains_shamela.py --books all --workers 4
```
Then a fresh database: `ConnectionStrings__DefaultConnection=...Database=SmartHadithTree_ShamelaVn...`, `dotnet ef database update`, the Etl, `seed-ilal-shamela`, a numbered backup, `eval_ilal_pages.py --compare`. The books 10906 (Siyar) and 96165 (Thiqat) must be dumped first with `ShamelaLuceneDumper`.
Open ideas, none done: a reviewed alias table for lagabs (e.g. «علي بن عمر الحافظ» = al-Daraqutni, «محمد بن عبد الله الحافظ» = al-Hakim); bare names («سفيان», «يحيى», «محمد») need context evidence, not a table; Tarikh al-Islam and Tarikh Dimashq are not downloaded.
