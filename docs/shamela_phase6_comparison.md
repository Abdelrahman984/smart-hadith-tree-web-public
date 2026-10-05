# Phase 6: `SmartHadithTree_Shamela` compared with v2

> Measured 2026-10-04/05 on branch `feature/shamela-rijal`. The live `SmartHadithTree` database was never written to.
> Scripts: `scripts/shamela4-extractor/rijal_pilot/phase6/` (`export_db_chains.ps1`, `compare_dbs.py`, `IlalCompare/`). Working data: `data/shamela_rijal/phase6/` (git-ignored).

## 1. What was built

`SmartHadithTree_Shamela`, created with `dotnet ef database update --connection …`, loaded with `dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal` and `seed-ilal-shamela` (101 s + 2 s). Counts match Phase 5.6 exactly: 23,502 narrators (7,882 ranked), 274,597 hadiths in 31 books, 1,125,644 transmissions, 116,670 quotes, 114,301 relations, 158 mudallisin, 131 mukhtalitun, 166 hearings, 75 group rules, 0 orphans, 0 self-links. `SmartHadithTree_Scratch` was dropped.

For the Ilal comparison the v2 backup was restored as `SmartHadithTree_V2Ilal` (the additive migration applied, the old Itqan seeds loaded with `seed-ilal`), because the live database carries **no** mudallis or mukhtalit flags today (checked: 0 and 0), so it would find nothing.

## 2. Hadiths

| | v2 | Shamela |
|---|---|---|
| Hadiths | 237,558 | 274,597 |
| Paired by text | 220,603 (92.9% of v2) | |

Shamela has more hadiths because records are cut at the edition's own numbers (Bayhaqi's Sunan al-Kubra 11,655 → 21,849, al-Bazzar 4,470 → 10,373, Muwatta 1,860 → 2,917). **No text is lost:** in a 1,500-record sample of each of Bayhaqi's Sunan al-Kubra, al-Mustadrak and Shu'ab al-Iman, 100% of the old records have ≥70% of their text in the new books. The unpaired v2 hadiths (7.1%, mostly Bayhaqi 32%, Ahmad 17%, Mustadrak 14%) are differently cut records, not missing ones.

## 3. Chains (paired hadiths, first four narrators after the compiler, position by position)

490,000 positions are the same person, 172,000 differ, 62,000 are in v2 only, 23,000 in Shamela only: **74% agreement**. v2 narrators with no registry mapping (52,000 positions) are not judged. By book: 86–90% for Abd al-Razzaq, the Muwatta, al-Humaydi, al-Shafi'i, Ishaq, Ibn Hibban and al-Tirmidhi; **37–56%** for Shu'ab al-Iman, Bayhaqi's Sunan al-Kubra and al-Mustadrak, where v2 reads another hadith's isnad (§6.1 of the migration doc).

**Who is right** (20 sampled disagreements read against the isnad text):

| Verdict | Count |
|---|---|
| Shamela right | 9 |
| v2 better (Shamela's chain lacks narrators) | 5 |
| Same person under two entries (not a disagreement) | 3 |
| Unclear | 2 |

- v2's errors are namesakes: Layth b. Abi Sulaim for Layth b. Sa'd, Asim b. Kulayb for Asim b. Damra, Yahya b. Sa'id al-Umawi for al-Qattan, Ibn Uyayna for al-Thawri; the second isnad of a tahwil appended to the first chain; «عن أبيه» not resolved; the compiler read as a narrator (Tayalisi, Sa'id b. Mansur).
- **Shamela's weakness is completeness.** A name the resolver left undecided leaves a gap, and the loader draws no link across it (Phase 5.3 decision 2), so the narrators on both sides disappear («إسماعيل → مالك» in Bukhari, «الفضل» in Ibn Abi Shayba). Short chains (one narrator or none): Ibn Abi Shayba 8% (v2 0%), the Muwatta 38% (v2 11%; many Muwatta records are «بلغني» sayings with no isnad, not verified). Chains are also shorter where v2 appended several isnads (Mustadrak 6.0 vs 7.6 narrators, Shu'ab 5.9 vs 7.5).

## 4. Ilal findings (the real `IlalAnalysisService`, 4,650 paired hadiths, 150 per book, each analysed alone)

| | v2 (16 mudallisin, 6 mukhtalitun seeds) | Shamela |
|---|---|---|
| Hadiths with a tadlis finding | 275 | **793** (190 shared; 603 new, 85 only in v2) |
| Hadiths with an ikhtilat finding | 0 | **341** (10 قادحة, 23 غير قادحة, 319 تنبيه) |

A sample of the new tadlis findings is correct under the rule (al-Zuhri, Qatada, Hajjaj b. Arta'a, Abu al-Zubayr narrating with «عن» and no explicit hearing in the collected turuq). The ikhtilat findings are mostly low-severity notes (Ibn Aqil, Asim b. Bahdala, Ibn Lahi'a), as designed in Phase 5.4. The 85 tadlis findings found only in v2 (31% of its 275) were not diagnosed: the sample shows Qatada and Abu al-Zubayr on links that Shamela's chain does not have, so the gap policy of §3 is the likely cause.

## 5. Grading

| | v2 | Shamela |
|---|---|---|
| Links whose sheikh has a grade | 98.7% (Itqan grade other than "unknown") | 85.5% ranked, 93.4% with a rank or a Taqrib verdict |
| Narrators with a grade | 84,059 of 115,735 (31,676 "unknown") | 7,882 ranked of 23,502 |
| Quotes | 106,164 summaries | 116,670 attributed quotes with book, volume and page |

Shamela's grades are Ibn Hajar's ranks and his own words; narrators absent from Taqrib (the compilers' shaykhs, later narrators) have no grade, about 7% of links. This is a regression in coverage and a gain in the quality of each grade. The narrator page is richer (kunya, death year, residence, biography, grade; v2's sample page had none of these).

## 6. The application

The API (`TreeController`, search, narrators, takhreej, Ilal) runs on both databases with the same latencies, all under 2 s warm (search about 2–6 s in both). Not done: the frontend in a browser, the AI summary (needs the Gemini key), and the 30-chain manual review of famous isnads.

## 7. What is lost or still tied to Itqan

- Itqan's English text and grades: dropped on purpose (Phase 4).
- `ItqanId`, `ItqanGrade`, `ContextualDisambiguator`, `ItqanDatasetParser`, `IlalSeedService` and the old seeds stay until Phase 7.
- Grades for narrators outside Taqrib (§5).

## 8. Recommendation

Switching improves identification (precision about 97% on a 500-name review; v2's namesake errors disappear), the Ilal findings (793 vs 275 tadlis, 341 vs 0 ikhtilat), the texts (record boundaries), and the narrator data. It costs completeness: chains with gaps, and no grade for about 7% of links. Neither blocks the switch, but the gap policy is the largest quality difference and is cheap to measure fixing (Phase 5.3 loader: add the most likely candidate of an undecided name, marked uncertain). **The decision to switch is the user's.** If it is yes: take a v3 backup of v2 first (`backups/README.md`), then point `appsettings.json` at the Shamela database.
