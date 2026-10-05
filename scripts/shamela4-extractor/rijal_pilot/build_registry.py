"""Merge Tahdhib al-Kamal + Taqrib al-Tahdhib into one narrator registry.

Each narrator gets Ibn Hajr's verdict and its rank (1-12, as defined in the Taqrib
introduction), which is the same T1-T12 scale used by NarratorGradeScale.
Usage: python build_registry.py tahdhib.json taqrib.json align.json out.json
pipeline.py imports RANKS / rank() from here.
"""
import json
import re
import sys
from collections import Counter


# Ibn Hajr's twelve ranks (Taqrib, introduction). Checked from the most specific pattern down.
RANKS = [
    (12, r'كذاب|وضاع|يضع|كذبه|كذبوه'),
    (11, r'متهم'),
    (10, r'متروك|واهي|ساقط|منكر الحديث جدا|هالك'),
    (1, r'صحابي|صحابية|له صحبة|لها صحبة|له رؤية|لها رؤية|أم المؤمنين'),
    (2, r'ثقة ثقة|ثقة ثبت|ثقة حافظ|ثقة حجة|ثقة متقن|حافظ حجة|إمام|أمير المؤمنين|أحد الأئمة|أحد الأعلام'
        r'|متفق على (?:جلالته|إتقانه)|^الحافظ|^الفقيه الحافظ'),
    (5, r'صدوق\S* .*(?:يهم|يخطئ|أوهام|سيئ الحفظ|سيء الحفظ|تغير|اختلط|خلط|وهم|يدلس|في حفظه|يغرب'
        r'|بالتشيع|بالقدر|بالإرجاء|بالنصب|بدعة|شيعي|ناصبي)'),
    (3, r'^(?:ثقة|ثبت|متقن|عدل|حافظ)|وثقه'),
    (4, r'صدوق|لا بأس به|ليس به بأس'),
    (6, r'مقبول'),
    (7, r'مستور|مجهول الحال'),
    (8, r'لين|ضعيف|فيه ضعف|منكر الحديث|ليس بالقوي'),
    (9, r'مجهول|لا يعرف|لا تعرف|لا يدرى|غير مشهور'),
]
RANK_LABEL = {1: 'صحابي', 2: 'ثقة ثبت', 3: 'ثقة', 4: 'صدوق', 5: 'صدوق يهم', 6: 'مقبول', 7: 'مستور',
              8: 'ضعيف', 9: 'مجهول', 10: 'متروك', 11: 'متهم', 12: 'كذاب'}


def rank(verdict: str | None) -> int | None:
    if not verdict:
        return None
    v = re.sub(r'[ً-ْٰـ]', '', verdict).strip()
    v = re.sub(r'(?<!\S)ثقه(?!\S)', 'ثقة', v)          # the edition sometimes spells ثقة with ه
    v = re.sub(r'^مخضرم\s*', '', v)                     # "مخضرم ثقة": a generation, then the grade
    for r, pattern in RANKS:
        if re.search(pattern, v):
            return r
    return None




def main() -> None:
    TAHDHIB, TAQRIB, ALIGN, OUT = sys.argv[1:5]
    tahdhib = [e for e in json.load(open(TAHDHIB, encoding='utf-8')) if e['kind'] == 'entry']
    taqrib = json.load(open(TAQRIB, encoding='utf-8'))['entries']
    by_tahdhib = {p['tahdhib']: p['taqrib'] for p in json.load(open(ALIGN, encoding='utf-8'))}
    registry = []
    for i, e in enumerate(tahdhib):
        t = taqrib[by_tahdhib[i]] if i in by_tahdhib else None
        verdict = t['grade'] if t else None
        registry.append({
            'id': f'tk{e["num"]}' + ('s' if e['num_suspect'] else ''),
            'tahdhib_num': e['num'], 'taqrib_num': t['num'] if t else None,
            'name': e['name'], 'header': e['header'], 'symbols': e['symbols'],
            'verdict': verdict, 'rank': rank(verdict),
            'tabaqa': t['tabaqa'] if t else None, 'death': t['death'] if t else None,
            'shuyukh': e['shuyukh'], 'talamidh': e['talamidh'], 'quotes': e['quotes'],
        })

    ranked = [r for r in registry if r['rank']]
    print(f'narrators: {len(registry)}  with Taqrib verdict: {sum(1 for r in registry if r["verdict"])}  '
          f'with rank: {len(ranked)} ({len(ranked) / len(registry):.1%})')
    for r, n in sorted(Counter(r['rank'] for r in ranked).items()):
        print(f'   T{r:<2} {RANK_LABEL[r]:10} {n:5}')
    unranked = Counter(r['verdict'].split()[0] for r in registry if r['verdict'] and not r['rank'])
    print('verdicts without a rank (first word):', unranked.most_common(12))
    json.dump(registry, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False)


if __name__ == '__main__':
    main()
