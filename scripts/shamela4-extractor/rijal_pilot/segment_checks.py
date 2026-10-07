"""Fast checks of the isnad segmenter in gap_test.py (no registry needed): run `python segment_checks.py`.

gap_test.py loads the whole registry when imported, so only its segmentation block (from the VERBS definition to the
`is_name` helper) is executed here. Each case is a text from the books and the narrator names that must come out.
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "gap_test.py"), encoding="utf-8").read()
block = src[src.index("# Later books abbreviate"):src.index("# A segment that is not shaped like a name")]
ns = {"re": re, "norm": lambda s: s, "isms": set()}
exec(block, ns)
segments = ns["chain_segments"]

CASES = [
    ("a verb after the name: «X يحدث عن Y»",
     "حدثنا محمد بن جعفر، حدثنا شعبة، قال: سمعت قتادة يحدث عن عطاء بن أبي رباح، عن جابر بن عبد الله، عن النبي ﷺ أنه قال",
     ["محمد بن جعفر", "شعبة", "قتادة", "عطاء بن أبي رباح", "جابر بن عبد الله"]),
    ("brackets restore a narrator: Bayhaqi",
     "أخبرنا أبو علي الروذباري، أخبرنا أبو بكر ابن داسة، [حدثنا أبو داود، حدثنا] ابن كثير، حدثنا همام، عن علي بن زيد، عن أم محمد، عن عائشة، أن النبي ﷺ",
     ["أبو علي الروذباري", "أبو بكر ابن داسة", "أبو داود", "ابن كثير", "همام", "علي بن زيد", "أم محمد", "عائشة"]),
    ("parenthesis restores a narrator: Ibn Abi Shayba 37",
     "حدثنا عفان قال: حدثنا أبان العطار عن يحيى بن أبي كثير عن زيد (عن أبي سلام) عن أبي مالك الأشعري أن رسول الله",
     ["عفان", "أبان العطار", "يحيى بن أبي كثير", "زيد", "أبي سلام", "أبي مالك الأشعري"]),
    ("a parenthesis inside a name",
     "حدثنا وكيع عن عبد الله بن (عامر) عن أبيه أن النبي ﷺ",
     ["وكيع", "عبد الله بن عامر", "أبيه"]),
    ("a footnote mark is not an insertion",
     "حدثنا ابن أبي أنيس (1) ، عن أبيه، عن النبي ﷺ",
     ["ابن أبي أنيس", "أبيه"]),
    ("«ز-» is a mark, not a narrator",
     "ز- حدثنا محمد بن يحيى، حدثنا مسلم، عن أبان، عن النبي ﷺ",
     ["محمد بن يحيى", "مسلم", "أبان"]),
    ("reading to the shaykh: the Muwatta",
     "قرأت على مالك، عن نافع، عن ابن عمر قال: قال رسول الله ﷺ",
     ["مالك", "نافع", "ابن عمر"]),
    ("reading to the shaykh, «وأنا أسمع»: al-Hakim",
     "أخبرنا أحمد بن سلمان الفقيه ببغداد قال قرئ على عبد الملك بن محمد، وأنا أسمع، حدثنا سهل بن حماد وأبو ربيعة قالا: حدثنا حماد بن سلمة، عن سعيد الجريري. قال رسول الله ﷺ",
     ["أحمد بن سلمان الفقيه", "عبد الملك بن محمد", "سهل بن حماد", "حماد بن سلمة", "سعيد الجريري"]),
    ("«النبى» with a final alef maqsura ends the isnad",
     "حدثنا فلان، عن أبي هريرة، عن النبى أنه قال: لا يقبل الله صلاة بغير طهور",
     ["فلان", "أبي هريرة"]),
    ("«أنه» is not part of the name",
     "حدثنا مالك، عن نافع، أنه رأى صفية بنت أبي عبيد",
     ["مالك", "نافع"]),
    ("«أنه» before a verb: «عن عطاء أنه قال»",
     "حدثنا وكيع، عن سفيان، عن عطاء أنه قال: سمعت ابن عباس يقول كذا",
     ["وكيع", "سفيان", "عطاء"]),
    ("the Muwatta's «أنه بلغه» ends the isnad and is not a narrator",
     "حدثني يحيى عن مالك أنه بلغه أن رسول الله ﷺ قال كذا",
     ["يحيى", "مالك"]),
    ("«بلغه عن X» still names the narrator",
     "حدثنا يحيى عن مالك أنه بلغه عن سعيد بن المسيب أنه قال كذا",
     ["يحيى", "مالك", "سعيد بن المسيب"]),
    ("verbs with a final alef maqsura: Bayhaqi 15101 («أخبرنى محمد بن إبراهيم»)",
     "أخبرنا أبو عبد الله الحافظ، أخبرنا أبو بكر أحمد بن الحسن الفقيه، حدثنا يزيد بن هارون، أخبرنا يحيى بن سعيد، أخبرنى محمد بن إبراهيم التيمى أنه سمع علقمة بن وقاص الليثى يقول: سمعت عمر بن الخطاب رضي الله عنه قال: قال رسول الله ﷺ",
     ["يزيد بن هارون", "يحيى بن سعيد", "محمد بن إبراهيم التيمى", "علقمة بن وقاص الليثى", "عمر بن الخطاب"]),
    ("«بلغني» too",
     "حدثنا يحيى عن مالك أنه بلغني أن عمر قال كذا",
     ["يحيى", "مالك"]),
]

failed = 0
for title, text, expected in CASES:
    got = segments(text)
    # the expected names must appear in order; extra trailing words (a matn that is not the isnad) are ignored
    it = iter(got)
    ok = all(any(name == g for g in it) for name in expected) and not any(
        g in ("ز-", "أنه", "بلغه", "بلغني") or g.startswith(("[", "(")) or g.endswith(("أنه", "يحدث")) for g in got)
    print(("ok   " if ok else "FAIL ") + title)
    if not ok:
        failed += 1
        print("     expected:", expected)
        print("     got     :", got)
sys.exit(1 if failed else 0)
