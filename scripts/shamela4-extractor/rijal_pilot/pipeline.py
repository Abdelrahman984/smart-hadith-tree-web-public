"""One reproducible pipeline for the hadith narrator registry.

Usage: python pipeline.py <dump_dir> <out_dir> [--scripts DIR] [--from STEP]

Steps (1-5 are subprocesses, 6 links in-process, then the registry is written):
  1 parse_tahdhib  2 parse_taqrib  3 align_taqrib  4 parse_lisan  5 parse_shaykh_books
  6 link (link_tahdhib + gap_test + chain_resolver, loaded as compare_current.py does)
    and write registry.json / registry_stats.json.
  7 ilal: parse_mudallisin (طبقات المدلسين, 1186) and link_ilal (mudallisin and the curated
    ilal_data/mukhtalitun.json to registry ids, with ilal_overrides.json) → ilal.json.
--from N starts at step N (1-7) and reuses the earlier outputs in out_dir.
Set PYTHONIOENCODING=utf-8. First draft by a Sonnet sub-agent, reviewed.

List links in registry.json are strict: a list name links to a narrator only when it has one candidate
(after the book symbols), or exactly one candidate lists the narrator back. The resolver's shuyukh_of /
talamidh_of keep every candidate of an ambiguous name (Sufyan b. Uyayna had 200 shaykhs that way, with
«أمي بن ربيعة الصيرفي» among them); here the rest stay in *_raw only.
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_registry import rank  # noqa: E402  Ibn Hajar's twelve ranks
from compiler_items import carries_symbol, compiler_entries, compilers_named  # noqa: E402


COMPANION = re.compile(r'صحابي|صحابية|له صحبة|لها صحبة|أم المؤمنين')
PROPHET = re.compile(r'\s*(?:النبي|رسول الله)')


def unique_ids(ids):
    """Second and later duplicates get -2, -3, ... in order (never colliding with an existing id)."""
    seen, out, used = Counter(), [], set(ids)
    for i in ids:
        seen[i] += 1
        n = seen[i]
        if n == 1:
            out.append(i)
            continue
        cand = f'{i}-{n}'
        while cand in used:
            n += 1
            cand = f'{i}-{n}'
        used.add(cand)
        out.append(cand)
    return out


def registry_ids(entries, n_tahdhib):
    """Stable ids: tk<num> (s when the number is suspect) for the first n_tahdhib entries, <source>:<num> after."""
    return unique_ids([f'tk{e["num"]}' + ('s' if e['num_suspect'] else '') if i < n_tahdhib
                       else f'{(e.get("source") or "extra").split("+")[0]}:{e["num"]}' for i, e in enumerate(entries)])


def load_linker(scripts, out_dir, book_dir):
    """Exec link_tahdhib.py, gap_test.py (up to 'MAX_DEPTH = 8') and chain_resolver.py like compare_current.py."""
    sys.argv = ['compare_current.py', os.path.join(out_dir, 'tahdhib.json'), book_dir, '1']
    ns = {'__name__': '__linker__', '__file__': os.path.join(scripts, 'compare_current.py'), 'os': os}
    src = open(os.path.join(scripts, 'gap_test.py'), encoding='utf-8').read()
    src = src.replace("exec(open(__file__.replace('gap_test.py', 'link_tahdhib.py')",
                      "exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'link_tahdhib.py')")
    exec(compile(src.split('MAX_DEPTH = 8')[0], 'gap_test.py', 'exec'), ns)
    exec(compile(open(os.path.join(scripts, 'chain_resolver.py'), encoding='utf-8').read(), 'chain_resolver.py', 'exec'), ns)
    return ns


def link_compilers(entries, n_tahdhib, ids, reg):
    """Add the compilers named by a bare word in a student list («النسائي», «ابن ماجه», «الجماعة») to that
    narrator's strict `talamidh` (compiler_items.py; «أبو داود» also needs the shaykh's symbol «د», because
    al-Tayalisi has the same name). Returns the number of links added, by compiler."""
    compilers = compiler_entries(entries, n_tahdhib)
    added = Counter()
    for i in range(n_tahdhib):
        for it in entries[i]['talamidh']:
            for key in compilers_named(it['name']):
                c = compilers.get(key)
                if c is None or c == i or ids[c] in reg[i]['talamidh']:
                    continue
                if key == 'abudawud' and not carries_symbol(entries[i]['symbols'], key):
                    continue
                reg[i]['talamidh'].append(ids[c])
                added[key] += 1
        reg[i]['talamidh'].sort()
    return dict(added)


def build_registry(ns, out_dir):
    entries, kunyas = ns['entries'], ns['kunyas']
    shuyukh_of, talamidh_of = ns['shuyukh_of'], ns['talamidh_of']
    tahdhib = [e for e in json.load(open(os.path.join(out_dir, 'tahdhib.json'), encoding='utf-8')) if e['kind'] == 'entry']
    n_tahdhib = len(tahdhib)
    taqrib = json.load(open(os.path.join(out_dir, 'taqrib.json'), encoding='utf-8'))['entries']
    by_tahdhib = {p['tahdhib']: p['taqrib'] for p in json.load(open(os.path.join(out_dir, 'align.json'), encoding='utf-8'))}

    sources = ['tahdhib' if i < n_tahdhib else (e.get('source') or 'extra') for i, e in enumerate(entries)]
    ids = registry_ids(entries, n_tahdhib)

    candidates, symbol_filter = ns['candidates'], ns['symbol_filter']

    def strict(i: int, items: list, back_of: list) -> set:
        """Each item's narrator when certain: one candidate, or the one candidate listing i back."""
        out = set()
        for it in items:
            c = set(symbol_filter(set(candidates(it['name'])) - {i}, it['symbols']))
            if len(c) > 1:
                c = {j for j in c if i in back_of[j]}
            if len(c) == 1:
                out |= c
        return out

    reg = []
    for i, e in enumerate(entries):
        src = sources[i]
        verdict = rk = tabaqa = death = None
        if src == 'tahdhib':
            t = taqrib[by_tahdhib[i]] if i in by_tahdhib else None
            if t:
                verdict, tabaqa, death = t['grade'], t['tabaqa'], t['death']
            rk = rank(verdict)
            # Ibn Hajar does not grade Companions and gives them no tabaqa ("عائشة ... أفقه النساء", "أبو
            # هريرة الصحابي الجليل حافظ الصحابة" read as rank 2): rank 1 when Taqrib or the first sentence
            # of Tahdhib says so, or when Taqrib has no verdict and the shaykh list opens with the Prophet
            # (not "مرسل"). A Taqrib verdict on a disputed Companion («مقبول») is kept.
            # With a Taqrib verdict only Taqrib's own words count: Tahdhib's first sentence also reports
            # disputes («أحزاب بن أسيد», Ibn Hajar: "مخضرم ثقة").
            first = re.split(r'\.\s|\n', e['header'])[0] if rk is None else ''
            if COMPANION.search((t['raw'][:200] if t else '') + ' ' + first) or (
                    rk is None and any(PROPHET.match(x['name']) and 'مرسل' not in x['name'] + ' ' + x.get('note', '')
                                       for x in e['shuyukh'][:3])):
                rk = 1
        else:
            verdict = e.get('verdict')
        header = e['header']
        reg.append({
            'id': ids[i], 'name': re.split(r'[،.]', header, maxsplit=1)[0].strip(), 'header': header,
            'source': src, 'symbols': e['symbols'],
            'verdict': verdict, 'rank': rk, 'tabaqa': tabaqa, 'death': death,
            'kunyas': sorted(kunyas[i]), 'aliases': e.get('aliases') or [], 'merged_ids': e.get('merged_ids') or [],
            'shuyukh': sorted(ids[j] for j in strict(i, e['shuyukh'], talamidh_of)),
            'talamidh': sorted(ids[j] for j in strict(i, e['talamidh'], shuyukh_of)),
            'shuyukh_raw': e['shuyukh'], 'talamidh_raw': e['talamidh'], 'quotes': e['quotes'],
        })
    compiler_links = link_compilers(entries, n_tahdhib, ids, reg)
    json.dump(reg, open(os.path.join(out_dir, 'registry.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    n = len(reg)
    avg = lambda f: round(sum(len(r[f]) for r in reg) / n, 3)
    stats = {
        'entries': n,
        'by_source': dict(sorted(Counter(r['source'] for r in reg).items())),
        'by_first_source': dict(sorted(Counter(r['source'].split('+')[0] for r in reg).items())),
        'with_verdict': sum(1 for r in reg if r['verdict']),
        'with_rank': sum(1 for r in reg if r['rank']),
        'with_shuyukh_resolved': sum(1 for r in reg if r['shuyukh']),
        'with_talamidh_resolved': sum(1 for r in reg if r['talamidh']),
        'avg_shuyukh_resolved': avg('shuyukh'), 'avg_talamidh_resolved': avg('talamidh'),
        'avg_shuyukh_raw': avg('shuyukh_raw'), 'avg_talamidh_raw': avg('talamidh_raw'),
        'compiler_links': compiler_links,
    }
    json.dump(stats, open(os.path.join(out_dir, 'registry_stats.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    return stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('dump_dir')
    ap.add_argument('out_dir')
    ap.add_argument('--scripts', default=os.path.dirname(os.path.abspath(__file__)))
    ap.add_argument('--from', dest='start', type=int, default=1, choices=range(1, 8))
    a = ap.parse_args()
    scripts, dump, out = (os.path.abspath(p) for p in (a.scripts, a.dump_dir, a.out_dir))
    os.makedirs(out, exist_ok=True)
    env = dict(os.environ, PYTHONIOENCODING='utf-8')
    py = lambda s: os.path.join(scripts, s)
    steps = [
        ('parse_tahdhib', [py('parse_tahdhib.py'), dump, 'tahdhib.json']),
        ('parse_taqrib', [py('parse_taqrib.py'), dump, 'taqrib.json']),
        ('align_taqrib', [py('align_taqrib.py'), 'tahdhib.json', 'taqrib.json', 'align.json']),
        ('parse_lisan', [py('parse_lisan.py'), dump, 'lisan.json']),
        ('parse_shaykh_books', [py('parse_shaykh_books.py'), dump, 'tahdhib.json', 'extra_shaykh_books.json']),
    ]
    def run(k: int, name: str, args: list[str]):
        t = time.time()
        r = subprocess.run([sys.executable] + args, cwd=out, env=env, capture_output=True, text=True,
                           encoding='utf-8', errors='replace')
        if r.returncode:
            print(f'step {k} {name}: FAILED ({r.returncode})\n{r.stdout[-2000:]}\n{r.stderr[-3000:]}')
            sys.exit(1)
        print(f'step {k} {name}: {time.time() - t:.1f}s', flush=True)
        return r.stdout

    for k, (name, args) in enumerate(steps, 1):
        if k >= a.start:
            run(k, name, args)
    if a.start <= 6:
        t = time.time()
        # gap_test.py wants a book folder and a sample; any book works, only the registry is used here.
        book = os.path.normpath(os.path.join(scripts, '..', '..', '..', 'data', 'itqan', 'sunni', 'bukhari'))
        ns = load_linker(scripts, out, book)
        print(f'step 6 link: {time.time() - t:.1f}s', flush=True)
        t = time.time()
        stats = build_registry(ns, out)
        print(f'registry: {time.time() - t:.1f}s  {json.dumps(stats, ensure_ascii=False)}')
    run(7, 'parse_mudallisin', [py('parse_mudallisin.py'), dump, 'mudallisin.json'])
    print(run(7, 'link_ilal', [py('link_ilal.py'), out]).strip().splitlines()[-4:])


if __name__ == '__main__':
    main()
