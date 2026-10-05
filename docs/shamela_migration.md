# Shamela 4 Migration (Replacing Itqan) — Status & Handoff

> Read this first when resuming the migration in a new session.
> Last updated: **2026-10-04** · Branch: **`feature/shamela-rijal`** (not merged into `master`).

## 1. Goal and decisions

The project currently depends on the **Itqan** dataset for (a) hadith texts of the 12 primary books and (b) the whole narrator registry: 115,735 narrators, their Jarh wa Ta'deel and the IDs used to resolve every isnad (`data/itqan/rijal/`, `ItqanDatasetParser`, `ContextualDisambiguator`, `Narrator.ItqanId`/`ItqanGrade`).

**User decision:** stop depending on Itqan and rely **entirely on the local Shamela 4 installation** (`D:\Islamic\shamela4`). The user was shown that a hybrid (keep Itqan for isnad resolution, enrich narrators from Shamela) would be cheaper and recommended it, and chose to **continue the full migration** ("اكمل").

Ground rules agreed so far:
- Nothing in the app or the live database changes until a new database built from Shamela is shown to be at least as good as the current one (Phase 6).
- The v2 backup is the baseline (`backups/SmartHadithTree_v2_2026-10-03.bak`, see `backups/README.md`).
- Work happens on `feature/shamela-rijal`; the user merges to `master` on request.

## 2. Why Shamela, and the hard part

Shamela gives **richer narrator data** than Itqan:

| | Itqan | Shamela (so far) |
|---|---|---|
| Teacher / student lists | ~21% of narrators | 85% / 88% of Tahdhib al-Kamal narrators |
| Jarh wa Ta'deel | one summary verdict per source book | the critics' own words, attributed (26,661 quotes from Tahdhib) |
| Overall grade | coarse (`reliable`, `weak`, ...) | Ibn Hajr's 12 ranks from Taqrib (same T1–T12 scale as `NarratorGradeScale`) |

The hard part is **identity resolution**: Itqan is a ready-made registry with IDs; from Shamela we must build our own registry and then recognise every name in every isnad.

## 3. How Shamela data is read

All data comes from the local Shamela 4 installation, read-only. No network access.

| Location | Contents |
|---|---|
| `database\master.db` (SQLite) | Book catalogue (`book_id`, `book_name`, ...) |
| `database\book\<id%1000, 3 digits>\<id>.db` (SQLite) | Book structure (`page`, `title`). The folder is zero-padded (`book\037\13037.db`). The file exists only for books downloaded in the Shamela app; `master.db`'s `book.major_ondisk` says the same without the path. |
| `database\store\page`, `database\store\title` (Lucene 10.4) | **The Arabic text itself** |

Text is dumped with `scripts/shamela4-extractor/ShamelaLuceneDumper.java` using Shamela's bundled JRE and Lucene jars. Book IDs are passed as the third argument:

```powershell
javac -encoding UTF-8 -d data\shamela_rijal\dumper scripts\shamela4-extractor\ShamelaLuceneDumper.java
& "D:\Islamic\shamela4\app\win\64\jre\2\bin\java.exe" --add-modules=jdk.incubator.vector `
  -cp "D:\Islamic\shamela4\app\lucene\2\*;data\shamela_rijal\dumper" ShamelaLuceneDumper `
  "D:\Islamic\shamela4\database\store" data\shamela_rijal\dump 3722,8609
```

Each book produces `<id>_pages.tsv` and `<id>_titles.tsv`, plus `<id>_foot.tsv` (the editor's footnotes, same format) when the book has any. The page bodies contain literal `\n`, which the parsers turn into real newlines.

## 4. Working data (`data/shamela_rijal/`, git-ignored)

| File | Produced by | Contents |
|---|---|---|
| `dump/<id>_pages.tsv`, `dump/<id>_titles.tsv` | `ShamelaLuceneDumper` | Raw book text |
| `lisan.json` | `parse_lisan.py` | 9,170 Lisan al-Mizan entries + 1,646 cross-references (not merged, see §7) |
| `tahdhib.json` | `parse_tahdhib.py` | 8,471 Tahdhib al-Kamal entries + 120 cross-references |
| `taqrib.json` | `parse_taqrib.py` | 8,828 Taqrib entries + 1,419 "X هو Y" redirects |
| `align.json` | `align_taqrib.py` | Tahdhib ↔ Taqrib alignment (92.8% of Tahdhib) |
| `extra_shaykh_books.json` | `parse_shaykh_books.py` | 15,052 narrators from the compilers' shaykh books, تعجيل المنفعة and تاريخ بغداد, after merging 899 cross-book duplicates (plus Ithaf's name forms, ري الظمآن's kunyas and the compiler entries) |
| `mudallisin.json` | `parse_mudallisin.py` | Ibn Hajar's 152 mudallisin in five tiers, plus the editor's 22-entry appendix |
| `ilal.json` | `link_ilal.py` (pipeline step 7) | Mudallisin and mukhtalitun with registry ids, before/after students with ids and their quotes, and the app's current seeds linked the same way (for comparison) |
| `review/` | sub-agents and reviews | Review samples and verdicts (`mudallisin_review.json`, `mukhtalitun_review.json`, `named_review*.json`), the mukhtalitun extraction's intermediate files, `registry_index.tsv` (id, symbols, header, Taqrib text: a lookup aid for reviewers) |
| `chains/<slug>.json` (31 files, 127 MB) | `export_chains_shamela.py` | **Phase 5.2:** every record's resolved chain, loaded by the C# ETL (format in §5, script table) |
| `current_chains.json`, `current_chains_tabarani.json`, `current_chains_bayhaqi.json`, `current_chains_hibban.json`, `current_chains_daraqutni.json`, `current_chains_khuzaymah_awanah.json`, `current_chains_ahmad_malik.json`, `current_chains_early.json` (the 8 early compilers), `current_chains_<slug>.json` for each of the last 10 books (`muslim`, `abudawud`, ..., `sunan_kubra_nasai`) | `export_chains_shamela.py` | **Phase 5.2 (option B):** resolves **every** record of the 31 books of `data/shamela/` and writes `data/shamela_rijal/chains/<slug>.json`: `{book, compiler (registry id), records, chains: [{id, number, names: [{n, id, how, verbs, ties?}]}]}`. It runs the bench's own code (the part of `compare_current.py` before its sampling loop), so a chain is the one the bench measures: the record's first isnad, the first 8 names, after the compiler's own name; records of kind `text` have no chain. In the 7 Shamela-linked books an undecided name whose aligned Shamela narrator maps to one of its tied candidates (exact S1 map only) takes it (`how: shamela_link`; `--no-link` turns this off). `--books all\|b1,b2`, `--limit N`, `--workers 4`; one process per book, the first alone (it rebuilds the cache). |
| `export_chains.ps1` | The **current system's** chains, for comparison |

`link_tahdhib.py` automatically loads `taqrib.json` and every `extra_*.json` that sits next to the `tahdhib.json` it is given.

**Shamela books dumped so far:**

| ID | Book | Used for |
|---|---|---|
| 3722 | تهذيب الكمال | Registry core |
| 8609 | تقريب التهذيب | Verdicts, ranks, aliases |
| 1692 | ميزان الاعتدال | Itqan-ID check only |
| 14463 | الروض الباسم في تراجم شيوخ الحاكم | al-Hakim's shaykhs |
| 29742 | رجال الحاكم في المستدرك | al-Hakim's narrators |
| 29745 | إرشاد القاصي والداني إلى تراجم شيوخ الطبراني | al-Tabarani's shaykhs |
| 1208 | تحفة الغريب بتراجم رجال معجمي الطبراني | Narrators of the Awsat / Saghir |
| 123667 | السلسبيل النقي في تراجم شيوخ البيهقي | al-Bayhaqi's shaykhs |
| 123666 | إتحاف المرتقي بتراجم شيوخ البيهقي | al-Bayhaqi's shaykhs, plus every form of each name as it appears in his books |
| 1498 | ري الظمآن بتراجم شيوخ ابن حبان | Ibn Hibban's 489 shaykhs, plus a table of the kunyas he uses for them |
| 7852 | الدليل المغني لشيوخ الإمام أبي الحسن الدارقطني | al-Daraqutni's 543 shaykhs, with the author's verdicts |
| 151171 | المسالك القويمة بتراجم رجال ابن خزيمة | Three parts: Ibn Khuzaymah's 370 shaykhs, his 204 students, and 198 of his narrators who are not in Tahdhib |
| 1893 | تعجيل المنفعة بزوائد رجال الأئمة الأربعة | Narrators of Ahmad, Malik, al-Shafi'i and Abu Hanifa who are not in Tahdhib (1,113 kept) |
| 241 | فوائد المستخرجات من خلال مسند أبي عوانة | Checked only: a study of the Mustakhraj's benefits, no biographies. Abu Awanah has no dedicated rijal book in Shamela. |
| 736 | تاريخ بغداد | 7,800 biographies: ~4,900 later narrators added, ~1,100 merged into narrators already in the shaykh books (`khatib` layout) |
| 10906, 96165 | السير، الثقات ممن لم يقع في الستة | Gap measurement only (not parsed; see §7) |
| 36357 | لسان الميزان | Parsed (`parse_lisan.py`), not merged into the resolver (§7) |
| 1186 | طبقات المدلسين (تعريف أهل التقديس) لابن حجر | Mudallisin and their tiers (Phase 3) |
| 309 | الكواكب النيرات لابن الكيال | Mukhtalitun, with heard-before / after students (Phase 3) |
| 25846 | المختلطين للعلائي | Mukhtalitun (Phase 3); 82 more narrators in its marginal additions |
| 130, 1187 | الاغتباط، التبيين لأسماء المدلسين (سبط ابن العجمي) | Cross-checks only (Phase 3) |

## 5. Scripts (`scripts/shamela4-extractor/rijal_pilot/`)

| Script | Role |
|---|---|
| `parse_tahdhib.py` | Splits Tahdhib into entries: book symbols, shuyukh/talamidh, attributed quotes, cross-references. Copes with typo'd, repeated or missing entry numbers (longest increasing subsequence + recovery; a repeated "٣٤٩١" had dropped Abu Musa al-Ash'ari, Ibn Mas'ud and three other Companions) and heads without a dash ("١٠٢٥: الحارث الأعور"). Lists: women's «روت عن» / «روى عنها»; a list goes on over a line break unless the line ends with a full stop or the next line is a remark; items are also split at a Latin comma or after a symbols bracket. |
| `parse_taqrib.py` | Ibn Hajr's verdict (full phrase), tabaqa, death, symbols, redirects. |
| `align_taqrib.py` | Unique-name anchors, then an in-order fill between anchors. |
| `build_registry.py` | Ibn Hajar's ranks T1–T12 from a Taqrib verdict (`rank()`, imported by `pipeline.py`); run alone it merges Tahdhib + Taqrib only. |
| `pipeline.py` | **One command for the whole registry** (first draft by a sub-agent, reviewed): runs the five parsers, links the lists, writes `registry.json` (stable ids `tk<num>` / `<source>:<num>`, name, header, sources, verdict, rank, tabaqa, death, kunyas, aliases, strictly resolved shuyukh/talamidh ids plus the raw lists, quotes) and `registry_stats.json`. `--from N` restarts at step N. |
| `parse_shaykh_books.py` | **Generic parser for compilers' shaykh / rijal books**. Configured by `BOOKS` (layouts: `bracket`, `paren`, `star`, `isnad`, `dash`, `runs`, `tajil`, `khatib`) and `COMPILERS`. Merges duplicates across books (see §6, تاريخ بغداد; `SHOW_MERGES=1` prints a sample of merged groups for review) and adds one compiler entry per compiler, whose shuyukh are that compiler's shaykh-book narrators. |
| `link_tahdhib.py` | Name → entry matching (the core). Reports list-linking statistics when run directly. |
| `compiler_items.py` | **Compilers named by a bare word in student lists** («النسائي», «ابن ماجه», «الجماعة سوى البخاري»), mapped to the compilers' own entries and added to the strict `talamidh` by `pipeline.py` (`link_compilers`). `python compiler_items.py` runs its self-test. See §9. |
| `tahwil.py` | **Isnads with several chains (C1).** `chain_branches(text)` splits a record's isnad into branches; `compare_current.our_branches()` resolves each one and joins heads to tails (`join_heads`). `CHAIN_BRANCHES=0` keeps the old single flattened chain. See §9. |
| `chain_resolver.py` | **Joint isnad resolution** (Viterbi over candidate sets, scored by mutual teacher/student listing). Exec'd by `compare_current.py`. |
| `gap_test.py` | Isnad segmentation and clean-up, plus a per-depth coverage report. Its helpers are reused by the other scripts. |
| `gap_books.py` | Which rijal books would cover the missing narrators. |
| `../build_shamela_books.py` | **Phase 4 builder**: `data/shamela/dump/` → `data/shamela/<slug>/` in the Phase 4 format, records cut at the edition's in-text hadith numbers. `python scripts/shamela4-extractor/build_shamela_books.py [out] [slug ...]`. |
| `../split_parts.py` | Phase 4: fills each record's `parts` (isnad / matn / remark / note / text) in place; `--eval [--part held] [--mode blind]` measures it on Shamela's matn groups. |
| `decode_s1.py` | Decodes Shamela's narrator encyclopedia (`service\S1.db`, a per-byte substitution) to `data/shamela/narrators.json`. |
| `links/` | Precision against Shamela's own narrator links: `map_s1.py` (S1 id → registry id, independent evidence), `run_links.py` (resolve 2,000 records of each linked book), `eval_links.py` (align and compare; writes `data/shamela_rijal/review/links/links_eval.json`). |
| `map_records.py` | Pairs Phase 4 records with the current hadiths (by text, and by number where reliable) for `bench.py --texts shamela`. |
| `check_record_boundaries.py` | Classifies every record in `data/itqan/sunni` as clean, prefix tail, or **shifted** (the isnad belongs to the next hadith). See §6.1. |
| `compare_current.py` | **Main benchmark**: our resolution versus the current DB chains on the same hadiths, plus the agreement rate. |
| `bench.py` | **Runs `compare_current.py` on 4 or all 31 books** (in parallel), keeps each run in `data/shamela_rijal/results/<tag>/` (each name with its registry `id`, the `verbs` between it and the next name, and the tied candidates `ties` when undecided), and prints one line per book with the change since the previous run, the names gained and lost, and a sample of new disagreements to review. |
| `parse_mudallisin.py` | Ibn Hajar's طبقات المدلسين: number, tier, symbols, name, kind (taswiya / shuyukh), also-mukhtalit; entries are found anywhere in a paragraph ("... يدلس (٦٩) ع حبيب"). First draft by a sub-agent. |
| `link_ilal.py` | Links the Ilal narrators to registry ids (§6.4) and the students before / after the ikhtilat; also links the app's current seeds. Reads `ilal_data/mukhtalitun.json` and `ilal_overrides.json`. |
| `ilal_data/mukhtalitun.json` | **Curated, git-tracked.** The mukhtalitun of 309 and 25846, read entry by entry by a sub-agent and merged per narrator, with every claim's book, critic and verbatim quote; `ikhtilat_conflicts.json` beside it lists where the books disagree. Not regenerated by a script. |
| `ilal_overrides.json` | **Curated, git-tracked.** Links decided by hand after the reviews (mudallisin by number, mukhtalitun by name, students by «mukhtalit\|student»). |
| `ilal_bench.py` | What the Ilal data finds on a saved bench run, mirroring `TadlisRule` / `IkhtilatRule`, against the app's seeds; ikhtilat by severity and hearing table; ties that may hide a finding. |
| `export_chains.ps1` | Exports current chains: `-Books 'المعجم'` (substrings of `Hadiths.BookName`; use `'بيهقي'`, not `'البيهقي'`, since the DB name is «للبيهقي»), `-Out <file>`. |

### Name matching rules in `link_tahdhib.py` (learned the hard way)

- **Tokens:** harakat removed, `أإآ→ا`, `ى→ي`, `ة→ه`, `أبي→أبو` (not before «بن»: «أبي بن كعب» is Ubayy), `زكرياء→زكريا`, the article dropped, and **`عبد X` and `عبيد الله` kept as one token**. As two words, "عبيد الله" cut al-Zuhri's nasab short, so "ابن شهاب" went to عاصم بن كليب بن شهاب.
- **Anchoring:** a name must start with the entry's own ism, or with its own kunya followed by the ism. Otherwise relatives match ("أخو محمد بن سيرين").
  - Kunya-only entries ("أبو زيد. عن: أبي هريرة") must match on their own kunya. Before this, any word in their header counted, which gave "أبي هريرة" 13 candidates.
  - "المعروف بأبي الزناد" counts as a kunya (عبد الله بن ذكوان), and so does a kunya right after the ism («ذكوان أبو صالح السمان»), but not in "ابن أبي X".
- **Nasab order:** the chain must agree in order, so "محمد بن علي" does not match "محمد بن عمر بن علي". Only words linked by `بن` count as nasab; a trailing nisba is not a grandfather.
  - The aligned Taqrib entry's nasab is accepted as a second nasab (1,277 entries). This corrects typos in Shamela's Tahdhib text, e.g. "عبيد الله بن عتبة" printed for عبيد الله بن عبد الله بن عتبة.
  - The words of the aligned Taqrib name also count as the narrator's own words (laqab, nisbas). Tahdhib may give them only in a later sentence: «الحميدي» comes after "وقيل:".
- **Kunya forms:**
  - "أبو بكر بن إسحاق": kunya, then father or ancestor. "أبو سلمة بن عبد الرحمن" is matched on the father (or the kunya-headed entry's own words), not on narrators whose ism is عبد الرحمن.
  - "أبو زكريا العنبري": kunya, then nisba.
  - "أبو بكر محمد بن أحمد": kunya, then ism and nasab.
- **Shuhra:**
  - Taqrib redirects are aliases, used only when the plain match finds nobody.
  - "ابن X" is checked first: X must be a father or ancestor. When a son of X is at least as cited as the grandsons, only the sons count («ابن عمر» is not Salim b. Abdullah b. Umar); «ابن جريج» stays the grandson.
  - A bare laqab or nisba ("الأعمش", "الزهري") is matched when it starts with `ال`.
  - A laqab without `ال` ("بندار") is matched only when it is nobody's ism, and only when it is the last word of the entry's kunya phrase ("أبو بكر البصري بندار") or follows "الملقب". Matching any header word picked up verbs ("خرج", "بعث").
- **Names by grandfather or "ابن X"** (only when nobody has the given father): the words after the ism come in order in the narrator's own name part, and a word from the nasab is at most the grandfather ("عبد الله بن أحمد بن حنبل", "علي بن المديني", "عثمان بن أبي شيبة"). Entries headed by a kunya («أم عثمان») are excluded.
- **Unnamed narrators** («رجل», «امرأة», «شيخ») match nobody.
- **Book symbols:** "(خ م)" must be covered by the candidate's own symbols.
- **Aliases from shaykh books:**
  - Exact: a form of 3+ words ("أبو القاسم الفقيه") matches only that narrator, before any other rule. A 2-word form is loose: as exact, Ithaf's "عبد الله بن يوسف" (ابن بامويه) replaced al-Tinnisi in Bukhari's and al-Bazzar's isnads.
  - Loose: a bare kunya ("أبو إسحاق", "أبو خليفة") only adds a candidate; the chain context decides. As exact aliases they turned "أبي إسحاق" from Shu'ba into al-Bayhaqi's shaykh يحيى بن إبراهيم.
- **Fame tie-break** (`fame_pick`): only when one candidate has three times the students of the next.
  - In the joint resolver it applies only when no candidate has any edge **and** the isnad gives kunya + ism + father ("أبو العباس محمد بن يعقوب" → al-Asamm, not al-Ahwazi).
  - On shorter forms it was wrong too often: "ابن أبي مليكة" → يعقوب بن زيد, "عبدان" → al-Marwazi, "ابن صاعد" → خلف بن خليفة.
  - It also breaks a **tie between candidates equally linked to their neighbours** (`chain_fame`): Abu Nadra lists both Abu Sa'id al-Khudri (126 students) and Samura b. Jundub (26), both «أبو سعيد». Not for the compiler's own shaykh given by kunya: al-Hakim's «أبو بكر بن إسحاق» is Ahmad b. Ishaq, not Ibn Khuzaymah.
- **Edition typos tolerated:** "بن بن", "ثقه", numbers without a dash ("٩٣٣ ق:"), missing numbers.

### Isnad segmentation (`gap_test.py`)

The segmentation:
- Strips the previous hadith's verdict and number ("هذا حديث صحيح ... ٣٧٦٣ -"), honorifics (الشيخ، القاضي...), place suffixes (ببغداد...), "إملاء" and footnote marks.
- Resolves "X هو Y" to Y.
- Keeps only the first of two shaykhs joined by "و".
- Drops segments that are not shaped like a name; they are reported separately as noise.

## 6. Results so far (samples, same hadiths as the current DB)

Coverage means the share of narrator names in the first 8 links of each chain that were identified. Agreement means the share of our identified narrators that the current system also identified (same ism + father; a Companion known by his kunya also agrees when his Tahdhib header gives the current system's name, «أبو هريرة» = «عبد الرحمن بن صخر»; the Taqrib form of a typo'd Tahdhib name counts too, «عبيد الله بن عتبة» = «عبيد الله بن عبد الله بن عتبة»; and a current-system narrator whose Itqan id is mapped to our entry agrees whatever name it carries, since Itqan names some narrators wrongly but means the right person: «نافع بن همام» is Nafi' mawla Ibn Umar, «عكرمة بن منصور» Ikrima mawla Ibn Abbas, «محمد بن أبي عمرة» Ibn Sirin — 77 ids in `review/itqan_id_map.json` so far). A name past the end of the current chain ("… ← عروة" with no «عائشة») has nothing to compare with: it is counted in the last column, not as a disagreement. Before the clean-up step below, those names counted as disagreements, so older agreement figures in commit messages are lower.

| Book (sample) | Ours: first greedy | Ours: now | Current system | Agreement | Ours past the current chain |
|---|---|---|---|---|---|
| al-Bukhari (500) | 54% | **87%** | 81% | 84% | 233 |
| al-Mustadrak (1,000) | 59% | **81%** | 92% | 61% † | 984 |
| al-Mu'jam al-Kabir (500) | 70%* | **87%** | 81% | 89% | 393 |
| al-Mu'jam al-Awsat (500) | 65%* | **84%** | 87% | 89% | 124 |
| al-Mu'jam al-Saghir (500) | 65%* | **87%** | 84% | 86% | 126 |
| al-Sunan al-Kubra, al-Bayhaqi (500) | 60%* | **82%** | ✱ † | 45% † | 555 |
| Shu'ab al-Iman (500) | 60%* | **74%** | 95% † | 79% † | 278 |
| Sahih Ibn Hibban (500) | 74%* | **90%** | 85% | 90% | 194 |
| Musnad Ahmad (500) | 75%* | **87%** | 85% | 87% | 133 |
| al-Muwatta (500) | 53%* | **70%** ‡ | 78% ‡ | 87% | 143 |
| Sunan al-Daraqutni (500) | 68%* | **85%** | 87% | 83% | 244 |
| Sahih Ibn Khuzaymah (500) | 68%* | **85%** | 80% | 86% | 329 |
| Mustakhraj Abi Awanah (500) | 67%* | **83%** | 68% | 84% | 538 |
| Musannaf Abd al-Razzaq (500) | — | **77%** | 76% | 91% | 121 |
| Musnad al-Tayalisi (500) | — | **86%** | ✱ | 80% | 22 |
| Musnad al-Shafi'i (500) | — | **85%** | 80% | 92% | 155 |
| Musnad al-Humaydi (500) | 44% | **84%** | 88% | 91% | 84 |
| Sunan Sa'id b. Mansur (500) | — | **80%** | ✱ | 80% | 44 |
| Musnad Ishaq (500) | — | **83%** | 82% | 88% | 217 |
| Musnad al-Bazzar (500) | — | **87%** | 94% | 81% | 76 |
| Musnad Abi Ya'la (500) | — | **85%** | 87% | 88% | 134 |
| Sahih Muslim (500) | — | **87%** | 69% | 87% § | 511 |
| Sunan Abi Dawud (500) | — | **85%** | 80% | 89% | 233 |
| Jami' al-Tirmidhi (500) | — | **91%** | 90% | 91% | 129 |
| Sunan al-Nasa'i (500) | — | **88%** | 82% | 88% | 222 |
| Sunan Ibn Majah (500) | — | **89%** | 83% | 92% | 240 |
| Sunan al-Darimi (500) | — | **86%** | 86% | 89% | 126 |
| al-Adab al-Mufrad (500) | 58% | **83%** | 82% | 85% | 102 |
| al-Shama'il (396) | 72% | **87%** | 86% | 94% | 97 |
| Musannaf Ibn Abi Shayba (500) | — | **78%** | 89% | 81% | 60 |
| al-Sunan al-Kubra, al-Nasa'i (500) | — | **89%** | 87% | 87% | 141 |

Measured on run `final3`, after Phase 3's kunya tie-break (§6.4).

\* Joint resolver already applied, before that compiler's shaykh books were added.
✱ Over 100%: the current system counts more links than our name segments (it also links the book's transmitters), so its share is not comparable.
‡ Measured after the chain starts behind the compiler's own name (see §6.2). The earlier 53% counted the transmitter «يحيى» and «مالك» himself, mostly resolved wrongly. The drop from 62% to 57% with تاريخ بغداد is 65 wrong links removed: "عن مالك أنه بلغه" had been resolved to the Companion «مالك بن صعصعة»; agreement rose from 63% to 69%.
§ The current system's coverage is low because 18% of its Muslim chains are cut short (§6.3).
† Not comparable: many records have shifted boundaries (§6.1), so the two systems read different isnads from the same record. A manual review of ~50 resolved al-Bayhaqi narrators found 1 error ("أبي إسحاق" from Zuhayr, which should be al-Sabi'i).

Since the shifted-record fix in `gap_test.py`, al-Mustadrak's agreement is low (60%) for the same reason (†).

The table is measured after Ibn Hibban's book (`ري الظمآن`) was added. That step:
- Raised Ibn Hibban from 74% to 79%.
- Raised agreement in every comparable book. Bukhari has 1,417 agreeing names against 1,405, because bare kunyas are now loose aliases.
- Cost about 0.5–1 point in al-Mustadrak and al-Sunan al-Kubra. These are real namesakes from the same generation (Ibn Hibban's «محمد بن عبد السلام» against al-Hakim's), which the resolver now leaves undecided rather than guessing.

Adding al-Daraqutni's book (الدليل المغني) raised Sunan al-Daraqutni from 68% to 73%, and al-Sunan al-Kubra of al-Bayhaqi from 69% to 71%, with no losses elsewhere. A review of 12 al-Daraqutni chains (~50 names) found 2 errors:
- «عكرمة» after an unresolved «أيوب» → عكرمة بن خالد, instead of the mawla of Ibn Abbas.
- «شريك عن أبي حمزة» (a saying of al-Nakha'i) → Anas. The chain is consistent but wrong; Abu Hamza there is Maymun al-A'war.

Its misses are mostly Baghdadi narrators (سعدان بن نصر، سعيد بن بحر القراطيسي) who belong to تاريخ بغداد.

The Ibn Khuzaymah step (`المسالك القويمة`) added his shaykh and student lists, but his shaykhs are mostly in Tahdhib already. The larger gain came from general fixes found while reviewing his gaps, which raised every book: Bukhari from 73% to 76%, Ibn Hibban from 79% to 81% (agreement 84%). The fixes:
- Five Companions recovered in the Tahdhib parse.
- Kunya-only entries anchored on their own kunya.
- "المعروف بأبي X" read as a kunya.
- Laqabs without "ال".
- "عبيد الله" as one name.
- Taqrib's nasab used as a second nasab.

Corrected along the way: «أبو الزناد» (was أبو القاسم بن أبي الزناد), «الأعرج» (was ثابت بن عياض الأحنف), «أبي موسى الأشعري», «ابن مسعود», «بندار». A review of 12 Ibn Khuzaymah chains (~75 names) found 1 error, «ابن شهاب» → عاصم بن كليب, which is now fixed.

Abu Awanah's remaining misses are mostly his own shaykhs (أبو أمية الطرسوسي، الصغاني، ابن الجنيد، محمد بن حيويه), with no dedicated book in Shamela. They are left to تاريخ بغداد / السير.

تعجيل المنفعة adds 1,113 early narrators, but each is rare, so a 500-hadith sample barely moves: Malik +8 names, Ahmad +1, Ibn Hibban −6, Bukhari −2. What made it neutral rather than harmful:
- 263 entries are remarks on isnads of Tahdhib narrators («شعبة بن الحجاج», «عمرو بن شعيب», «محمد بن عبد الرحمن بن أبي ذئب»). They are skipped when their nasab is a prefix of a Tahdhib nasab, or with 3+ names an in-order subsequence of one.
- Entries without lists (notes on names, «عبد الرزاق») are skipped.
- Its lists name narrators briefly ("نافع"). Such 'short' items give a teacher/student link only when they resolve to one narrator; otherwise every Nafi' became a link.

The early compilers (no shaykh books needed) read 72–77% with 78–82% agreement, since their narrators are in Tahdhib. Two fixes came from them:
- Musnad al-Humaydi went from 44% to 75%. "حدثنا الحميدي، ثنا سفيان" opens almost every chain. «الحميدي» was neither recognised as the compiler nor found at all: his laqab is in Tahdhib's second sentence, and is now taken from Taqrib.
- Sa'id b. Mansur's agreement went from 60% to 81% once «سعيد» opening the chain was taken as the compiler.

Al-Bazzar's 71% agreement is mostly the current system's errors: «نافع» → نافع بن همام, «عكرمة» → عكرمة بن منصور, «عبيد الله» → عبيد الله بن معاذ.

The last 10 books (the six books, al-Darimi, al-Adab al-Mufrad, al-Shama'il, Ibn Abi Shayba, al-Nasa'i's al-Kubra) read 72–82%, with 76–87% agreement except Muslim (§6.3). Three fixes came from them:
- Itqan's text of al-Adab al-Mufrad and al-Shama'il puts invisible direction marks (U+200F) around the colon: "قال‏:‏". The verb was not recognised, so whole segments ("بشر بن محمد، قال‏:‏") failed. `chain_segments` now strips them: al-Adab al-Mufrad went from 58% to 77%, al-Shama'il from 72% to 82%.
- "عن أبيه" after a narrator whose father is named by a kunya («سهيل بن أبي صالح») found a kunya-only entry «أبو عبيد». It now looks for the shaykh with that kunya (ذكوان أبو صالح).
- «زكرياء» and «زكريا» are one spelling.

Ibn Abi Shayba has 22 of 500 hadiths with no narrator resolved (the current system: 1). Not reviewed yet.

**تاريخ بغداد (736)** added ~4,900 later narrators, with the shaykh and student lists from al-Khatib's opening paragraph ("سمع X، وY. روى عنه Z"), not from his own isnads. Al-Daraqutni went from 74% to 76%; the other books moved by at most a point. On the way:
- **Namesakes of Tahdhib narrators** («محمد بن الصباح، أبو يعقوب الصوفي» is not al-Dulabi) are kept. An entry is skipped as a Tahdhib narrator only when the nasab matches and, if al-Khatib gives a kunya or nisba, one of those words is in the Tahdhib header too; with ism + father only, a shared kunya is not enough.
- **Cross-book duplicates** («ابن صاعد» had four entries under four heads) made names tie that a single entry resolves. All shaykh-book entries now go through one merge: same nasab as far as both go (one-letter typos allowed), no conflicting kunya, never two entries of the same book, and a shared rare nisba/laqab (a common one such as «الكاتب» needs a second shared word or the kunya), or a shared kunya with four names. Complete linkage: a narrator joins a group only if he fits every member. A review of 25 merged groups under these rules found no clear wrong merge; the first, looser version had merged nine different «إبراهيم بن محمد بن أحمد».
- "المعروف بالشافعي" in a header now gives the laqab «الشافعي» (أبو بكر الشافعي).

**What is left is mostly our matching, not missing books.** In the al-Daraqutni, Kabir, Mustadrak and Bukhari samples, the unresolved names are mainly:
- Famous Tahdhib narrators named by grandfather or by a well-known "ابن X": «عبد الله بن أحمد بن حنبل» (56 times in the Kabir and Mustadrak samples), «عثمان بن أبي شيبة», «سعيد بن أبي مريم», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية», «محمد بن أبي عدي». Name matching wants the father right after the ism.
- Text not cleaned: «أبي هريرة رضى الله عنه» (ى), accusative «أبا هريرة» / «جابرا» / «أنسا», «مسدد قالا», «أبي، ح».
- Companions and famous single names left ambiguous when the neighbour is unresolved («ابن عمر», «ابن عباس», «الزهري»).

**Clean-up and names by grandfather** fixed the first two. Bukhari went from 77% to 83%, Muslim from 74% to 82%, the Kabir from 80% to 84%, al-Daraqutni from 76% to 80%, the Muwatta from 57% to 66%; every book gained. Ours is now above the current system in Bukhari, the Kabir, Muslim and Abu Awanah, and level in Ibn Hibban, Abu Dawud and al-Nasa'i.
- **Clean-up** (`clean_segment`): "رضي/رضى الله عنه" and similar end the name; a comma ends it unless the nasab goes on ("عائشة، زوج النبي", "ابن جريج، أخبرهم"; but "عبد الملك، بن أبي بكر"); a trailing "ح" (tahwil); "قالا:" after a name; accusative «أبا هريرة» → «أبي هريرة», «جابرا» → «جابر» (only when the form without ا is a known ism). Alone this added 33 «أبي هريرة», 17 «عائشة», 15 «ابن عباس» to 500 Bukhari isnads.
- **Unnamed narrators**: «رجل», «امرأة», «شيخ» no longer match entries such as "رجل من آل سهل بن حنيف".
- **Names by grandfather or by "ابن X"** (`by_grandfather`): when nobody has the given father, the words after the ism must come in order in the narrator's own name part, and a name taken from the nasab must be the grandfather at most. This finds «عبد الله بن أحمد بن حنبل», «عثمان بن أبي شيبة», «سعيد بن أبي مريم», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية», «محمد بن أبي عدي», «إسماعيل بن أبي أويس», «محمد بن إشكاب», «حفص بن أبي داود» (حفص بن سليمان). Allowing any ancestor gave «أحمد بن أسد» → Ahmad b. Hanbal and «أيوب بن جابر» → أيوب بن خالد بن صفوان بن أوس بن جابر.
- A manual review of 20 random new resolutions (four books, Companions excluded) found 1 error, from an older path: «ابن منيع» as al-Daraqutni's shaykh → محمد بن سعد بن منيع; it is al-Baghawi, whose entry does not record that name.

**Companions and common names** (the resolver gap of §7). The cause was mostly not ambiguity but **lists missing from the Tahdhib parse**, which left the most frequent narrators without context:
- Women's lists say «روت عن» / «روى عنها»: Aisha, Umm Salama, Hafsa, Asma', Amra had none.
- A line break inside a list ended it: Abu Hurayra's students stopped at 17 (now 329), Shu'ba's at 10, Ibn Uyayna's at 2 (now 188), Qatada's at 8.
- Items joined without "، و" («(بخ) ومسلم», «(س) , ويحيى») were swallowed by the item before: Abu Salih al-Samman had 17 students, Mu'awiya, Jabir and Mu'adh lost dozens.
- 21 entries with a head «١٠٢٥:» (no dash) were missing: al-Harith al-A'war, Jumay' b. Umayr, Abu Umayya al-Tarsusi.

Fixing them added ~5,600 list items. Complete lists also make real namesakes tie, so the fame tie-break now settles ties between candidates equally linked to their neighbours (rules in §5), and four matching bugs it exposed were fixed: «ابن عمر» → his grandsons, «أبو سلمة بن عبد الرحمن» missing Abu Salama b. Awf, «أبي بن كعب» read as «أبو كعب», and «ذكوان أبو صالح» without his kunya. The agreement metric was also corrected: a compound name («عبد الرحمن بن صخر») was cut to three words, and the header compared only to its first 120 characters. Against the old code measured with the same metric:
- Coverage rose 2–8 points in every book (Bukhari 83% → 85%, Muslim 82% → 86%, Ibn Majah 81% → 87%, al-Bazzar 76% → 84%); overall agreement 81.8% → 82.1%.
- 2,576 names newly resolved agree with the current system, 727 differ, 621 are past its chain (186 «عائشة»). 314 earlier disagreements were dropped and 212 corrected (Sufyan b. Uyayna, al-Sabi'i, al-Harith al-A'war); 218 agreeing names were lost or changed.
- Many "disagreements" are the current system's: «أبي صالح» from al-A'mash → باذام (it is Dhakwan al-Samman, whom it also names «ذكوان بن الله», so every Dhakwan counts as a disagreement), «عكرمة» → عكرمة بن منصور, «عبيد الله» from Yahya → عبيد الله بن معاذ, «نافع» → نافع بن همام, Ibn Sirin as «محمد بن أبي عمرة».
- Tie-break resolutions agree 73% (ordinary chain resolutions 81%), largely for the reasons above.

**The compiler's shaykh list for the first link.** It was already used (the edge from the compiler to his shaykh), but al-Hakim was never found as the compiler: the Mustadrak prefix «…بن حمدويه الحاكم» did not match his header «…بن حمدويه بن نعيم», and an empty duplicate entry from السلسبيل shares that prefix. The compiler is now the matching entry with the most lists. The Mustadrak rose from 78% to 79% (94 names gained); «أبو بكر بن إسحاق» is now Ahmad b. Ishaq al-Faqih, not Ibn Khuzaymah. Other findings:
- Tahdhib gives no shaykh list for al-Tirmidhi, al-Nasa'i and Ibn Majah («قد سميناهم في مواضعهم»); their link comes from each shaykh's student list, which works.
- «أبو بكر محمد بن أحمد» in the Mustadrak is a real ambiguity: several of al-Hakim's shaykhs carry it (Ibn Balawayh among them), so it stays undecided. Putting the compiler's shaykhs ahead of the 80-candidate cut changed nothing there, and for a bare «محمد» it made things worse, so it was not kept.

**Precision review (150 names, run `final`).** A fixed sample (`data/shamela_rijal/review/precision_sample.py final 150 2026`: 15 resolved names from each of 10 books) was judged against the isnad, the registry lists and knowledge of the narrators, ignoring the current system. A Sonnet sub-agent did the first pass; every "wrong" / "unsure" and 40 random "right" verdicts were then checked by hand, and all were confirmed. Result: **141 right, 8 wrong, 1 unsure: precision 94%** (95% CI 90–98%).
- By method: `unique` 65/66, `chain` 73/78, `chain_fame` 3/6 (small, but in line with its lower agreement).
- **Where we disagree with the current system (19 names), ours was right every time.** The agreement column above understates our accuracy.
- Names past the end of the current chain are the weak spot: 16 right, 6 wrong. The errors: a kunya stub entry («أبو التياح» instead of يزيد بن حميد); segmentation («إسماعيل - وهو ابن علية -» read as Ibn Ayyash, so «عبد العزيز» went wrong too; «سعيد وأبى سلمة» as one name); later namesakes chosen by the chain («أبى هريرة» → محمد بن أيوب الكلابي, «أبو الوليد الفقيه» → ابن عرق); «محمد» after Hisham → Abu al-Zubayr (Ibn Sirin); Daraqutni's «إسحاق بن إبراهيم» → Ibn Rahawayh (al-Dabari); «أبيه» after a wrong «طلحة».
- Verdicts are in `review/precision_firstpass.json`.

**Segmentation fixes from the review.** The trailer rule that strips a place after a name («ببغداد», «بنيسابور») stripped **any** last word starting with «ب»: «بكر», «بكير», «بشار», «بلال», «بشر», «بردة», «بريدة» (over 7,000 segments in Bukhari and al-Bayhaqi alone). «محمد بن بشار» became «محمد بن», «يحيى بن بكير» «يحيى بن»; the chain often still found them, which hid the bug. It now never strips a word after «بن / بنت / أبي / أبو / أبا / أم». Also: dashes around a note («إسماعيل، - وهو ابن علية -») hid «وهو»; «X وهو ابن Y» / «X يعني ابن Y» now give «X ابن Y», «يعني X» gives X like «هو X», but «وهو من بني …» (a description) ends the name; «X وأبي Y» keeps the first shaykh. Coverage +1 to +4 points in 17 books (Ibn Khuzaymah +4, al-Bayhaqi +3, Ahmad +2); agreement steady or +1. Counted by narrator per isnad: 1,460 gained (986 agree with the current system, 305 differ), 546 lost (317 had disagreed: the wrong guesses for «محمد بن»; 121 agreed, mostly a name pushed past the first-8 limit by a now-complete first name, or a full name spelled differently in the registry, «شبيب بن بشير» / «بشر»). The current system's column moves by −1 because our name-segment count, its denominator, grew.

**Rules of `ContextualDisambiguator`, ported where our evidence confirms them.** A Sonnet sub-agent listed its rules (111 entries, ~62 distinct; every condition is the student; targets are Itqan ids) and mapped the 77 Itqan ids to registry entries (67 exact, 10 probable; all spot-checks right). Measured against our resolutions on the 31-book bench, its unconditional rules agree with ours 95% of the time (15,105 / 15,895). The 28 bare names where we agree 97%+ over 30+ cases are a fallback (`DEFAULT_FOR` in `chain_resolver.py`) when the chain cannot decide: 471 names gained, none lost («ابن عباس» 136, «عائشة» 113, «ابن عمر» 36, «جابر بن عبد الله» 31, «الزهري» 26). Rules our evidence contradicts are not ported: «سفيان» (28% differ), «جابر» (26%), «يحيى بن سعيد» (35%), «عكرمة», «محمد بن كثير», «ابن علية». The conditional rules (student-specific) are what the chain already does from the lists.

**Correction to the earlier disagreement reviews:** Itqan's «نافع بن همام», «عكرمة بن منصور» and «محمد بن أبي عمرة» are naming errors for the right narrator, not identification errors. With the id mapping in the metric, agreement rose 1–4 points in most books and 9 in the Muwatta.

**Precision review, 500 names (run `rules`, after the segmentation, glued-kunya and rules fixes).** Same method on a new sample (`review/p500/`: `precision_sample.py rules 500 2027`, 50 names from each of 10 books): Sonnet first pass, then every "wrong" / "unsure" and 48 random "right" checked by hand — all confirmed. **485 right, 13 wrong, 2 unsure: precision 97.0%** (95% CI 95.5–98.5), up from 94% (90–98) on 150 names before those fixes.
- By method: `unique` 200/204, `chain` 271/280, `chain_fame` 11/13, `default` 3/3. By the current system: where it agrees 367/368, where it differs 60/67, past its chain 58/65.
- The errors, by cause: segmentation (a footnote mark with Latin digits «(1)» cut «ابن أبي أنيس» out, so «الزهري» became his nephew; «هذا» and «بعض» taken as names; «يعنون ابن جعفر» not read); «قال ابن المثنى», a speaker tag in Muslim repeating the previous narrator, read as a new link (2); «ابن علية» → his son Hammad (the Taqrib alias is used only when nothing else matches); «مالك بن يحيى» → مالك بن دينار أبو يحيى (the grandfather rule accepts a kunya word); chain errors («هشام» → Ibn Urwa instead of al-Dastuwa'i, «محمد بن جعفر» → al-Warkani instead of Ghundar, «الزهري» → سعد بن إبراهيم); al-Hakim's «أبو بكر» in a quoted isnad.

A manual review of 12 Ibn Hibban chains (~70 names) found 1 likely error: "أبي جعفر" from يحيى بن أبي كثير → al-Baqir, probably al-Ansari al-Mu'adhdhin.

### 6.1 Data finding: shifted hadith records (affects the live app today)

`check_record_boundaries.py` shows that in some books extracted earlier from Shamela (`build_itqan_books.py`), a record holds a matn followed by the **next** hadith's numbered isnad ("… ١٠٨٣٨ - أخبرنا …"). For those records, the matn and the isnad shown together do not belong to each other.

| Book | Prefix tail (own isnad, previous hadith's end in front) | **Shifted** (isnad of the next hadith) |
|---|---|---|
| السنن الكبرى للبيهقي | 45% | **30%** |
| شعب الإيمان | 24% | **34.5%** |
| المستدرك | 54% | **19%** |
| مسند البزار | 10% | 0.6% |

The other books are clean, or have no numbered markers. Abd al-Razzaq, Ibn Khuzaymah and al-Daraqutni open with other forms; these were not checked further. The current database was built from the same files, so its trees for these hadiths can pair a matn with another hadith's chain. Phase 4 fixes this; it can also be fixed earlier in `build_itqan_books.py` if the user wants.

### 6.2 Data finding: the Muwatta's chains loop through «يحيى بن سعيد» (affects the live app today)

The Muwatta's isnads open with its transmitter: "حدثني يحيى، عن مالك، عن ابن شهاب". The current database reads "يحيى" as يحيى بن سعيد الأنصاري and makes him Malik's shaykh. This produces the loop Malik → يحيى بن سعيد → Malik → ابن شهاب in **576 of the 1,860 Muwatta hadiths**. In 74 more, "نافع" is linked to نافع بن همام.

Our resolver had the same problem, and now `compare_current.py` starts each chain after the compiler's own name. The name, in the first two positions, must include the compiler among its candidates, and be either his most-cited candidate or one of the book's usual openers (first two positions of 20%+ of the sample: «الحميدي», «سعيد»). So "محمد" in a Bukhari isnad is not taken for al-Bukhari. Phase 5 must do the same when chains are rebuilt.

### 6.3 Data finding: Muslim's chains are often cut short (affects the live app today)

In the live database, **1,296 of Muslim's 7,368 hadiths (18%)** have a first chain of one narrator or none. In Bukhari it is 2%, in al-Tirmidhi 1%. Many of these are Muslim's follow-up isnads ("وحدثنا ... بهذا الإسناد"). In 500 sampled hadiths, the current system's coverage was 72%, below ours (74%), and agreement only 71%. The disagreements reviewed were mostly the current system's: a chain of one name, «نافع» → نافع بن همام, «عبيد الله» → عبيد الله بن معاذ, «عمرو» from Ibn Wahb → عمرو بن دينار (it is عمرو بن الحارث). Phase 5 must rebuild these chains.

**Precision (manual review of the disagreements):**
- **Bukhari:** of 20 disagreements, ours was right in ~15, there was 1 clear error of ours, and the rest were the same person spelled differently or undecided. The current system has repeated errors: "نافع" → نافع بن همام, "الليث" → "الليثي", "أبو الوليد" (from Shu'ba) → هشام بن عمار, "أبو معمر" → إسماعيل بن إبراهيم.
- **Mustadrak:** most disagreements happen because the current chain is truncated or mixes two isnads.
- **Mu'jam al-Kabir:** 2 errors of ours in 18, both fixed.

**Summary:** the current system covers more; ours resolves less, but what it resolves is usually right. These are small manual samples, not a full precision measurement.

### 6.4 Ilal data (Phase 3)

**Mudallisin** (طبقات المدلسين, 1186). 152 narrators in five tiers, 33 / 33 / 50 / 12 / 24 as the book itself counts them (not the 33 / 37 / 50 / 12 / 20 often quoted), plus the editor's 22-entry appendix (no tier). **158 of 174 are linked**; the other 16 are later scholars or obscure men not in the registry (Abu Nu'aym al-Asbahani, Ibn Masdi), or Abu Qatada al-Harrani, who has no entry of his own in Shamela's Tahdhib text. A Sonnet sub-agent reviewed all 174 links: 147 right, 7 wrong, 8 unlinked but in the registry, 11 absent, 1 unsure. Every non-right verdict and 25 right ones were checked by hand; the reviewer was wrong once («عبد العزيز بن جريج» is Ibn Jurayj's father, tk3438s, not a typo for Ibn Jurayj). 16 links are in `ilal_overrides.json`.

**Mukhtalitun** (الكواكب النيرات 309, المختلطين للعلائي 25846). A sub-agent read all 249 entries (309: 70 + 51 in the editor's two appendices; 25846: 46 + 82 marginal additions marked (ز)) and merged them into **164 narrators**, 85 in both books. Every quote was machine-checked as a verbatim substring of its entry. Only 30 narrators have named students: 173 claims (105 before, 60 after, 7 conflicting, 1 both); 29 have group rules (76, e.g. «من سمع منه بالبصرة قبل أن يقدم بغداد»); 13 say nobody heard after. In 309, entries 1–70 are Ibn al-Kayyal's own text and both appendices are the editor's (`source: editor`). The books disagree directly on three students (Abu Nu'aym from Ibn Abi Aruba, Hammad b. Salama from Ata b. al-Sa'ib, Isra'il from Abu Ishaq; `ikhtilat_conflicts.json`). Of الاغتباط's 122 names, 23 are in neither book (Companions, Mujahid, Masruq, later Syrians); not merged. **131 of 164 narrators linked** (most of the rest are 6th–8th-century scholars) and **166 of 171 hearings**. Review as above: narrators 126 right / 12 wrong / 3 found / 18 absent / 5 unsure, students 152 / 7 / 10 / 4 / 1; 20 narrators and 18 students overridden, 24 right ones spot-checked by hand. A student must be listed with the mukhtalit, or have a single candidate, or be the clearly most cited one («وكيع بن الجراح» under Ibn Abi Aruba is not in the lists).

**What they find** (`ilal_bench.py` on the 31-book run `final3`, 15,894 isnads; same rules as `TadlisRule` / `IkhtilatRule`):

| | Shamela lists | App seeds (16 mudallisin, 7 mukhtalitun) |
|---|---|---|
| Tadlis: tier 3+ narrating with عن / أن / قال, no explicit formula | **3,243** | 2,326 |
| Ikhtilat: a narrator narrating from a mukhtalit | **4,325** | 950 |

- 917 tadlis findings come from narrators not in the seeds (al-Mughira b. Miqsam, Makhul, Baqiyya, Ibn Ayyash, al-Hasan b. Dhakwan ...).
- **Ikhtilat needs weighting before it reaches the app.** 3,082 of the 4,325 are narrators the books themselves call light or disputed (Ibn Uyayna 437, Hisham b. Urwa 377, Hammad b. Salama 328, Abu Ishaq 318, Abd al-Razzaq 254, al-Maqburi 148); 501 are harmful (Ibn Abi Aruba 138 ...), 742 unstated. 3,796 have no recorded timing for the student (before 278, after 160). Phase 5 must use the severity, the group rules and the timing, not flag every link.

**Ties** (the deferred-ties condition of Phase 2). Undecided names whose tied candidates include a narrator who would give a finding there hid **7.7% of tadlis findings**, past the 1% condition, mostly «أبو معاوية» (al-Darir / Hushaym, a tier-3 mudallis; 64) and «أبو إسحاق». So they were fixed here (`chain_named` in `chain_resolver.py`): a tie between equally linked candidates goes to the one a neighbour names with the isnad's own form, when that neighbour lists every tied candidate (al-A'mash's students include «أبو معاوية الضرير» and «هشيم بن بشير»), and only for a kunya, a bare ism or a nisba. A broader first version (no "lists every candidate" condition, any form) was 86% right on 80 random picks («أبي بشر» → بيان بن بشر, «أبي سلام» → the Kufan al-Aswad, «ابن عون» → عمرو بن عون); the final rule keeps 55 / 55 of those (1 unsure) and **59 / 60 on a fresh sample**. 31 books: **299 names gained, none lost** (96 «أبو معاوية», 60 «عبد الله», 22 «عمرو», 22 «الأوزاعي», 16 «أبي إسحاق»); coverage +1 point in 11 books and +2 in Sa'id b. Mansur. Hidden tadlis went from 7.7% to 4.7%.

What remains hidden by ties is mostly ikhtilat (15% of those findings): bare «سفيان» (al-Thawri / Ibn Uyayna, 159), «عطاء» (Ibn al-Sa'ib, 59), «حماد» (Ibn Zayd / Ibn Salama, 47). They were not on the deferred list, the mukhtalitun involved are ones the books call light or disputed, and Phase 2 found the old rules for «سفيان» wrong 28% of the time; **deferred until after Phase 7 (user decision, 2026-10-04)** (§7).

Other findings:
- The registry holds duplicates of Tahdhib narrators in the compilers' books (رجال الحاكم's «أحمد بن حنبل» = tk96, تاريخ بغداد's «إسحاق الأزرق» = tk395, الباغندي in ري الظمآن and إرشاد القاصي). The Ilal linker prefers the tk entry when the nasab is the same; the cross-book merge of Phase 2 does not cover Tahdhib narrators.
- Ibn Hajar's own tiers put al-A'mash and both Sufyans in tier 2 (accepted 'an'ana) and al-Zuhri, Qatada, Abu Ishaq, Ibn Jurayj, Hushaym and Abu al-Zubayr in tier 3, as the app's seeds did.

## 7. The plan we are following: phases and tasks

Update the checkboxes whenever a task is finished, and record the commit next to it.
**Next task:** merge `feature/shamela-rijal` into `master` when the user asks; then, optionally, the chain gaps and Phase 5b. Phase 6 is done and the app was switched to `SmartHadithTree_Shamela` (see `docs/shamela_phase6_comparison.md`); the chain gaps left by undecided names (§3 of that report) and the missing grades of narrators outside Taqrib are known regressions. Phase 5 is complete with option B: the resolver stays in Python and C# loads its output; the C# port is Phase 5b, later (user decision, 2026-10-04). Phase 4b is deferred (user decision); Phase 4 is complete; Phase 3 is complete; the bare «سفيان» / «حماد» / «عطاء» ties (§6.4) are deferred until after Phase 7 (user decision). Work split used since late Phase 2: Sonnet sub-agents draft parsers, do mechanical extraction, hand-reading and first-pass reviews, writing only to the scratchpad, `data/shamela_rijal/review/` or `rijal_pilot/review_drafts/`, and never touching the resolver, the registry files or the cache; the main session reviews, links, measures with `bench.py` and commits.

**Working rules** (from Phase 3; follow them in every later phase):
1. **Every sub-agent review gets a second check.** The main session checks every item not judged "right", plus a random sample of the "right" ones. In Phase 3 a reviewer once "corrected" a right reading (Ibn Jurayj's father taken for Ibn Jurayj).
2. **Re-measure on a fresh sample after tuning.** A rule adjusted using a review sample is checked on a new random sample before it is committed (the kunya tie-break looked perfect on its tuning sample; the fresh one gave 59/60).
3. **Hand-read data goes in git.** What a sub-agent reads by hand cannot be regenerated by a script, so the final data is committed next to the scripts (as `rijal_pilot/ilal_data/`), not left in the git-ignored `data/`.
4. **Sub-agents' scripts go to `rijal_pilot/review_drafts/`** (git-ignored); the main session copies in the ones it adopts. Each brief says that the main session may edit files while the sub-agent runs.

### Overview

| Phase | Status |
|---|---|
| 0. Preparation and backups | ✅ Done |
| 1. Pilot (Tahdhib + Taqrib, name linking, isnad test) | ✅ Done |
| 2. Full narrator registry from Shamela | ✅ Done (ties deferred) |
| 3. Ilal data (mudallisin, mukhtalitun) | ✅ Done (bare-name ties deferred) |
| 4. All hadith texts from Shamela | ✅ Done |
| 4b. Resolver fixes from Shamela's links | ⏸ Deferred (user decision) |
| 5. Code changes (Domain, loaders, Ilal, tests); resolver stays in Python (option B) | ✅ Done |
| 5b. Port the resolver to C# (option A) | ⏸ Later (after Phase 6, if switching) |
| 6. Build a separate database and compare with v2 | ✅ Done; **switched on 2026-10-05** (user decision) |
| 7. Remove Itqan and update docs | ✅ Done (merge into `master` waiting for the user) |
| 8. Review fixes found on `/takhreej` (2026-10-05): grade, compiler links, kin words, tahwil | ✅ Done; app switched to **`SmartHadithTree_ShamelaV5`**, the user verifies (§7 Phase 8) |

### Phase 0 — Preparation ✅

- [x] Rename the old backup to v1, take a compressed and checksummed v2 backup, document both in `backups/README.md` (`f08a0bc`)
- [x] Track `backups/README.md` in git while keeping `.bak` files ignored (`f08a0bc`)

### Phase 1 — Pilot ✅

- [x] Check whether Itqan's Mizan `entry_id`s match Shamela's numbering: 60% exact, ~90% recoverable with a nearby name match
- [x] `ShamelaLuceneDumper` accepts book IDs as an argument (`03fdae4`)
- [x] Parse Tahdhib al-Kamal: entries, symbols, shuyukh/talamidh, attributed quotes, cross-references (`03fdae4`)
- [x] Link list names to entries and verify precision by manual samples (`03fdae4`)
- [x] Isnad test on 500 Bukhari hadiths (`03fdae4`)
- [x] Parse Taqrib, align it with Tahdhib, map verdicts to Ibn Hajr's 12 ranks (`46b4035`)
- [x] Write the handoff document and point `AGENTS.md` to it (`c8e95fa`)

### Phase 2 — Full narrator registry ✅

Measurement tools:
- [x] Gap measurement per isnad depth, and which books cover the gap (`1f51a2b`)
- [x] Side-by-side comparison with the current system, with an agreement rate (`286c48d`, `3ea458f`)

Resolution:
- [x] Shuhra index: Taqrib aliases, bare laqab/nisba, "ابن X", fame tie-break (`845e5f2`)
- [x] Stricter matching: compound "عبد X", nasab order, kunya forms, edition typos (`1a2ec3d`, `3ea458f`, `5cd9709`)
- [x] Joint isnad resolution, `chain_resolver.py` (`3ea458f`)
- [x] Isnad clean-up: previous hadith's verdict, honorifics, place suffixes, "X هو Y", "وهب" not treated as a conjunction (`1a2ec3d`, `3ea458f`)

Compilers' shaykh books (via `parse_shaykh_books.py`):
- [x] al-Hakim: الروض الباسم (14463), رجال الحاكم في المستدرك (29742) (`1a2ec3d`, `5cd9709`)
- [x] al-Tabarani: إرشاد القاصي والداني (29745), تحفة الغريب (1208) (`5cd9709`)
- [x] al-Bayhaqi: إتحاف المرتقي (123666), السلسبيل النقي (123667), with exact name forms and al-Hakim merged as his shaykh (`1741406`)
- [x] Ibn Hibban: ري الظمآن بتراجم شيوخ ابن حبان (1498), with his kunya table as loose aliases; bare-kunya aliases made loose; full-name fame fallback in the resolver (`c5dbe46`)
- [x] al-Daraqutni: الدليل المغني لشيوخ الدارقطني (7852), same `bracket` layout (`2c51097`)
- [x] Ibn Khuzaymah: المسالك القويمة (151171), `runs` layout. Abu Awanah: no dedicated book (241 checked). General matching fixes found on the way, and five Companions recovered in the Tahdhib parse (`38c03f9`)
- [x] Ahmad and Malik: تعجيل المنفعة (1893), `tajil` layout; chains start after the compiler's own name (`10461aa`)
- [x] Early compilers (Abd al-Razzaq, al-Tayalisi, al-Shafi'i, al-Humaydi, Sa'id b. Mansur, Ishaq, al-Bazzar, Abu Ya'la): measured, 72–77%; no shaykh books needed. Compiler openers and Taqrib laqabs fixed al-Humaydi (`34125c6`)
- [x] Measure every remaining book of the 31 against the current system at least once: 72–82%. Direction marks stripped from isnads, "عن أبيه" via the father's kunya (`d65b670`)

General rijal books for what remains (each needs its own parser):
- [x] تاريخ بغداد (736): `khatib` layout, stricter Tahdhib skip, cross-book duplicate merge; al-Daraqutni 74% → 76% (`bcf95f2`)
- [x] سير أعلام النبلاء (10906): **closed, not parsed** — Lisan showed that extra biography books add namesakes about as often as narrators (182 gained / 221 lost). Reconsider only if grading needs its verdicts
- [x] لسان الميزان (36357): parsed (`parse_lisan.py` → `lisan.json`, 9,170 entries, 1,646 cross-references; first draft by a sub-agent) but **not merged** into the resolver. Even with namesakes of Tahdhib narrators skipped (same ism + father) and Tahdhib laqabs used as isms («بندار بن محمد») skipped, its 3,544 added narrators gained 182 names and lost 221 across the 31 books: criticised men rarely meant in these isnads, tying with «قتيبة», «علي بن المديني», «الحسن بن سفيان». Kept for narrator grading (Phase 3/5); the `lisan` layout in `parse_shaykh_books.py` is ready but commented out (`61d1618`)
- [x] الثقات ممن لم يقع في الكتب الستة (96165): **closed, not parsed**, same reason as السير

Remaining resolver gaps:
- [x] Names by grandfather or by "ابن X" («عبد الله بن أحمد بن حنبل», «عثمان بن أبي شيبة», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية») (`afaef5c`)
- [x] Isnad clean-up: «رضى الله عنه» with ى, accusative «أبا هريرة» / «جابرا», «قالا» after a name, «أبي، ح»; agreement no longer counts names past the end of the current chain (`afaef5c`)
- [x] Al-Baghawi as «ابن منيع»: tried and not kept. A maternal-grandfather candidate («ابن بنت أحمد بن منيع» → «ابن منيع») fixed 3 al-Daraqutni names but broke 2 real ties («ابن نمير» → سوادة بن علي, «ابن حرب» → الحسن بن عثمان); 73 headers say «ابن بنت X» and most are not called «ابن X». Worth a dedicated alias only if the precision review finds more
- [x] Companions and common names when context is weak: Tahdhib lists repaired (women, line breaks, separators, «N:» heads), fame tie-break for equally linked candidates, «ابن عمر» / «أبو سلمة بن عبد الرحمن» / «أبي بن كعب» / «ذكوان أبو صالح» matching (`b15bcd2`)
- [x] Real ties left undecided — «عبد الله» bare (146 names on the 31-book bench), «أبو معاوية» al-Darir / Hushaym (103), «أبو إسحاق» al-Sabi'i / al-Shaybani (31). Deferred with the condition that Phase 3 measure how often such a tie hides a mudallis / mukhtalit: 7.7% of tadlis findings, so fixed in Phase 3 by the kunya tie-break (§6.4; 96 «أبو معاوية», 60 «عبد الله», 19 «أبو إسحاق» resolved) (`d8845ce`)
- [x] The compiler's own shaykh list as context for the first link: already in the resolver; al-Hakim was not found as the compiler, fixed (Mustadrak 78% → 79%) (`56105f6`)
- [x] «عن أبيه» (diagnosed by a sub-agent: 410 unresolved with a known son): the father may be the one registry entry whose nasab continues the son's (father + grandfather), even when unlisted, but only when the son is certain; it also breaks ties between listed shaykhs; «أبيه» must be the father of the son chosen on the path (it had paired «أبو بردة» with another candidate's father). Tahdhib heads with symbols and no colon («ر ٤ شعيب بن محمد», ~280 incl. «تمييز») fixed, so عمرو بن شعيب's father is found. 145 «أبيه» gained, 29 changed (mostly corrections: al-Baqir → علي بن الحسين), 2 lost. A rule rejecting a listed father whose own father differs from the grandfather was tried and dropped (it lost «سلمة بن عمرو بن الأكوع», «بريدة») (`0e4d8b1`)
- [x] Teacher lists missing from the Tahdhib parse: «روى عن ك سالم» (عمرو بن شعيب's typo'd colon; his «عن أبيه» now resolves, 65 names) and «روى عن X» with no colon at a line start (27 narrators: أحمد بن بشير، يعلى بن الحارث، العرزمي، أبي بن كعب), used only when no «روى عن:» exists and before the students. Full bench after the «عن أبيه» step: 455 narrators gained, 168 lost (39 had agreed; mostly header changes from the symbol fix); table updated (`bda1dcc`)
- [x] Port `ContextualDisambiguator`: rules listed and Itqan ids mapped (sub-agent), 28 unconditional rules our evidence confirms used as a fallback; the agreement metric uses the id mapping (`59df440`)
- [x] Map every Itqan id used in the current chains to the registry (sub-agent; `review/map_itqan.py` → `review/itqan_id_map_all.json`, git-ignored): 16,337 ids, 9,027 exact / 2,732 probable / 4,578 none; 70 of the 77 hand-checked ids reproduced. "Probable" can be wrong (Abu Hurayra → another «عبد الرحمن بن صخر»), and so can an "exact" reached by eliminating namesakes (1 of 12 in a spot check); exact with a single nasab candidate held up. Used in the metric it changed nothing (those narrators already agree by name), so the metric keeps the 77 hand-checked ids; the full map is for Phase 6's comparison of the two databases
- [x] **Closed as a known residual:** later namesakes chosen by chain (17 names on the 31-book bench: «أبو هريرة» → الكلابي, «أبو الوليد الفقيه» → ابن عرق). Deciding them needs death dates; revisit when Taqrib's dates are used for grading
- [x] Measure precision on a systematic sample: 150 names, 94% (`369fd56`)
- [x] Segmentation fixes from the review: the «ب» trailer bug, «- وهو ابن Y -», «يعني X», «X وأبي Y» (`a5a6751`)
- [x] Kunyas glued in the edition («أبوالتياح», «أبويحيى», ~170 headers) were not read, so «أبي التياح» found only a bare stub from تعجيل المنفعة instead of يزيد بن حميد: split in `norm` / `soft_norm` (not «أبوه», «أبواب», «أبوين»). +1 point in 6 books; «أبي البختري» is now سعيد بن فيروز, not a later namesake (`4453885`)
- [x] Re-measure on 500 names with the sub-agent first pass: **97.0%** (95% CI 95.5–98.5) (`afcfbee`)
- [x] Fix the 500-name review's error classes: Latin-digit footnote marks «(1)», «هذا» not a name, «بعض» unnamed, «يعنون / يعنيان», Muslim's speaker tags («… بن المثنى، قال ابن المثنى» repeats him), the Taqrib alias joins «ابن X» candidates («ابن علية» was his son Hammad), the grandfather rule ignores the narrator's own kunya («مالك بن يحيى» ≠ مالك بن دينار أبو يحيى; an ancestor's «بن أبي شيبة» stays), and Tahdhib heads with a bracketed number («(٢٢٥١) ع: سعيد بن أبي الحسن», 4 entries; 63 later entries renumbered by one, so their `tk` ids changed). 151 names gained, 84 changed — mostly the targeted errors; +1 point in 3 books (`b9b2ad0`)

Housekeeping:
- [x] Delete the superseded `parse_hakim_books.py` and `isnad_test.py` (user approved)
- [x] One reproducible pipeline that writes the final registry (`pipeline.py`; first draft by a sub-agent; strict list links and Companion ranks added in review) (`171eef4`)

### Phase 3 — Ilal data ✅

User decisions at the start: الاغتباط (130) and التبيين (1187) as cross-checks only; the editors' notes kept, tagged `source: editor`; a tie is "significant" from 1% of the findings.

- [x] Dump the five books with their footnotes (`<id>_foot.tsv`) (`75ae047`)
- [x] Save each name's registry id, transmission verbs and tied candidates in bench runs; 31 books unchanged (`7c99238`)
- [x] طبقات المدلسين لابن حجر (1186), replacing the 16 hand-written mudallisin: 152 in five tiers + 22 in the appendix; linked 158 / 174 after a full review (`b8870f0`)
- [x] الكواكب النيرات (309) and المختلطين للعلائي (25846), replacing the 7 hand-written mukhtalitun, with heard-before / heard-after students: 164 narrators, 173 student claims with quotes, 76 group rules (`a88e692`)
- [x] Link both to registry IDs, with a review of every link (`b8870f0`, `a88e692`); pipeline step 7 writes `ilal.json` (`a88e692`)
- [x] Measure on the 31-book bench against the seeds: tadlis 3,243 vs 2,326, ikhtilat 4,325 vs 950 (`ilal_bench.py`, `b8870f0`, `d8845ce`)
- [x] Ties hiding findings: 7.7% of tadlis, past the 1% condition; kunya tie-break, 98% on a fresh sample (`d8845ce`)
- [ ] **Deferred until after Phase 7 (user decision, 2026-10-04):** bare «سفيان» / «حماد» / «عطاء» ties, which hide 15% of ikhtilat findings (mostly light / disputed mukhtalitun)

For Phase 5 (from what Phase 3 found):
- `IlalSeedService` reads `ilal.json` by registry id instead of the seeds; `MudallisTier` from Ibn Hajar's tiers (the appendix has no tier).
- `MukhtalitHearing.Timing` needs `Both` and `Conflict` besides `Before` / `After`, and the narrator needs the books' severity: most ikhtilat findings are from narrators the books call light or disputed, and most have no timing. Group rules («أهل البصرة قبل») and "nobody heard after" are in the data and not yet used by any rule.

### Phase 4 — Hadith texts ✅

User decisions at the start (2026-10-04):
- **Arabic only.** Itqan's English translations and grades are not carried over.
- **Each edition's own numbering.** Itqan's hadith numbers are mostly wrong, so editions are chosen for quality (a reliable editor, footnotes, complete text), not to match them. Old records are mapped to new ones by text, not by number.
- **Shifted records are fixed only in the new data** (`data/shamela/`); `data/itqan/sunni` and the live database stay as they are.

**Output format** (`data/shamela/`, git-ignored, rebuilt by script from `data/shamela/dump/`):

```
data/shamela/<slug>/book.json   {"slug", "title", "shamela_id", "edition", "compiler", "records", "numbered"}
data/shamela/<slug>/index.json  [{"chapter", "file", "name_ar", "count"}]   (same shape as Itqan's)
data/shamela/<slug>/<n>.json    [record, ...]   one file per top-level title (كتاب)
```

A record:

```json
{
  "id": 412,                       // 1..N in book order, stable for a given edition and builder
  "number": 380, "number_label": "٣٨٠",   // the edition's own number (null when the book has none)
  "kind": "hadith",                // "hadith", or "text" (the compiler's prose with no isnad)
  "bab": "…",                      // nearest sub-title above the record, if any
  "vol": "1", "page": 478, "page_end": 479,   // printed volume and pages (Shamela page.part / page.page)
  "arabic": "حدثناه أبو عبد الله …",    // full cleaned text, harakat kept, footnote marks and page marks removed
  "parts": [{"type": "isnad", "start": 0, "end": 210}, {"type": "matn", "start": 210, "end": 400},
            {"type": "remark", "start": 401, "end": 470}],   // spans of "arabic" (task 4.5)
  "footnotes": [{"at": 209, "text": "إسناده صحيح …"}],         // the editor's note, anchored at its mark's offset
  "narrators": [{"start": 9, "end": 31, "man": 3889}],         // only in Shamela's linked editions: span of "arabic" + S1.db narrator id
  "groups": [{"start": 210, "end": 400, "key": 2464}]          // only in linked editions: a <hadeeth-M> matn span; M = hadeeth.db key_id
}
```

`arabic` keeps the field name the ETL and the bench already read; `parts` may hold several isnads (tahwil, «وحدثنا … بهذا الإسناد»), a `remark` is the compiler's own comment («هذا حديث صحيح …», «قال أبو عيسى …», «لم يروه عن … إلا …»), and a `note` is the editor's text inside the record (al-Darimi's «[ب ١٢٩٧، د ١٣٦٢ …] تحفة …، إتحاف …», «[حكم حسين سليم أسد]»); a `kind: "text"` record has one part `{"type": "text"}`. Records are cut at the edition's in-text hadith numbers («٣٨٠ - حدثنا»), never at Shamela's page boundaries (the cause of §6.1); pages before the first numbered hadith (editor's introduction) are skipped.

- [x] Detect shifted record boundaries in the existing extraction (`check_record_boundaries.py`, `1741406`)
- [x] Re-split al-Mustadrak, al-Sunan al-Kubra, Shu'ab al-Iman and al-Bazzar on the hadith-number markers, so each record holds its own isnad and matn (§6.1). New builder `scripts/shamela4-extractor/build_shamela_books.py` (first draft by a sub-agent): one text stream per book, cut at the in-text numbers (`380 -`, `[3936]`, `4100 م-`, `6769/1-`, `1/ 830`; sequence-aware, so takhrij numbers, verses and years are not cut), footnotes matched by mark per page, printed page from `page.page` and `⦗N⦘`. All 19 books: **0 shifted** by `check_record_boundaries.py` (al-Mustadrak 19.3% → 0, al-Sunan al-Kubra 29.8% → 0, Shu'ab 34.5% → 0, al-Bazzar 0.6% → 0, and their prefix tails 54% / 45% / 24% / 10% → 0); 16 random records of these four checked by hand, all right; the "no marker" records open with «نا» or «عبد الرزاق، عن» (checked).
- [x] Choose a Shamela edition for each of the 12 primary books (survey by a sub-agent; user decision 2026-10-04). Where Shamela links the isnad's narrators (below), that edition is used even with fewer footnotes:

  | Book | ID | Edition | Why |
  |---|---|---|---|
  | al-Bukhari | 1681 | ط السلطانية | narrator links |
  | Muslim | 711 | ط التركية | narrator links, voweled |
  | Abu Dawud | 654 | ط دهلي مع عون المعبود | narrator links |
  | al-Tirmidhi | 7895 | ت بشار | narrator links, critical text (its appended Ilal is missing from the Lucene store) |
  | al-Nasa'i | 829 | ط المصرية | narrator links, voweled |
  | al-Muwatta (Yahya) | 28107 | ت الأعظمي | narrator links; numbers every narration (3,676) |
  | Ibn Majah | 98138 | ت الأرنؤوط | takhrij and grades |
  | Ahmad | 25794 | ط الرسالة | the only complete edition, takhrij |
  | al-Darimi | 36114 | ت الزهراني | the only one with takhrij (~313 numbers not at a line start: to check) |
  | al-Adab al-Mufrad | 12991 | ت عبد الباقي | |
  | al-Shama'il | 13037 | ت الجليمي | |
  | Ibn Abi Shayba | 333 | ت الشثري | most complete (40,754 numbers), unvoweled |

- **Finding: Shamela links narrators.** Seven editions (the six above with links, and al-Tayalisi 1456, already one of the 19) wrap each isnad narrator as `<a href="inr://man-N">` (~200,000 names: Bukhari 37,109, Muslim 48,270, Abu Dawud 31,629, al-Nasa'i 35,128, al-Tirmidhi 24,493, al-Tayalisi 15,226, al-Muwatta 7,189) and each matn as `<hadeeth-M>`. N is an id in Shamela's narrator encyclopedia, `database\service\S1.db` table `b` (18,989 narrators: short and full name, death year, the critics' quotes with their books, and kunya, nasab, residence, travels, Taqrib tabaqa, Ibn Hajar's and al-Dhahabi's verdicts). Its text is a simple byte substitution, decodable from the linked names. `service\hadeeth.db` groups the same hadith across 9 books (`key_id` → book, page). **User decision:** keep the ids in the records and use them to measure our resolver now; whether to merge the encyclopedia into the registry is decided after the measurement.
- [x] Measure the new texts on the bench: `map_records.py` pairs each new record with the current hadith by word trigrams of its opening (one record per current hadith; where 80%+ of text matches also agree on the edition's number — 15 of the 19 books — the number is required, since a shared matn and isnad end once paired two different hadiths); `bench.py --texts shamela` reads `data/shamela/<book>` through `maps/<book>.json`, and `compare_current.py` takes `CURRENT_MAP`. Unchanged on the Itqan texts (31 books, 0 gained / 0 lost against `final3`). On the new texts (run `p4s19`, a different 500-hadith sample): coverage within ±2 points in every book; agreement al-Mustadrak 61% → **80%**, al-Sunan al-Kubra 45% → **66%**, al-Bazzar 81% → 87%, others ±2; names past the current chain al-Mustadrak 984 → 423, al-Sunan al-Kubra 555 → 140. Shu'ab 79% → 70%: its "disagreements" are hadiths whose current chain lacks the isnad's upper part (al-Bayhaqi's shaykhs), not wrong pairs (checked), so it stays †. A hand check of 10 text-only pairs (al-Mustadrak, al-Bayhaqi) found 1 wrong pair.
- [x] Extract them with footnotes (`foot`) for takhrij and editors' grades, and Shamela's narrator links: the builder now covers all 31 books (12 more marker forms, by a sub-agent, reviewed). Bukhari's `N - M -` blocks are one record (number N, label both); Muslim's `N - (M)`: `number` = M, the Abd al-Baqi number (N restarts in every kitab), full label kept, every chain line its own record; al-Darimi's `N - (k)`; Ahmad's `* N -`; the Muwatta's `global/ local` (number = global; 2,917 numbered, its other numbers are chapter headings in the same sequence); `(م)` extra chains as their own record with the same number; numbered headings («١١٣٢ - باب») never cut; isolated typo'd numbers repaired (`number_fixed`, 69 records); Muslim's editor's afterword cut at the «_____» rule. Narrator links → `narrators` spans (every span equals its link text: 0 mismatches in the 7 linked books, 183,000+ spans), matn groups → `groups`. 0 shifted records in all 31 books; the 18 non-linked old books byte-identical to the 19-book builder. Records: Bukhari 9,250, Muslim 7,757, Abu Dawud 5,318, al-Tirmidhi 4,264, al-Nasa'i 5,799, the Muwatta 2,946, Ibn Majah 4,418, Ahmad 27,708, al-Darimi 3,577, al-Adab al-Mufrad 1,347, al-Shama'il 406, Ibn Abi Shayba 40,759. Hand check: 14 random records of 7 new books, 13 right; the 14th found the Muwatta's unnumbered `N/ k` records (58), fixed with three other classes found by a scan for unnumbered records starting with a number. Last records checked: al-Daraqutni, Abd al-Razzaq and Sa'id b. Mansur end with a long hadith; Ibn Hibban's holds his own closing words (and two short editor's lines), Shu'ab's the copyist's colophon (1159 AH) — kept, for the splitter to mark as remarks; 235 al-Darimi numbers are absent from the edition's text.
- [x] Decode Shamela's narrator encyclopedia: `rijal_pilot/decode_s1.py` → `data/shamela/narrators.json` (18,989 narrators, 181,825 critics' quotes with book / volume / page; first draft by a sub-agent). Checked by hand on five narrators (al-Humaydi, Ibn Uyayna, Shu'ba, al-Hakam, Ibn Abi Shayba): names, kunya, death, tabaqa, verdicts and quotes right. 6,375 of the 6,377 ids linked in the 7 books are in it.
- [x] Segmentation for the new editions: al-A'zami's Muwatta puts «؛» after names («مالك ؛ أنه», «أبيه ؛ أنه») and «عائشة زوج النبي» without a comma; «؛» now ends a name like «،», and «زوج (النبي)» like an honorific. Itqan texts: every book's figures unchanged (the names gained and lost are the same narrators without the «؛»). New texts (run `p4links3`): the Muwatta 62% → 71%, agreement 87% → 89%. On the new texts the linked books read Bukhari 90%, Muslim 87%, Abu Dawud 88%, al-Tirmidhi 93%, al-Nasa'i 89% (+1 to +3 over the Itqan texts), agreement level or up. Found on the way, left for the link measurement: «أم سلمة» is matched to men named Salama (Salama b. Sakhr, b. Kuhayl: ~30 names) and «أم حبيبة» to حبيبة بنت عبيد الله — «أم» is not read as a kunya.
- [x] Measure our resolver against Shamela's narrator links (sub-agent, reviewed; scripts in `rijal_pilot/links/`). `map_s1.py` maps S1 ids to registry ids from independent evidence only (the Tahdhib al-Kamal volume/page cited in S1, nasab, death, tabaqa, Ibn Hajar's verdict, kunya, nisba; never from our resolutions): of the 6,375 ids in the 7 books 5,060 exact, 584 probable, 731 none (93% of occurrences exact). `run_links.py` resolves 2,000 records per book (the Muwatta and al-Tayalisi: all), `eval_links.py` aligns our names with Shamela's spans (92%) and compares. **Precision 97.55% on 54,411 names** (98.04% counting a registry duplicate of the same narrator as right), in line with the 97.0% hand review of §6; by book 96.9% (Abu Dawud) to 98.4% (the Muwatta); by method `chain` 97.9%, `unique` 97.6%, `default` 99.6%, `chain_named` 98.3%, `chain_fame` 93.3%, `father` («أبيه») 93.4%. We resolve 92.6% of the linked names. A hand-classified random 60 of the 1,069 disagreements (`links/sample60_classified.json`, second-checked: all 15 non-"ours" and 6 of the 45 "ours" re-read): 45 our error, 8 registry duplicates, 5 Shamela's link wrong (Bukhari's «الفزاري» from Muhammad b. Salam is Marwan b. Mu'awiya; Muslim's «ابن نمير، حدثنا محمد بن بشر» is Muhammad b. Abdullah b. Numayr), 1 edition typo, 1 alignment. Error classes over all 1,069: a bare ism / kunya / nisba resolved to another bearer 467; a shaykh-book entry chosen over the Tahdhib narrator 204 («طاوس» → `tuhfa:2179` ~100, «علي» → `rayy:260`); a full name resolved to another person 190 («عبد العزيز بن أبي سلمة» → not al-Majishun, 27); «أبيه» 91; «ابن X» / «يعني ابن X» cut 45; segmentation 38; «أم X» → a man 34.
  - **Bare-name ties** (deferred in Phase 3): Shamela's narrator is always among our tied candidates, and it links the tied «سفيان» to al-Thawri 117 / Ibn Uyayna 8, «حماد» to Ibn Salama 44 / Ibn Zayd 15 (Abu Dawud: Ibn Salama 42 of 49), «عبد الله» to Ibn Mas'ud 72 / Ibn Umar 21 (al-Tayalisi: Ibn Mas'ud 41 of 41), «هشام» al-Dastuwa'i 15 / Ibn Urwa 13; picking the most-cited «سفيان» would agree only 8 times in 125.
- [x] Separate the compiler's own remarks from the matn, and keep volume and page references: `scripts/shamela4-extractor/split_parts.py` (first draft by a sub-agent, two rounds of review) fills `parts` in place after the builder. Grammar only (no registry): it walks the chain (verbs, «عن», tahwil, speaker tags, asides such as «شك X» / «أو أحدهما» / «يحدث … أنه سمع X» / «يعني»), then a second isnad at a sentence start, a remark from the book's own openers (`REMARKS` table: «قال أبو عيسى», «هذا حديث صحيح …», «لم يرو هذا الحديث عن … إلا …», «وهذا الحديث لا نعلمه …»), and the editor's `note`s (al-Darimi's takhrij lines, Abu Ya'la's «[حكم حسين سليم أسد]», al-Adab al-Mufrad's grades, Ibn Khuzaymah's «قال الألباني / الأعظمي»). Measured on Shamela's matn groups (`--eval --part held`, records with id % 5 == 0 held out; matn start within ±3 words): with the editions' quotation marks (used in the six linked hadith books) Bukhari 97.7%, Muslim 94.4%, Abu Dawud 94.9%, al-Tirmidhi 98.5%, al-Nasa'i 98.2%, al-Tayalisi 99.8%; blind (no quotes, the estimate for the other books) 88.9 / 82.1 / 85.3 / 91.5 / 90.0 / 91.9%, the Muwatta 84.6% — part of the blind gap is Shamela's own convention (the matn starts after «X قال:» in ~80% of records and at the frame in ~20%). The first round's hand check (58 / 60) was lenient; the main session's own check found ~9 / 13 and sent back four failure classes (asides ending the isnad early, a dialogue's «قال:» taken for a remark, missed «ولم يرو …» / variant notes, editor's takhrij in the matn); after the fix, a fresh sample of 14 records from 7 books not used in tuning: 13 right, 1 with a follow-up isnad and an editor's line left in the matn (Ibn Khuzaymah ٢٥٠٤). Left: al-Bayhaqi's chains for critics' statements (Ibn Adi, Ahmad) are marked as a second isnad + matn; ~1,730 chain-less records in Ibn Abi Shayba are mostly sayings without an isnad, a few are narrator lines from its appendix.
- [x] Move all 31 books to `data/shamela/` in one format (279,198 records; `build_shamela_books.py`, then `split_parts.py --write data/shamela <slugs>`)

### Phase 4b — Resolver fixes from Shamela's links ⏸ Deferred

**Skipped for now (user decision, 2026-10-04):** the resolver goes into Phase 5 as it is (97.55% against Shamela's links). The tasks below stay as known residuals; pick them up after Phase 6 if the comparison shows a need. Fixing them in the Python resolver is cheaper than after the C# port (the link measurement runs on the Python version). The bare-name ties of the 7 linked books can instead be settled in Phase 5, by reading Shamela's narrator ids when their chains are rebuilt.

User decisions (2026-10-04): after Phase 4, fix the largest error classes found by the link measurement (Phase 4), each measured with `links/eval_links.py` on held-out records (rule 2: tune on part of the records, confirm on the rest) and with `bench.py --books all`; and, in the 7 linked books, settle the bare-name ties («سفيان», «حماد», «عبد الله», «هشام» …) with Shamela's own link when it is one of our tied candidates (other books stay undecided).

- [ ] Shaykh-book duplicates of Tahdhib narrators chosen over the Tahdhib entry (204; «طاوس» → `tuhfa:2179`)
- [ ] «أم X» read as a kunya, never matched to a man named X (34)
- [ ] Full names resolved to another person (190; «عبد العزيز بن أبي سلمة» = al-Majishun)
- [ ] Bare ism / kunya / nisba resolved to another bearer (467; review the largest names first)
- [ ] «أبيه» (91), «ابن X» / «يعني ابن X» cut (45). **Partly covered by Phase 8 (C3):** a first-person «أبي» and «جده» are now resolved, and the «عمرو بن شعيب» grandfather; the 91 «أبيه» that Shamela links to someone else and the «ابن X» cuts are not looked at
- [ ] Bare-name ties in the linked books from Shamela's links

### Phase 5 — Code ⬜

**User decision (2026-10-04): Option B.** The resolver **stays in Python**. Python writes the finished data (registry, resolved chains, Ilal data) as files and the C# ETL only **loads** them into the database. Estimated 3–5 working sessions. Option A (porting the resolver to C#) is recorded below as **Phase 5b** and is done later, if at all.

| | Option B (chosen) | Option A (Phase 5b, later) |
|---|---|---|
| Resolver | Python (`chain_resolver.py`, `link_tahdhib.py`, `gap_test.py`) | Ported to C# |
| C# ETL | Reads `registry.json` + a chains file | Runs the resolver itself |
| Rebuild needs | Python + the Shamela install | Only the data files |
| Risk of different results | Almost none | Medium; needs a parity test |
| Estimate | 3–5 sessions | 8–12 sessions |

Tasks (option B):

- [x] **5.1 Domain** (`AddShamelaRegistryFields` migration, generated only; applied to the Phase 6 database, never the live one). **Additive:** the new fields sit beside `ItqanId` / `ItqanGrade`, which stay until Phase 7 so the Itqan code and the live app keep building. `Narrator`: `SourceKey` (registry id, unique when set), `ShamelaManId`, `IbnHajarRank` (1–12), `Verdict`, `IkhtilatSeverity` (Light / Disputed / Harmful), `NoHearingAfterIkhtilat`; `ScholarEvaluation`: `SourceVolume`, `SourcePage` (the critic is `ScholarName`, the book `SourceBook`); `HearingTiming`: `Both`, `Conflict`; `MukhtalitHearing`: `Evidence`, `SourceBook`; new `MukhtalitGroupRule` entity (not on `IHadithTreeDbContext`, so mocks are unaffected). New files under `Infrastructure/Data/` need `git add -f` (the `data/` ignore rule matches them)
- [x] **5.2 Chains file:** `rijal_pilot/export_chains_shamela.py` (see §5). **274,597 records with an isnad (of the 279,198; the rest are the compilers' prose) over 31 books in about 10 minutes** (`data/shamela_rijal/chains/`, 127 MB, git-ignored). Same code as the bench, so parity is exact: against a fresh bench run on the Shamela texts (`parity5`, 10 books, ~4,000 isnads) **every difference is a tie settled by Shamela's link, none otherwise** (the 7 linked books differ only where a tie was settled: 38 names in Bukhari's 330 isnads, 70 in Muslim's 461; Ibn Majah, Ahmad, al-Mustadrak, al-Bayhaqi, Ibn Abi Shayba are identical; the few "no chain" cases are prose records the bench had sampled). Shamela's narrator id settled **4,446 ties in the 7 linked books** (Bukhari 837, Muslim 1,172, Abu Dawud 744, al-Tirmidhi 327, al-Nasa'i 959, the Muwatta 86, al-Tayalisi 321: «سفيان» al-Thawri 375 / Ibn Uyayna 48, «حماد» Ibn Salama 120 / Ibn Zayd 77, «عبد الله» Ibn Mas'ud 134 / Ibn Umar 64); ties left: 3,159 in those books. Coverage of the names by book: 67% (the Muwatta) to 94% (al-Tirmidhi). The full run found a crash the 500-record samples never hit: `gap_test.py` built a regex from the word after «ابن» without escaping it, and a stray «(» stopped al-Bayhaqi's Shu'ab, Abu Awanah and Ibn Abi Shayba; fixed with `re.escape` (no other result changes)
- [x] **5.3 ETL loaders** (`Etl/Parsers/Shamela/`: `ShamelaDatasetParser`, `ShamelaMappers`, `ShamelaModels`, `ShamelaBookNames`; the Itqan code is untouched). Source path = `data/shamela_rijal` (`registry.json`, `chains/`, `review/links/s1_registry_map.json`), texts and `narrators.json` from the sibling `data/shamela/`; `dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal` (the parser is registered first, so it wins over the Itqan parsers for a folder with `registry.json`). **Real data, no database yet:** 23,502 narrators (5,055 linked to Shamela's encyclopedia: death year 3,635, kunya 9,159, rank 7,882), 274,597 hadiths (268,391 with an isnad text), 1,125,644 transmissions, 116,670 critics' quotes, 114,301 teacher/student relations (`source = 'shamela'`; `ParsedDataset.NarratorRelations` and the bulk ingestion are new). Decisions made on the way: (1) `MatnArabic` keeps the **full** record text (isnad + matn, as the Itqan data did, so the app's `MatnText` heuristics and display do not change) and `FullIsnadText` is the isnad part(s) of `parts`; switching to matn-only is a later choice (Phase 6). (2) **A name the resolver left undecided is a gap: no link across it** («A ← ? ← C» is never «A ← C»; the old code bridged); the chain's first link is compiler ← first narrator; `TransmissionTerm` is the last verb after the student's name (null for the compiler's link). The old "stop at a Companion" rule is not needed: the resolver already cuts the isnad at the first «رسول الله / النبي». (3) **`BookName` keeps the app's current names** (`ShamelaBookNames`): Shamela's titles collide («المصنف» is Abd al-Razzaq's and Ibn Abi Shayba's, «السنن الكبرى» al-Bayhaqi's and al-Nasa'i's) and the Ilal rules test for «صحيح البخاري» / «صحيح مسلم». (4) Quotes: the registry's 26,639 (Tahdhib al-Kamal) plus Shamela's own for the exactly-mapped narrators, with book, volume and page, without its Tahdhib al-Kamal ones (duplicates). (5) Death years only where Shamela's encyclopedia gives one: Taqrib's words («مات سنة إحدى وستين») have no century. Kunya from the encyclopedia, else from the Tahdhib header. (6) Text fields are cut to their column limits (the longest Taqrib verdict is 513 characters, the column 500). The loader needs about 1 GB of memory (all texts at once). 15 tests in `ShamelaLoaderTests` (mappers, kunya, chain builder, relations; one runs the whole real data and returns silently where `data/` is absent)
- [x] **5.4 Grading and Ilal.** **Grades:** `NarratorGradeScale` has rank overloads (`ToTier`, `ToArabicLabel`, `IsCompanion`, `IsTabii`: Ibn Hajar's rank when known, else the legacy Itqan grade), and `ToGradeEn(rank)` gives the string the API and frontend still use (1 companion, 2–3 reliable, 4–5 mostly_reliable, 6–8 weak, 9 unknown, 10–11 abandoned, 12 fabricator); `HadithChainRepository`'s SQL and `NarratorService` return `COALESCE(ItqanGrade, from IbnHajarRank)` as `GradeEn`, and `GradeSummary` falls back to the Taqrib `Verdict`, so a Shamela-built database shows grades in the UI. `IlalNarrator` carries rank, severity and "nobody heard after". **Loader:** `Etl/Services/ShamelaIlalService` reads `ilal.json` by registry id (`dotnet run --project src/SmartHadithTree.Etl -- seed-ilal-shamela data/shamela_rijal`, after the main load): mudallisin with their tiers (the editor's appendix stays a mudallis without a tier), mukhtalitun with severity, "nobody heard after", the note, hearings (timing, the critic's words, the book) and group rules; it resets the flags first, so it is for a Shamela-built database only; the old `IlalSeedService` and `Seeds/*.json` stay for the Itqan database until Phase 7. On the real file: 158 mudallisin (138 with a tier), 131 mukhtalitun, 166 hearings, 75 group rules. **`IkhtilatRule` now weighs the books' severity** (Phase 3: 3,082 of the 4,325 old findings were light or disputed mukhtalitun): heard after: harmful → قادحة (غير قادحة in the Sahihs), disputed → غير قادحة, light → تنبيه; timing unknown: harmful → تنبيه 0.45, disputed → تنبيه 0.3 **only for a narrator graded صدوق يهم (rank 5) or weaker** (added in 5.6, see there), light → nothing; both / conflict → تنبيه unless light or only in the Sahihs; "nobody heard after" and heard-before → nothing; the critic's words and a group rule («من سمع منه قبل التغير …») are quoted in the evidence. Severity decisions: the books state none for 85 of 164 mukhtalitun, so the loader gives them **disputed**; where books disagree (4 narrators) the **most severe** wins; a narrator with no severity at all (the Itqan seeds) is still treated as harmful. Group rules are shown as a hint only: deciding a student's group needs data the students do not have (residence of the hearing). `TadlisRule` is unchanged (tier 3+). New tests: `NarratorGradeScaleTests`, `IkhtilatSeverityTests`, `ShamelaIlalServiceTests`. The counts of §6.4 on the C# side (3,243 tadlis; ikhtilat by severity) are checked in 5.6, which needs a database
- [x] **5.5 Tests: 135 in `SmartHadithTree.Tests` (63 before Phase 5).** New in Phase 5: `ShamelaLoaderTests` (15: mappers, kunya, chain builder, relations, and the whole real data), `ShamelaDatasetParserTests` (3: the parser end to end on a tiny dataset laid out like `data/shamela_rijal` + `data/shamela`: the app's book name, kitab from `index.json`, prose skipped, no link across an undecided name, the encyclopedia only for the exact mapping, relations deduplicated, and the run without the encyclopedia), `ShamelaIlalServiceTests` (loader on a small `ilal.json` and on the real one: 158 / 131 / 166, repeatable), `NarratorGradeScaleTests`, `IkhtilatSeverityTests`, `ShamelaGradingTests` (9: the rank beats the legacy grade, tier / label / generation from the rank, an untiered mudallis is not flagged, confidence by tier, and `IlalAnalysisService` on Shamela-shaped data from the database: severity, the critic's words and a group rule in the evidence, a dead end in the chain). `IlalTestBuilder` takes rank, severity, "nobody heard after", hearing evidence and group rules. **Left as they are on purpose:** `ContextualDisambiguatorTests` and `ShamelaSqliteParserTests` test the Itqan-era code that stays until Phase 7 (they go with it), and `IlalAnalysisServiceDbTests` keeps its legacy-grade case (the fallback still works; the rank-only case is in `ShamelaGradingTests`). The two data-dependent tests return silently where `data/` is absent. Not covered by any test: the changed raw SQL of `HadithChainRepository` (`GradeEn` from the rank), which needs SQL Server; 5.6 exercises it
- [x] **5.6 Check, on a scratch database** `SmartHadithTree_Scratch` (created with `dotnet ef database update --connection …`; the live `SmartHadithTree` was never written to: 115,735 narrators / 237,558 hadiths / 1,110,677 transmissions before and after). **Load:** `dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal` then `-- seed-ilal-shamela data/shamela_rijal`, with `ConnectionStrings__DefaultConnection` set: parsing 14 s, bulk insert 70 s, **1,654,714 records in 84 s**, the Ilal seed 2 s. **Counts in SQL Server:** 23,502 narrators (7,882 with a rank), 274,597 hadiths in 31 distinct books (268,391 with an isnad text; 9,432 with no link at all), 1,125,644 transmissions, 116,670 quotes, 114,301 relations, 158 mudallisin, 131 mukhtalitun, 166 hearings, 75 group rules; **0 orphan transmissions, 0 self-links**. **Link-level Ilal check, SQL against Python on the chains files and `ilal.json`:** 1,125,644 links, **53,955** ambiguous-formula links of tier-3+ mudallisin, **90,800** links from mukhtalitun with the same timing split (unknown 79,790, before 6,589, after 2,924, conflict 1,481, both 16): identical in both, so the load carries the data faithfully. **The API on the scratch database** (`--urls http://localhost:5199`): the tree gives `gradeEn` from the rank (reliable / companion), a narrator page gives kunya, death year, `gradeEn` and 251 quotes, and an Ilal analysis gives a severity-weighted ikhtilat finding with the books' words. **Four things found and fixed on the way:** (1) **`Program.cs` read `appsettings.json` after the environment, so `ConnectionStrings__DefaultConnection` was ignored and the first load attempts went to the LIVE database.** They failed in the bulk copy (the live table lacks the new columns) and rolled back, and the live database was verified unchanged; the ETL now adds environment variables last and logs `Database: <server> / <name>` at the start: **always check that line before a load**. (2) The chain builder kept only the last verb between two names, while the Python bench calls a link explicit when any verb is; `TermOf` now prefers an explicit-hearing verb, and `TransmissionTerms` knows the later books' abbreviations (ثنا، نا، أنا، أبنا, plus حدثه / حدثناه / أخبره …) and «قالت / يقول» as ambiguous. (3) A redundant `HasConversion<int>()` on `IkhtilatSeverity` was removed (EF stores enums as int already; the model has no pending changes). (4) **The ikhtilat rule still flagged 60,487 links, 48,689 of them "disputed, timing unknown" from famous reliable narrators** (Ibn Uyayna 11,859, Hisham b. Urwa 5,162, Jarir 3,595, Abd al-Razzaq 2,548): that case now needs the narrator to be graded rank 5 or weaker (Shuraik, Simak, Ibn Lahi'a, Asim stay). Link-level flags for the same 90,800 links, ignoring the Sahih exemption: **old rule 84,211 → now 25,975** (قادحة 632 after harmful, غير قادحة 2,285 after disputed, تنبيه 7,462 unknown-harmful and 14,177 unknown-disputed-weaker; dropped: 34,512 disputed-thiqah, 20,391 light, 5,201 "nobody heard after"). **Not done:** the per-hadith finding counts of §6.4 (3,243 tadlis / 4,325 ikhtilat on 15,894 sampled isnads) are not reproduced one for one: the C# service analyzes the turuq of the hadiths it is given, not a book, and the bench samples are Itqan-text records; the link-level equality above is the check of the data. `SmartHadithTree_Scratch` is left in place for inspection (drop it with `DROP DATABASE SmartHadithTree_Scratch`; Phase 6 builds `SmartHadithTree_Shamela`)

### Phase 5b — Port the resolver to C# (option A) ⏸ Later

Not needed for Phase 6. Do it **only if Phase 6 says to switch** and the user wants the ETL to rebuild without Python (it also serves Phase 7's goal of removing the old pipeline). The Python output is the test oracle.

- [ ] Port name normalization, `link_tahdhib` matching rules (§5), isnad segmentation and clean-up (`gap_test.py`), and the joint resolver with its tie-breaks (fame, named, father, default fallback, compiler start)
- [ ] Replace `ChainReprocessingService` and `ContextualDisambiguator`; drop the chains file from the ETL
- [ ] **Parity test:** C# against the saved Python runs (`data/shamela_rijal/results/<tag>/`): 0 differences on a golden subset (4 books first, then all 31), every difference explained
- [ ] Unit tests with golden cases from §5 («ابن عمر», «أبي بن كعب», «أم سلمة», the grandfather rule)
- [ ] **Since Phase 8 the port also has to cover** `tahwil.py` (branches, `join_heads`), `kin_form` / `grandfather_of` in `chain_resolver.py`, `compiler_items.py` and the opener rules in `compare_current.py`; the parity test compares chains **with branches** (`chains/<book>.json` has `branches`)

### Phase 6 — Build and compare ⬜

- [x] Build `SmartHadithTree_Shamela` separately, without touching `SmartHadithTree` (counts as in 5.6; report: `docs/shamela_phase6_comparison.md`)
- [x] Compare with v2: chains 74% position agreement on 220,603 paired hadiths (Shamela right in 9 of 20 sampled disagreements, v2 in 5), Ilal 793 vs 275 tadlis and 341 vs 0 ikhtilat on 4,650 paired hadiths, grading coverage 93% vs 99% of links, no text lost, API runs on both
- [ ] Not done: manual review of ~30 famous isnads, the frontend in a browser, the AI summary. Known regression: undecided names leave gaps in chains (§3 of the report). **Phase 8 closed some gaps** (a father or grandfather named by «أبي» / «جده» no longer breaks the chain, the compilers' own links exist, tahwil chains keep their upper part) but an undecided name still leaves a gap
- [x] Switch only if the new database is as good or better (user decision); take a v3 backup first: **v3 backup taken (`backups/SmartHadithTree_v3_2026-10-05.bak`) and the Api and Etl `appsettings.json` now point at `SmartHadithTree_Shamela`** (rollback steps in `backups/README.md`). The old `SmartHadithTree` database is left in place. Branch `feature/shamela-rijal` is not merged into `master`

### Phase 7 — Clean-up ⬜

- [x] Remove `data/itqan/rijal`, the Itqan parser and the Itqan-only code: `ItqanDatasetParser`, `ContextualDisambiguator`, `ShamelaSqliteParser`, `IlalSeedService`, `ChainReprocessingService`, `Seeds/`, `download_itqan_data.ps1`, their tests (134 tests pass). **Kept on purpose:** `data/itqan/sunni/` (sample input of the Python benchmarks), `build_itqan_books.py`, the `ItqanId` / `ItqanGrade` columns (the grade fallback reads them; dropping them needs a migration on the live database), the legacy-grade test in `IlalAnalysisServiceDbTests`
- [x] Update `AGENTS.md`, `docs/data_ingestion.md`, `README.md`, `docs/changelog.md` and `backups/README.md` (v4 backup of the Shamela database taken)
- [ ] **The user runs the app on the branch against `SmartHadithTree_ShamelaV5` (since Phase 8; `SmartHadithTree_Shamela` is v4) and verifies it** before the merge (Api: `dotnet run --project src/SmartHadithTree.Api`; frontend: `npm run dev` in `frontend/`). Check: search, the tree and the comparative tree, the narrator pages (is Ibn Shihab al-Zuhri easy to find?), the Ilal panel (tadlis, ikhtilat), and the known chain gaps from undecided names (§3 of `docs/shamela_phase6_comparison.md`). Not yet tested by anyone: the frontend in a browser and the AI summary (needs the Gemini key)
- [ ] Merge `feature/shamela-rijal` into `master` (user decision, only after the step above)

### Phase 8 — Review fixes from the `/takhreej` page ✅ (2026-10-05)

Found by reviewing the comparative page of the Mughira hadith (11 routes). All general rules, measured on the 31 books; details in §9. Commits `95fdedc` … `e897569` on `feature/shamela-rijal`.

- [x] **A1** the overall grade is computed from each route's weakest narrator above the compiler (it walked the wrong way and ignored the madar: صحيح → حسن)
- [x] **A2–A5, A6, B1–B4** fewer false findings (the compiler's own link, matn start and framing words, medoid base for matn variation, requested hadith order), «تباين مكاني» became a travel note, and the page fixes (header badges, refit on resize, matn-variation and travel icons)
- [x] **C2** the compilers named by bare words in student lists (al-Nasa'i 11 → 433 teachers, Ibn Majah 1 → 303, al-Tirmidhi 1 → 213)
- [x] **C3** first-person «أبي» and «جده»
- [x] **C1** tahwil: one hadith, several chains (24,113 hadiths); no schema change
- [x] Rebuild: registry, chains, `SmartHadithTree_ShamelaV5`, **v5 backup**; the Api and Etl `appsettings.json` point at it
- [ ] **The user** restarts the Api and checks the 11-route page (grade حسن, «لم يثبت اللقاء» 3), a branched tree (Mustadrak 493) and the single tree
- [x] **B5** (low-confidence findings fold away in the Ilal panel) and the scratch database `SmartHadithTree_ScratchC1` with `data/shamela_rijal_c1/` are done
- [ ] Left open on purpose, tracked in `docs/backlog.md`: «إبراهيم بن موسى» (Mustadrak 493's second chain), «وحدثنيه X» in Muslim, «كلهم عن X بهذا الإسناد» tails, heads whose last narrator is unresolved, more than 8 chains in a record

## 8. How to re-run everything

Day to day, from `data/shamela_rijal`:

```bash
python ../../scripts/shamela4-extractor/rijal_pilot/bench.py <tag>               # 4 books, vs the previous run
python ../../scripts/shamela4-extractor/rijal_pilot/bench.py <tag> --books all   # all 31, before a commit
```

The Ilal data and what it finds on a saved run (from `data/shamela_rijal`; `pipeline.py dump . --from 7` does the first two):

```bash
python ../../scripts/shamela4-extractor/rijal_pilot/parse_mudallisin.py dump mudallisin.json
python ../../scripts/shamela4-extractor/rijal_pilot/link_ilal.py .
python ../../scripts/shamela4-extractor/rijal_pilot/ilal_bench.py results/<tag> ilal.json --show 8
```

`--base <tag>` picks the run to compare with, `--review N` the number of new disagreements shown (default 12; use 20–25 before a commit). Each name in a saved run carries `vs_current` (`agree` / `differ` / `past`). Checked against the verified run: the same numbers on all 31 books, the same saved results, and `vs_current` reproduces the agreement figures; with no change it reports 0 gained / 0 lost, and with the grandfather rule switched off it lists exactly the lost names.

The whole registry in one command (from the repo root, after the dump step in §3; `<out>` gets the five intermediate files, the cache and `registry.json`):

```bash
cd data/shamela_rijal
python ../../scripts/shamela4-extractor/rijal_pilot/pipeline.py dump <out>
```

Checked: the five intermediate files are byte-identical to the step-by-step commands below, and two runs give the same `registry.json`. In `registry.json`, a list name links to a narrator only when it has one candidate or exactly one candidate lists the narrator back (3.4 links per list against 4.8 names; the resolver itself keeps every candidate). Companions get rank 1 (Ibn Hajar gives them no verdict or tabaqa) when Taqrib says so, or when Taqrib has no verdict and the shaykh list opens with the Prophet; 870 Tahdhib narrators. Run alone, step by step:

From the repo root, in Git Bash (the dump step in §3 comes first):

```bash
cd data/shamela_rijal
python ../../scripts/shamela4-extractor/rijal_pilot/parse_tahdhib.py dump tahdhib.json
python ../../scripts/shamela4-extractor/rijal_pilot/parse_taqrib.py dump taqrib.json
python ../../scripts/shamela4-extractor/rijal_pilot/align_taqrib.py tahdhib.json taqrib.json align.json
python ../../scripts/shamela4-extractor/rijal_pilot/parse_shaykh_books.py dump tahdhib.json extra_shaykh_books.json
python ../../scripts/shamela4-extractor/rijal_pilot/link_tahdhib.py tahdhib.json
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/bukhari current_chains.json "محمد بن إسماعيل بن إبراهيم بن المغيرة" 500 12
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mustadrak_hakim current_chains.json "محمد بن عبد الله بن محمد بن حمدويه الحاكم" 1000 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mujam_kabir_tabarani current_chains_tabarani.json "سليمان بن أحمد بن أيوب" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sunan_kubra_bayhaqi current_chains_bayhaqi.json "أحمد بن الحسين بن علي بن موسى" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sahih_ibn_hibban current_chains_hibban.json "محمد بن حبان بن أحمد" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sunan_daraqutni current_chains_daraqutni.json "علي بن عمر بن أحمد بن مهدي" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sahih_ibn_khuzaymah current_chains_khuzaymah_awanah.json "محمد بن إسحاق بن خزيمة" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mustakhraj_abi_awanah current_chains_khuzaymah_awanah.json "يعقوب بن إسحاق بن إبراهيم بن يزيد أبو عوانة" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/ahmed current_chains_ahmad_malik.json "أحمد بن محمد بن حنبل" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/malik current_chains_ahmad_malik.json "مالك بن أنس بن مالك" 500 0
# Early compilers, all against current_chains_early.json: musannaf_abdurrazzaq "عبد الرزاق بن همام بن نافع",
# musnad_tayalisi "سليمان بن داود بن الجارود", musnad_shafii "محمد بن إدريس بن العباس",
# musnad_humaydi "عبد الله بن الزبير بن عيسى", sunan_said_ibn_mansur "سعيد بن منصور بن شعبة",
# musnad_ishaq "إسحاق بن إبراهيم بن مخلد", musnad_bazzar "أحمد بن عمرو بن عبد الخالق", musnad_abi_yala "أحمد بن علي بن المثنى"
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/musnad_humaydi current_chains_early.json "عبد الله بن الزبير بن عيسى" 500 0
# The last 10 books, each against current_chains_<slug>.json (export_chains.ps1 -Books 'صحيح مسلم' ..., then split by book):
# muslim "مسلم بن الحجاج بن مسلم", abudawud "سليمان بن الأشعث بن شداد", tirmidhi and shamail_muhammadiyah
# "محمد بن عيسى بن سورة", nasai and sunan_kubra_nasai "أحمد بن شعيب بن علي", ibnmajah "محمد بن يزيد الربعي",
# darimi "عبد الله بن عبد الرحمن بن الفضل", aladab_almufrad "محمد بن إسماعيل بن إبراهيم بن المغيرة",
# musannaf_ibnabi_shaybah "عبد الله بن محمد بن إبراهيم بن عثمان"
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/muslim current_chains_muslim.json "مسلم بن الحجاج بن مسلم" 500 0
cd ../.. && python scripts/shamela4-extractor/rijal_pilot/check_record_boundaries.py data/itqan/sunni
```

The Phase 4 texts (from the repo root; the dump command of §3 with `data\shamela\dump` as output and the book IDs of `BOOKS` comes first):

```bash
python scripts/shamela4-extractor/build_shamela_books.py            # all books in BOOKS -> data/shamela/<slug>/
python scripts/shamela4-extractor/split_parts.py --write data/shamela <slug ...>   # adds parts in place
python scripts/shamela4-extractor/rijal_pilot/check_record_boundaries.py data/shamela
```

`compare_current.py` options (environment variables):
- `CHAIN_MODE=greedy` — use the old link-by-link walk instead of the joint resolver.
- `SHOW_DIFF=20` — list disagreements with the current system for review.
- `NO_CACHE=1` — do not use `data/shamela_rijal/cache/`. Linking the registry's ~70k list names takes most of a run (36 s per book); the result is cached under a hash of every registry file and every script in `rijal_pilot/`, so any change to data or code recomputes it. Checked on all 31 books: the old code, the new code without cache, with an empty cache and with a filled cache gave byte-identical results; a run of all 31 went from 373 s to 99 s. `bench.py` runs its first book alone so that, after a change, the other books load the rebuilt cache instead of each rebuilding it in parallel: a run of all 31 after a change went from 168 s to 133 s, with identical results.
- `SAVE=<file>.json` — save every sampled isnad with each name's resolution (ours, how, from which book) and the current chain, to review a run or diff two runs.

The C# ETL loads them (Phase 5.3; the database is the one in `appsettings.json`, so use a scratch one until Phase 6): `dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal`.

The chains the C# ETL loads (Phase 5.2; from the repo root, after the registry and `data/shamela/` exist; about 10 minutes):

```bash
python scripts/shamela4-extractor/rijal_pilot/export_chains_shamela.py --books all     # -> data/shamela_rijal/chains/<slug>.json
```

Python output with Arabic needs `PYTHONIOENCODING=utf-8` on Windows.

The sample input texts come from `data/itqan/sunni/<book>/`. The 19 non-Itqan books there were already extracted from Shamela; the 12 primary books will be re-sourced from Shamela in Phase 4.

## 9. Pitfalls (for whoever continues)

- **Isnads with several chains: tahwil (C1, 2026-10-05).** About 9% of the hadiths (24,113 of 274,597; Muslim 27%, al-Bayhaqi 24%, Tabarani's Kabir 16%, the Mustadrak 14%) have more than one chain, and the segmenter flattened them into one line: Mustadrak 493 joined «قتيبة» to «الصيدلاني» and lost al-Mughira to the 8-name cap. `tahwil.py` reads three written forms:
  1. **Alternatives, one shared tail:** «حدثنا A، وB، وC، قالوا: ثنا X، ثنا Y، عن Z» → A→X→Y→Z, B→X→Y→Z, C→X→Y→Z (also «A وB قالا», «قالوا جميعا:», and several sub-heads: «… قتيبة. وأخبرني الصيدلاني … ؛ قالا: حدثنا إسماعيل»). `«قالوا:»` in the matn is not a tahwil: the head must name two chains or more.
  2. **Parts joined by «ح»:** each part is a chain. A part that stops short of the Prophet takes the tail of the next part only from the narrator its last resolved narrator is listed with (`join_heads`, edge > 0, or the same narrator), so a wrong join is not made; without such a link the head stays a partial chain.
  3. **«. وأخبرني …» sentences** without «قالا»: handled like the «ح» parts.
  - Output: `chains/<book>.json` records keep `names` (the first chain, the one the bench measures) and gain `branches` (every chain, the first included, each from the compiler upward; up to 8; names per chain 14 instead of 8). The loader (`ShamelaChainBuilder.BuildAll`) stores each chain from step 1 on its own, a link two chains share once, **so no schema change was needed**: a tahwil hadith simply has several step-1 rows. `GetIsnadTreeAsync` anchors on `SELECT DISTINCT` compiler (one root); `IlalAnalysisService.BuildPaths` follows every step-1 link and splits where a student has several next links (one `IlalChain` per branch, same hadith id, capped at 16); the findings list each hadith once.
  - Bench (31 books, `bench.py c1_final`, base `c3_g`): coverage and agreement within ±1 point everywhere; names past the current chain drop where flattening was worst (Muslim −140, Awanah −122, Kabir −87, Khuzayma −42, Daraqutni −35).
  - Review: two samples of 16 and 18 branched records (Muslim, Mustadrak, Kabir, Bayhaqi), all read by hand. The shared-tail forms are right; wrong joins did not occur (heads without a listed link stay partial). Known gaps: a head whose last narrator is unresolved cannot join (Tabarani 3733), «كلهم عن X بهذا الإسناد» tails are not followed, «وحدثنيه X» (Muslim) still keeps its verb in the name (`VERBS` lacks «حدثنيه»), and a record with more than 8 chains keeps 8.
  - **Scratch load** (`SmartHadithTree_ScratchC1`, `data/shamela_rijal_c1/`, both dropped afterwards): the registry with C2, all chains with C1/C3, loaded in 76 s: 1,217,978 transmissions (was 1,125,644), 115,816 relations (was 114,301), 19,982 hadiths with two or more step-1 rows, 0 orphans, 0 self-links, 0 duplicate rows. The 11-route page of the Mughira hadith: grade حسن, «لم يثبت اللقاء» 11 → 3, 12 chains for 11 hadiths.
  - **Found on the way:** the single-tree SQL did not select `TravelNote` (added to `IsnadNodeDto` in A6), so `/api/Tree/{id}` threw «The required column 'TravelNote' was not present» for every hadith. Fixed in the same query. The unit tests do not run that SQL; check `/api/Tree/<id>` after any change to `IsnadNodeDto`.
- **Kin words in isnads: «حدثني أبي» and «جده» (C3, 2026-10-05).** A bare first-person «أبي» («حدثني أبي», «سمعت أبي», «قال أبي») failed `is_name` and was dropped, so the chain showed a direct link that skipped the father (Muadh b. Hisham ← Qatada, without Hisham al-Dastuwa'i; about 570 first-person «أبي» in Ahmad, 610 in the Mustadrak, 230 in Muslim, 125 in Abu Dawud) and «جده» / «جدي» was never resolved (`LEADING` strips it). Now `chain_resolver.kin_form()` turns «أبي» / «أبى» / «والدي» / «والده» into «أبيه» and «جده» / «جدي» into «جده»; `resolve()` takes the father with `father_of()` and the grandfather with the new `grandfather_of()` (right after «أبيه» it is the father of that father, so «عمرو بن شعيب، عن أبيه، عن جده» works). Rules learned on the review samples:
  - A kin word with no narrator before it is skipped (`our_chain`): a 10-word first name had been dropped by `is_name`, and «أبيه» then meant the compiler's father.
  - A father or grandfather equal to a neighbour on the chain, or the narrator after a kin word equal to the one before it, is undecided (a wrongly resolved son gave «جدي» = the next narrator; Bakkar ← his father ← «أبي بكرة» came back to Bakkar).
  - In «عن أبيه، عن جده» for عمرو بن شعيب, «جده» is the Companion عبد الله بن عمرو (Tahdhib's reading; 37 chains), not Muhammad, Shu'ayb's father. This is a data decision in `chain_resolver.py`; other families get the plain father of the father.
  - «حدثنا عبد الله، حدثني أبي، حدثنا X» in Musnad Ahmad's Zawa'id (the compiler's son transmits, «أبي» is the compiler): the chain starts after the pair (`our_chain`, uses `openers`).
  - Result on the 31-book bench (`bench.py c3_g --books all --texts shamela`, base `c3_base`): coverage and agreement within ±1 point everywhere; +553 names gained (about 1,720 «أبيه» and 72 «جده» now resolved, 84 «جده» still undecided), 121 names changed. Review: 14 of 14 first-person fathers, 12 of 16 grandfathers on the first review (the misses led to the rules above; not re-sampled), 17 of 18 on a fresh seed, and 28 of 29 resolved→resolved changes are corrections (Hisham b. Urwa instead of Hisham b. Hassan, «عائشة» = bint Abi Bakr instead of bint 'Irar, «أبو عثمان» = al-Nahdi). Known miss: Ibn Khuzayma «عن أبي، عن أبي رافع» (the first «أبي» is cut off from its name).
  - An undecided father leaves a gap in the chain; the loader draws no link across it, where before it fabricated one (محمد بن عمرو ← حماد بن عمرو).
- **The compilers have almost no shaykh list (C2, 2026-10-05).** Tahdhib gives none for al-Nasa'i, Ibn Majah and al-Tirmidhi, and al-Mizzi writes them in the shaykhs' student lists as bare words («النسائي» 352×, «أبو داود» 346×, «البخاري» 258×, «ابن ماجه» 244×, «مسلم» 170×, «الترمذي» 167×, plus «الجماعة…» / «الأربعة»). The strict linker dropped them (a bare nisba matches several narrators and the compiler's entry cannot confirm it), so the registry gave al-Nasa'i 11 teachers, Ibn Majah 1 and al-Tirmidhi 1, and the Ilal rule «لم يثبت اللقاء» flagged every compiler link. `compiler_items.py` now maps these words (student lists only; «أبو داود» also needs the shaykh's symbol «د», because al-Tayalisi shares the name; items with a colon are critics' quotes, not relations). Teachers after the change: al-Nasa'i 433, Ibn Majah 303, al-Tirmidhi 213, Abu Dawud 422 (was 148), al-Bukhari 405 (was 130), Muslim 245 (was 215); +1,515 relations, none removed. Symbols in Tahdhib are joined without spaces («دس» = د + س), so test a letter inside a symbol, not whole tokens. The effect reaches the database only after the next full registry rebuild and ETL load. `HiddenInqitaRule` still skips the compiler's own link, since other compilers' lists may stay thin.
- **Check the `Database:` line of the ETL log before a load** (Phase 5.6): until 5.6 the ETL ignored `ConnectionStrings__DefaultConnection` and would have written to the live database. It now honours it; create a scratch database with `dotnet ef database update --project src/SmartHadithTree.Infrastructure --startup-project src/SmartHadithTree.Api --connection "<scratch connection string>"`.
- **New files under `src/SmartHadithTree.Infrastructure/Data/` need `git add -f`** (the `data/` ignore rule matches them).

- **Shell escaping:** in the Bash tool, `\\n`, `\b` and `\s` inside heredocs or `python -c` get mangled. Write scripts with the file-writing tool, not heredocs.
- **Arabic from SQL Server:** `sqlcmd` output loses Arabic. Use `export_chains.ps1` (System.Data.SqlClient → UTF-8 JSON). `pyodbc` is not installed.
- **SQL Server file access:** restores must target the instance data folder; the service cannot write to user temp folders.
- **Workflow:** while changing the resolver, `bench.py <tag>` (4 books, ~15 s with a warm cache) with a 10–15 item review; before a commit, `bench.py <tag> --books all --review 25`.
- **Measure the right thing:**
  - A higher "resolved" rate can hide wrong links. Always check agreement and review disagreements manually.
  - Numbers reported mid-way were corrected several times. For example, a 70.4% Bukhari figure turned out to include wrong shuhra matches.
- **Itqan's coverage of later narrators is broad:** it includes al-Hakim's and al-Tabarani's shaykhs. Do not assume that gap is unique to Shamela.
