# Data Ingestion (ETL) & Corpus Architecture

The Smart Hadith Tree loads classical Hadith and Rijal data into SQL Server with the C# console application `src/SmartHadithTree.Etl`. **Everything comes from a local Shamela 4 installation** (`D:\Islamic\shamela4`); the Itqan dataset is no longer used. The full story of the migration, the decisions, the measurements and the pitfalls is in [`shamela_migration.md`](shamela_migration.md); the comparison with the old database is in [`shamela_phase6_comparison.md`](shamela_phase6_comparison.md).

## Current Corpus (database `SmartHadithTree_Shamela`)
- **Collections:** `31` canonical Sunni Hadith works
- **Hadiths:** `274,597` (each edition's own numbering; records cut at the edition's in-text hadith numbers)
- **Isnad links (`Transmissions`):** `1,125,644` directed Sheikh → Student links
- **Narrators:** `23,502` (a registry built from Tahdhib al-Kamal, Taqrib al-Tahdhib and the compilers' shaykh books), `7,882` with Ibn Hajar's rank
- **Jarh wa Ta'deel:** `116,670` attributed quotes (critic, book, volume, page)
- **Teacher / student relations:** `114,301`
- **Ilal data:** `158` mudallisin in Ibn Hajar's tiers, `131` mukhtalitun with the books' severity and `166` hearings

## 1. Pipeline overview

```
Shamela 4 (SQLite + Lucene)
  └─ ShamelaLuceneDumper.java ──► data/shamela_rijal/dump, data/shamela/dump   (raw pages / titles / footnotes)
        ├─ build_shamela_books.py + split_parts.py ─► data/shamela/<slug>/       (31 books: records, isnad / matn parts)
        └─ rijal_pilot/pipeline.py ─► data/shamela_rijal/registry.json, ilal.json
              └─ export_chains_shamela.py ─► data/shamela_rijal/chains/<slug>.json   (every isnad resolved to registry ids)
                    └─ dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal   ─► SQL Server
```

The resolver (name matching, isnad segmentation, joint chain resolution) is **Python** (`scripts/shamela4-extractor/rijal_pilot/`); the C# ETL only loads its output. A C# port is Phase 5b of the migration plan, not done.

## 2. How Shamela 4 storage is read (`scripts/shamela4-extractor/`)

Shamela 4 splits storage into:
1. **SQLite structural databases (`database/book/<id%1000, 3 digits>/<id>.db`)**:
   - `page (id, part, page, number, services)` maps internal `page.id` to printed volume / page and the canonical hadith `number`.
   - `title (id, page, parent)` maps the chapter hierarchy to starting `page.id`.
2. **Apache Lucene 10.4.0 stores (`database/store/page`, `database/store/title`)** hold the Arabic text (`body`, `foot`).

Dump books with Shamela's bundled JRE (book IDs as the third argument):
```powershell
javac -encoding UTF-8 -d data\shamela_rijal\dumper scripts\shamela4-extractor\ShamelaLuceneDumper.java
& "D:\Islamic\shamela4\app\win\64\jre\2\bin\java.exe" --add-modules=jdk.incubator.vector `
  -cp "D:\Islamic\shamela4\app\lucene\2\*;data\shamela_rijal\dumper" ShamelaLuceneDumper `
  "D:\Islamic\shamela4\database\store" data\shamela_rijal\dump 3722,8609
```

## 3. Rebuilding everything

The exact commands, in order, with the options and the pitfalls, are in §8 and §9 of [`shamela_migration.md`](shamela_migration.md). In short, from the repo root:

```powershell
python scripts/shamela4-extractor/build_shamela_books.py                      # data/shamela/<slug>/
python scripts/shamela4-extractor/split_parts.py --write data/shamela          # isnad / matn parts
cd data/shamela_rijal
python ../../scripts/shamela4-extractor/rijal_pilot/pipeline.py dump .         # registry.json, ilal.json
cd ../..
python scripts/shamela4-extractor/rijal_pilot/export_chains_shamela.py --books all    # chains/<slug>.json, about 10 minutes
```

## 4. Loading the database

Check the `Database:` line in the ETL log before every load.

```powershell
dotnet ef database update --project src/SmartHadithTree.Infrastructure --startup-project src/SmartHadithTree.Api
dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal             # narrators, hadiths, transmissions, quotes, relations
dotnet run --project src/SmartHadithTree.Etl -- seed-ilal-shamela data/shamela_rijal   # mudallisin, mukhtalitun, hearings, group rules
```

To load into another database (for example a scratch one), set `ConnectionStrings__DefaultConnection` in the environment first. The loader needs about 1 GB of memory and takes about 2 minutes.

## 5. Database backups and instant restore

Backups are listed in [`../backups/README.md`](../backups/README.md). The current one is `backups/SmartHadithTree_Shamela_v4_2026-10-05.bak` (~462 MB, compressed, checksummed). To restore it:
```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SmartHadithTree_Shamela] FROM DISK = N'backups\SmartHadithTree_Shamela_v4_2026-10-05.bak' WITH REPLACE, STATS = 25;"
```
`SmartHadithTree_v2_2026-10-03.bak` and `SmartHadithTree_v3_2026-10-05.bak` hold the old Itqan-built database.
