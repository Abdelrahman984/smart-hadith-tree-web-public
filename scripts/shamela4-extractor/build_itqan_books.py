import sqlite3
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent

BOOKS = [
    ("musannaf_abdurrazzaq", 13174),
    ("musnad_tayalisi", 1456),
    ("musnad_shafii", 9344),
    ("musnad_humaydi", 8493),
    ("sunan_said_ibn_mansur", 13122),
    ("musnad_ishaq", 13159),
    ("musnad_bazzar", 12981),
    ("sunan_kubra_nasai", 8361),
    ("musnad_abi_yala", 12520),
    ("sahih_ibn_khuzaymah", 1446),
    ("mustakhraj_abi_awanah", 18144),
    ("sahih_ibn_hibban", 537),
    ("mujam_kabir_tabarani", 1733),
    ("mujam_awsat_tabarani", 28171),
    ("mujam_saghir_tabarani", 13068),
    ("sunan_daraqutni", 9771),
    ("mustadrak_hakim", 1424),
    ("sunan_kubra_bayhaqi", 148486),
    ("shuab_iman_bayhaqi", 10660),
]

DUMP_DIR = Path(sys.argv[1]) if len(sys.argv) > 1 else (REPO_ROOT / "data" / "shamela_dump")
SHAMELA_BOOK_DIR = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(r"D:\Islamic\shamela4\database\book")
OUT_SUNNI_DIR = REPO_ROOT / "data" / "itqan" / "sunni"

RE_TITLE_SPAN = re.compile(r"<span[^>]*data-type=['\"]title['\"][^>]*>.*?</span>", re.DOTALL)
RE_HTML_TAGS = re.compile(r"<[^>]+>")
RE_LEADING_NUM = re.compile(r"^\s*[\[\(]?[٠-٩0-9]+[\]\)]?\s*[-–—:ـ]+\s*")
RE_FOOTNOTE_MARK = re.compile(r"\(\s*¬[٠-٩0-9]+\s*\)")
RE_PAGE_BRACKET = re.compile(r"⦗[٠-٩0-9]+⦘")
RE_NEW_HADITH_START = re.compile(r"^(?:[\[\(]?[٠-٩0-9]+[\]\)]?\s*[-–—:ـ]+\s*)?(?:حَدَّثَنَا|حدثنا|أَخْبَرَنَا|أخبرنا|ثنا|ثَنَا|أنبأنا|حَدَّثَنِي|حدثني)\b")

def clean_hadith_text(raw: str) -> str:
    text = raw.replace("\\n", "\n")
    text = RE_TITLE_SPAN.sub("", text)
    text = RE_HTML_TAGS.sub("", text)
    text = RE_FOOTNOTE_MARK.sub("", text)
    text = RE_PAGE_BRACKET.sub("", text)
    text = text.strip()
    text = RE_LEADING_NUM.sub("", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text

def clean_title_text(raw: str) -> str:
    text = raw.replace("\\n", " ")
    text = RE_HTML_TAGS.sub("", text)
    return re.sub(r"\s+", " ", text).strip()

def load_tsv_map(path: Path) -> dict[int, str]:
    result: dict[int, str] = {}
    if not path.exists():
        return result
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\r\n")
            if not line:
                continue
            parts = line.split("\t", 1)
            if len(parts) != 2:
                continue
            key_str, body = parts
            if "-" in key_str:
                try:
                    item_id = int(key_str.split("-", 1)[1])
                    result[item_id] = body
                except ValueError:
                    pass
    return result

def main():
    total_hadiths_all = 0
    for slug, bid in BOOKS:
        pages_map = load_tsv_map(DUMP_DIR / f"{bid}_pages.tsv")
        titles_map = load_tsv_map(DUMP_DIR / f"{bid}_titles.tsv")

        db_path = SHAMELA_BOOK_DIR / f"{bid % 1000:03d}" / f"{bid}.db"
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()

        all_titles = cur.execute("SELECT id, page, parent FROM title ORDER BY page ASC, id ASC").fetchall()
        title_page_ids = {row[1] for row in all_titles}
        top_titles = [row for row in all_titles if row[2] == 0]
        if not top_titles:
            top_titles = all_titles

        chapters = []
        for idx, (tid, start_page, _) in enumerate(top_titles):
            end_page = top_titles[idx + 1][1] - 1 if idx + 1 < len(top_titles) else 999999999
            ch_name = clean_title_text(titles_map.get(tid, f"الباب {idx + 1}"))
            if not ch_name:
                ch_name = f"الباب {idx + 1}"
            chapters.append((tid, start_page, end_page, ch_name))

        if not chapters:
            chapters.append((1, 1, 999999999, "الكتاب الأول"))

        first_tid, first_start, first_end, first_name = chapters[0]
        if bid != 9344:
            chapters[0] = (first_tid, 1, first_end, first_name)

        all_pages = cur.execute("SELECT id, part, page, number FROM page ORDER BY id ASC").fetchall()
        numbered_count = sum(1 for r in all_pages if r[3] is not None)

        chapter_hadiths: list[list[dict]] = [[] for _ in range(len(chapters))]
        ch_idx = 0
        seq_id = 0

        for page_id, part, printed_page, num in all_pages:
            while ch_idx + 1 < len(chapters) and page_id >= chapters[ch_idx + 1][1]:
                ch_idx += 1

            raw_body = pages_map.get(page_id, "")
            if not raw_body:
                continue

            if numbered_count > 0:
                if num is not None:
                    cleaned = clean_hadith_text(raw_body)
                    if cleaned:
                        seq_id += 1
                        chapter_hadiths[ch_idx].append({
                            "id": seq_id,
                            "idInBook": int(num),
                            "hadithNumber": int(num),
                            "arabic": cleaned
                        })
                else:
                    if page_id not in title_page_ids:
                        stripped_raw = RE_HTML_TAGS.sub("", RE_TITLE_SPAN.sub("", raw_body)).strip()
                        cleaned_cont = clean_hadith_text(raw_body)
                        if not cleaned_cont:
                            continue
                        # If unnumbered page starts a new Isnad or previous record is already large, create a new entry
                        if not chapter_hadiths[ch_idx] or RE_NEW_HADITH_START.match(stripped_raw) or len(chapter_hadiths[ch_idx][-1]["arabic"]) > 4000:
                            seq_id += 1
                            last_num = chapter_hadiths[ch_idx][-1]["hadithNumber"] + 1 if chapter_hadiths[ch_idx] else seq_id
                            chapter_hadiths[ch_idx].append({
                                "id": seq_id,
                                "idInBook": last_num,
                                "hadithNumber": last_num,
                                "arabic": cleaned_cont
                            })
                        else:
                            chapter_hadiths[ch_idx][-1]["arabic"] += " " + cleaned_cont
            else:
                if page_id < first_start or page_id in title_page_ids:
                    continue
                cleaned = clean_hadith_text(raw_body)
                if cleaned:
                    seq_id += 1
                    chapter_hadiths[ch_idx].append({
                        "id": seq_id,
                        "idInBook": seq_id,
                        "hadithNumber": seq_id,
                        "arabic": cleaned
                    })

        conn.close()

        book_out_dir = OUT_SUNNI_DIR / slug
        book_out_dir.mkdir(parents=True, exist_ok=True)
        for old_file in book_out_dir.glob("*.json"):
            old_file.unlink()

        index_entries = []
        file_num = 0
        book_total = 0
        for idx, (_, start_p, end_p, ch_name) in enumerate(chapters):
            h_list = chapter_hadiths[idx]
            if not h_list:
                continue
            file_num += 1
            fname = f"{file_num}.json"
            with (book_out_dir / fname).open("w", encoding="utf-8") as f:
                json.dump(h_list, f, ensure_ascii=False, indent=2)
            index_entries.append({
                "chapter": file_num,
                "file": fname,
                "name_ar": ch_name,
                "name_en": "",
                "count": len(h_list)
            })
            book_total += len(h_list)

        with (book_out_dir / "index.json").open("w", encoding="utf-8") as f:
            json.dump(index_entries, f, ensure_ascii=False, indent=2)

        total_hadiths_all += book_total
        print(f"{slug:25s} (ID {bid:6d}): {file_num:4d} chapters, {book_total:6d} hadiths")

    print(f"\nTOTAL HADITHS EXPORTED ACROSS 19 BOOKS: {total_hadiths_all}")

if __name__ == "__main__":
    main()
