"""Measure isnad resolution on the 31 books and compare the run with an earlier one, compactly.

Runs compare_current.py for each book (in parallel), saves every run under
data/shamela_rijal/results/<tag>/<book>.json (SAVE= format) and prints:
- one line per book: our coverage, the current system's, agreement, names past the end of the
  current chain, each with the change since the base run;
- the names gained and lost since the base run (by isnad and name text), most frequent first;
- a random sample of new resolutions that disagree with the current system, for review.

Usage (from data/shamela_rijal):
  python ../../scripts/shamela4-extractor/rijal_pilot/bench.py <tag> [--books quick|all|b1,b2]
                                                             [--base <tag>] [--review N] [--texts shamela]
--books quick (default) = Bukhari, the Kabir, the Mustadrak, al-Daraqutni; --base defaults to the
most recent earlier run of the same books; --review defaults to 12. --texts shamela reads the Phase 4
texts (../shamela/<book>) instead of ../itqan/sunni/<book>, paired with the current chains through
maps/<book>.json (map_records.py, made when missing); its sample is other hadiths, so compare its
runs with each other, not with Itqan-text runs.
"""
import argparse
import json
import os
import random
import re
import subprocess
import sys
from collections import Counter
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
COMPARE = os.path.join(HERE, 'compare_current.py')
MAP_RECORDS = os.path.join(HERE, 'map_records.py')
RESULTS = 'results'
TEXTS = 'itqan'

# book -> (compiler's Tahdhib header prefix, current-system chains file, sample size)
BOOKS = {
    'bukhari': ('محمد بن إسماعيل بن إبراهيم بن المغيرة', 'current_chains.json', 500),
    'mustadrak_hakim': ('محمد بن عبد الله بن محمد بن حمدويه بن نعيم', 'current_chains.json', 1000),
    'mujam_kabir_tabarani': ('سليمان بن أحمد بن أيوب', 'current_chains_tabarani.json', 500),
    'mujam_awsat_tabarani': ('سليمان بن أحمد بن أيوب', 'current_chains_tabarani.json', 500),
    'mujam_saghir_tabarani': ('سليمان بن أحمد بن أيوب', 'current_chains_tabarani.json', 500),
    'sunan_kubra_bayhaqi': ('أحمد بن الحسين بن علي بن موسى', 'current_chains_bayhaqi.json', 500),
    'shuab_iman_bayhaqi': ('أحمد بن الحسين بن علي بن موسى', 'current_chains_bayhaqi.json', 500),
    'sahih_ibn_hibban': ('محمد بن حبان بن أحمد', 'current_chains_hibban.json', 500),
    'ahmed': ('أحمد بن محمد بن حنبل', 'current_chains_ahmad_malik.json', 500),
    'malik': ('مالك بن أنس بن مالك', 'current_chains_ahmad_malik.json', 500),
    'sunan_daraqutni': ('علي بن عمر بن أحمد بن مهدي', 'current_chains_daraqutni.json', 500),
    'sahih_ibn_khuzaymah': ('محمد بن إسحاق بن خزيمة', 'current_chains_khuzaymah_awanah.json', 500),
    'mustakhraj_abi_awanah': ('يعقوب بن إسحاق بن إبراهيم بن يزيد أبو عوانة', 'current_chains_khuzaymah_awanah.json', 500),
    'musannaf_abdurrazzaq': ('عبد الرزاق بن همام بن نافع', 'current_chains_early.json', 500),
    'musnad_tayalisi': ('سليمان بن داود بن الجارود', 'current_chains_early.json', 500),
    'musnad_shafii': ('محمد بن إدريس بن العباس', 'current_chains_early.json', 500),
    'musnad_humaydi': ('عبد الله بن الزبير بن عيسى', 'current_chains_early.json', 500),
    'sunan_said_ibn_mansur': ('سعيد بن منصور بن شعبة', 'current_chains_early.json', 500),
    'musnad_ishaq': ('إسحاق بن إبراهيم بن مخلد', 'current_chains_early.json', 500),
    'musnad_bazzar': ('أحمد بن عمرو بن عبد الخالق', 'current_chains_early.json', 500),
    'musnad_abi_yala': ('أحمد بن علي بن المثنى', 'current_chains_early.json', 500),
    'muslim': ('مسلم بن الحجاج بن مسلم', 'current_chains_muslim.json', 500),
    'abudawud': ('سليمان بن الأشعث بن شداد', 'current_chains_abudawud.json', 500),
    'tirmidhi': ('محمد بن عيسى بن سورة', 'current_chains_tirmidhi.json', 500),
    'nasai': ('أحمد بن شعيب بن علي', 'current_chains_nasai.json', 500),
    'ibnmajah': ('محمد بن يزيد الربعي', 'current_chains_ibnmajah.json', 500),
    'darimi': ('عبد الله بن عبد الرحمن بن الفضل', 'current_chains_darimi.json', 500),
    'aladab_almufrad': ('محمد بن إسماعيل بن إبراهيم بن المغيرة', 'current_chains_aladab_almufrad.json', 500),
    'shamail_muhammadiyah': ('محمد بن عيسى بن سورة', 'current_chains_shamail_muhammadiyah.json', 500),
    'musannaf_ibnabi_shaybah': ('عبد الله بن محمد بن إبراهيم بن عثمان', 'current_chains_musannaf_ibnabi_shaybah.json', 500),
    'sunan_kubra_nasai': ('أحمد بن شعيب بن علي', 'current_chains_sunan_kubra_nasai.json', 500),
}
QUICK = ['bukhari', 'mujam_kabir_tabarani', 'mustadrak_hakim', 'sunan_daraqutni']
STATS = ('ours', 'now', 'agree', 'past')


def run_book(tag: str, book: str) -> dict:
    compiler, current, n = BOOKS[book]
    out = os.path.join(RESULTS, tag, f'{book}.json')
    env = dict(os.environ, SAVE=out, PYTHONIOENCODING='utf-8')
    book_dir = f'../itqan/sunni/{book}'
    if TEXTS == 'shamela':
        book_dir, mp = f'../shamela/{book}', os.path.join('maps', f'{book}.json')
        if not os.path.exists(mp):
            os.makedirs('maps', exist_ok=True)
            subprocess.run([sys.executable, MAP_RECORDS, book_dir, current, mp], capture_output=True, env=env)
        env['CURRENT_MAP'] = mp
    p = subprocess.run([sys.executable, COMPARE, 'tahdhib.json', book_dir, current, compiler,
                        str(n), '0'], capture_output=True, text=True, encoding='utf-8', env=env)
    t = p.stdout
    ours = re.search(r'ours\s+: resolved \d+ \((\d+)%\)', t)
    now = re.search(r'current: links\s+\d+ \((\d+)%\)', t)
    agr = re.search(r'\((\d+)% agreement\); (\d+) past', t)
    if p.returncode or not (ours and now and agr):
        return {'error': (p.stderr or t)[-600:]}
    return {'ours': int(ours.group(1)), 'now': int(now.group(1)), 'agree': int(agr.group(1)),
            'past': int(agr.group(2))}


def runs_with(books: list[str]) -> list[str]:
    """Earlier tags whose summary covers all `books`, oldest first."""
    found = []
    for tag in os.listdir(RESULTS) if os.path.isdir(RESULTS) else ():
        s = os.path.join(RESULTS, tag, 'summary.json')
        if os.path.exists(s) and set(books) <= set(json.load(open(s, encoding='utf-8'))['books']):
            found.append((os.path.getmtime(s), tag))
    return [t for _, t in sorted(found)]


def load(tag: str, book: str) -> list:
    return json.load(open(os.path.join(RESULTS, tag, f'{book}.json'), encoding='utf-8'))


def keyed(run: list) -> dict:
    """(isnad, its occurrence in the sample, name, its occurrence in the isnad) -> (resolution, isnad):
    a name can come twice in one isnad («شعبة» on both sides of a tahwil), and an isnad twice in
    a sample, so text alone does not identify a name across runs."""
    out, seen_isnad = {}, Counter()
    for h in run:
        seen_isnad[h['isnad']] += 1
        seen_name = Counter()
        for o in h['ours']:
            seen_name[o['name']] += 1
            out[h['isnad'], seen_isnad[h['isnad']], o['name'], seen_name[o['name']]] = (o, h)
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument('tag')
    ap.add_argument('--books', default='quick')
    ap.add_argument('--base')
    ap.add_argument('--review', type=int, default=12)
    ap.add_argument('--texts', choices=('itqan', 'shamela'), default='itqan')
    a = ap.parse_args()
    global TEXTS
    TEXTS = a.texts
    books = QUICK if a.books == 'quick' else list(BOOKS) if a.books == 'all' else a.books.split(',')
    base = a.base or next((t for t in reversed(runs_with(books)) if t != a.tag), None)
    os.makedirs(os.path.join(RESULTS, a.tag), exist_ok=True)

    # The first book runs alone: after any change to the data or the scripts it rebuilds the cache
    # (link_tahdhib.cached), which the others then load instead of each rebuilding it in parallel.
    stats = {books[0]: run_book(a.tag, books[0])}
    with ThreadPoolExecutor(max_workers=5) as pool:
        stats.update(zip(books[1:], pool.map(lambda b: run_book(a.tag, b), books[1:])))
    old = json.load(open(os.path.join(RESULTS, base, 'summary.json'), encoding='utf-8'))['stats'] if base else {}
    json.dump({'books': books, 'stats': stats}, open(os.path.join(RESULTS, a.tag, 'summary.json'), 'w',
                                                    encoding='utf-8'), ensure_ascii=False, indent=1)

    print(f'run {a.tag}' + (f'  (vs {base})' if base else ''))
    for b in books:
        s = stats[b]
        if 'error' in s:
            print(f'  {b:24} ERROR {s["error"]}')
            continue
        o = old.get(b, {})

        def cell(k: str) -> str:
            unit = '' if k == 'past' else '%'
            d = s[k] - o[k] if k in o else None
            return f'{k} {s[k]}{unit}' + (f' ({d:+d})' if d else '')
        print(f'  {b:24} ' + ' | '.join(cell(k) for k in STATS))

    if not base:
        return
    gained, lost, new_differ = Counter(), Counter(), []
    for b in books:
        if 'error' in stats[b] or not os.path.exists(os.path.join(RESULTS, base, f'{b}.json')):
            continue
        before, after = keyed(load(base, b)), keyed(load(a.tag, b))
        for k, (o, h) in after.items():
            p = before.get(k, (None,))[0]
            if o['narrator'] and not (p and p['narrator']):
                gained[(o['name'][:28], o['narrator'][:40])] += 1
                if o.get('vs_current') == 'differ':
                    new_differ.append((b, o['name'][:28], o['narrator'][:50], o['how'],
                                       ' ← '.join(c[:20] for c in h['current'])[:140]))
            elif p and p['narrator'] and o['narrator'] != p['narrator']:
                lost[(o['name'][:28], p['narrator'][:40] + ' → ' + (o['narrator'] or 'none')[:30])] += 1
        # Resolved before, and the same name is gone from the segments now (text cleaned differently).
        for k, (p, h) in before.items():
            if p['narrator'] and k not in after:
                lost[(p['name'][:28], p['narrator'][:40] + ' → (segment changed)')] += 1
    print(f'gained {sum(gained.values())}:', '; '.join(f'{n}×{k[0]}' for k, n in gained.most_common(10)))
    print(f'lost/changed {sum(lost.values())}:', '; '.join(f'{n}×{k[0]} [{k[1]}]' for k, n in lost.most_common(10)))
    random.seed(1)
    picked = random.sample(new_differ, min(a.review, len(new_differ)))
    print(f'new disagreements with the current system: {len(new_differ)} (sample {len(picked)}):')
    for b, name, narrator, how, cur in picked:
        print(f'  [{b[:8]} {how}] {name} → {narrator}\n      now: {cur}')


if __name__ == '__main__':
    main()
