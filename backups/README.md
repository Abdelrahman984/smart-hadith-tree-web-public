# Database Backups

Full SQL Server backups of the `SmartHadithTree` database live in this folder (git-ignored, local only).
Each file is a numbered version so later data migrations (e.g. the move from Itqan to Shamela 4) can always be compared against, or rolled back to, a known state.

| Version | File | Taken | Size | Compression / Checksum | SHA-256 |
|---|---|---|---|---|---|
| v1 | `SmartHadithTree_v1_2026-10-02.bak` | 2026-10-02 21:24 | 1,390 MB | No / No | `74353394703e7c896ec48c140df766b199cc1598667a6afe7eb8767bebb62945` |
| v2 | `SmartHadithTree_v2_2026-10-03.bak` | 2026-10-03 17:34 | 466 MB | Yes / Yes | `ba808ddd30021a56e162593f29a0363a5e9131d6f5f1c8b82d99b334463f61c2` |
| v3 | `SmartHadithTree_v3_2026-10-05.bak` | 2026-10-05 | 466 MB | Yes / Yes | `cb5fcf8de255a72b73d710b464ac3d41fa0ee5008b80d3b8809659801e3507a8` |
| v4 | `SmartHadithTree_Shamela_v4_2026-10-05.bak` | 2026-10-05 | 462 MB | Yes / Yes | `e5ba630986a76a1d4e73a846ac77ba738e53898fe417b1694adf3d8cbe494295` |
| v5 | `SmartHadithTree_Shamela_v5_2026-10-05.bak` | 2026-10-05 | 474 MB | Yes / Yes | `63413b6727c72133ff0e0dd4aad2678da475ca111636a7e6b956c96e0b308394` |
| v6 | `SmartHadithTree_Shamela_v6_2026-10-07.bak` | 2026-10-07 | 493 MB | Yes / Yes | `c43c10c777edaf326dbea266d40b654b80300c11aae4ef0e7ce78ab9e68af13b` |
| v7 | `SmartHadithTree_Shamela_v7_2026-10-07.bak` | 2026-10-07 | 497 MB | Yes / Yes | `5a738bb4005c87b78cff87e203c9a3a4475e5c79061c8b056c7abd9b8d83792a` |

Both files pass `RESTORE VERIFYONLY` (checked 2026-10-03).
The counts below were taken by restoring each backup and querying it directly.

---

## v1 — 31 books, first full ingestion

Previously named `SmartHadithTree_31Books_Full.bak`. Taken right after the 19 Shamela 4 books were ingested alongside the 12 Itqan books (see `docs/changelog.md`, "Database Backup").

| Table | Rows |
|---|---|
| Hadiths | 233,224 |
| Transmissions | 1,078,668 |
| Narrators | 115,735 |
| ScholarEvaluations | 106,164 |
| NarratorRelations / MukhtalitHearings | 0 / 0 |

- **Schema:** 9 EF migrations, last is `20261002140819_AddNarratorGeographyAndStats`. Missing `OptimizeSearchCollationAndIndexes`; the API applies it automatically on startup.
- **Sources:** hadith texts from Itqan (12 books) + Shamela 4 (19 books); all narrator profiles and Jarh wa Ta'deel from Itqan rijal.
- **Git state:** up to commit `88a5a0b` (2026-10-02 21:50).

## v2 — 31 books, reprocessed chains (pre-Shamela-migration baseline)

Current database as of 2026-10-03. This is the **baseline for the planned migration away from Itqan** to Shamela 4 as the sole source.

| Table | Rows | Change vs v1 |
|---|---|---|
| Hadiths | 237,558 | +4,334 |
| Transmissions | 1,110,677 | +32,009 |
| Narrators | 115,735 | — |
| ScholarEvaluations | 106,164 | — |
| NarratorRelations / MukhtalitHearings | 0 / 0 | — |

- **Schema:** 10 EF migrations, last is `20261002192731_OptimizeSearchCollationAndIndexes`.
- **Git state:** up to commit `a49b7af` (2026-10-03 16:05).
- **What changed since v1:**
  - `mujam_kabir_tabarani` grew from 14,549 to **18,883** hadiths (the whole `+4,334`).
  - Isnad chains were re-built with `reprocess-chains` after the narrator-segment cleaning and `ContextualDisambiguator` changes (`5b07f2d`, `3327aef`, `2bfc6a1`). Transmission counts moved in most books; the largest shifts:

    | Book | v1 | v2 |
    |---|---|---|
    | `mujam_kabir_tabarani` | 55,290 | 108,186 |
    | `mujam_awsat_tabarani` | 31,359 | 51,488 |
    | `sunan_daraqutni` | 12,435 | 26,042 |
    | `sunan_kubra_bayhaqi` | 126,322 | 84,516 |
    | `shuab_iman_bayhaqi` | 60,899 | 42,015 |
    | `mustadrak_hakim` | 51,401 | 39,660 |

## Known gaps (both versions)

- `seed-ilal` has not been run: `NarratorRelations`, `MukhtalitHearings` are empty and no narrator is flagged as mudallis/mukhtalit. Run `dotnet run --project src/SmartHadithTree.Etl -- seed-ilal data/itqan` after restoring if the Ilal rules need them.

---

## Restore

Replace the file name with the version you want:

```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SmartHadithTree] FROM DISK = N'backups\SmartHadithTree_v2_2026-10-03.bak' WITH REPLACE, STATS = 25;"
```

To inspect an old version without touching the live database, restore it under another name with `MOVE` (files must go to a folder the SQL Server service can write to, e.g. its default data path):

```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SHT_v1_inspect] FROM DISK = N'backups\SmartHadithTree_v1_2026-10-02.bak' WITH MOVE 'SmartHadithTree' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SHT_v1_inspect.mdf', MOVE 'SmartHadithTree_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SHT_v1_inspect_log.ldf';"
```

## Taking a new version

Use the next number and today's date, then add a section to this file:

```powershell
sqlcmd -S . -Q "BACKUP DATABASE [SmartHadithTree] TO DISK = N'backups\SmartHadithTree_v3_YYYY-MM-DD.bak' WITH INIT, FORMAT, COMPRESSION, CHECKSUM, NAME = N'SmartHadithTree v3 - <description>', STATS = 25; RESTORE VERIFYONLY FROM DISK = N'backups\SmartHadithTree_v3_YYYY-MM-DD.bak' WITH CHECKSUM;"
```

---

## v3: the v2 data, taken just before the switch to Shamela

Copy-only backup of the live `SmartHadithTree` on 2026-10-05, right before the app was pointed at `SmartHadithTree_Shamela` (`docs/shamela_phase6_comparison.md`). The data is the same as v2 (237,558 hadiths, 1,110,677 transmissions, 115,735 narrators); `RESTORE VERIFYONLY ... WITH CHECKSUM` passed. **To roll back:** restore this file as `SmartHadithTree` and set `ConnectionStrings:DefaultConnection` back to `Database=SmartHadithTree` in `src/SmartHadithTree.Api/appsettings.json` and `src/SmartHadithTree.Etl/appsettings.json`.

---

## v4: the Shamela-built database (before tahwil and the compiler links)

Copy-only backup of `SmartHadithTree_Shamela` after Phase 7: 23,502 narrators, 274,597 hadiths, 1,125,644 transmissions, 116,670 quotes, 114,301 relations, 158 mudallisin, 131 mukhtalitun. Restore it as `SmartHadithTree_Shamela` (the connection strings of the Api and the Etl name that database).

## v5: v4 plus the compiler links, kin words and tahwil chains (the current one)

Copy-only backup of `SmartHadithTree_ShamelaV5`, a fresh database loaded from a rebuilt registry and chains (`docs/shamela_migration.md` §9: C1 tahwil, C2 compiler links, C3 «أبي» / «جده»). Same narrators, hadiths, quotes and Ilal data as v4; the differences are the chains and the relations:

| Table | v4 | v5 |
|---|---|---|
| Hadiths | 274,597 | 274,597 |
| Narrators | 23,502 | 23,502 |
| Transmissions | 1,125,644 | **1,217,978** (+92,334: tahwil chains, father / grandfather links) |
| NarratorRelations | 114,301 | **115,816** (+1,515: al-Nasa'i, Ibn Majah, al-Tirmidhi and the other compilers' teachers) |
| ScholarEvaluations | 116,670 | 116,670 |
| Mudallisin / Mukhtalitun | 158 / 131 | 158 / 131 |
| MukhtalitHearings / GroupRules | 166 / 75 | 166 / 75 |

19,982 hadiths now have two or more chains (two or more rows at step 1); 0 orphan transmissions, 0 self-links; `RESTORE VERIFYONLY ... WITH CHECKSUM` passes; 11 migrations. Restore it as `SmartHadithTree_Shamela` (what the connection strings of the Api and the Etl name) or under any name, and point `ConnectionStrings:DefaultConnection` at it. The registry, chains and `ilal.json` it was built from are in `data/shamela_rijal/` (git-ignored; the v4 copies are `registry_v4.json`, `ilal_v4.json` and `chains_v4/`).

## v6 — resolver improvements (database `SmartHadithTree_ShamelaV6`)

A fresh database built from the improved resolver (`docs/changelog.md`, 2026-10-06/07): the isnad segmenter, the same-person merge, Lisan al-Mizan, the Siyar and the Thiqat of Ibn Qutlubugha as fallback registries, and the pair merge. Every hadith Guid differs from v5, so saved `/takhreej?ids=` and `/tree/<id>` links of v5 do not work on it. Not yet the default: the Api and Etl `appsettings.json` still point at V5.

| Table | v5 | v6 |
|---|---|---|
| Hadiths | 274,597 | 274,597 |
| Transmissions | 1,217,978 | 1,262,178 |
| Narrators | 23,502 | 44,282 |
| Mudallisin / mukhtalit hearings | 158 / 166 | 165 / 168 |

Isnad gaps (an undecided name inside a chain, branch by branch, 295,952 branches): 108,066 in v5, 94,032 in v6 (14,661 closed, 1,624 opened, 27,578 names gained, 694 lost, 3,996 narrators changed). The nine Ilal pages: findings 127 -> 121, matn Qadihah 11 -> 10, every grade unchanged (`docs/challenge/evaluation/ilal-pages-v6-2026-10-07.json`).

## v7 — verbs with a final alef maqsura (database `SmartHadithTree_ShamelaV7`, the current one)

Download: [Google Drive](https://drive.google.com/file/d/19oYawbGHpfwR9ZhwsnsFSGPFGINo88cF/view?usp=sharing). The file is 521,072,640 bytes (497 MB) and its SHA-256 matches the table above; the Api and Etl `appsettings.json` and `docker-compose.yml` point at this database.

The v6 build with one resolver fix: al-Bayhaqi writes «أخبرنى / حدثنى» with ى, which the segmenter did not know, so a narrator after such a verb was lost («أخبرنى محمد بن إبراهيم التيمي» in Sunan al-Kubra 15101). Chains re-exported for all 31 books (the registry is unchanged); 3,512 of 21,849 Bayhaqi records changed, 30 other books by 1 to 14. Every hadith Guid differs from v6.

| Table | v6 | v7 |
|---|---|---|
| Hadiths | 274,597 | 274,597 |
| Transmissions | 1,262,178 | 1,266,022 |
| Narrators | 44,282 | 44,282 |
| Mudallisin / mukhtalit hearings | 165 / 168 | 165 / 168 |

The nine Ilal pages: findings 121 -> 119, Qadihah 10 -> 9, every grade unchanged (`docs/challenge/evaluation/ilal-pages-v7-2026-10-07.json`).
