"""Phase 5.2: resolve EVERY record of the 31 Shamela books and write one chains file per book.

The C# ETL (option B) only loads these files; the resolver stays in Python. Each book gives
data/shamela_rijal/chains/<slug>.json:

  {"book": slug, "compiler": <registry id of the compiler or null>, "records": N,
   "chains": [{"id": <record id>, "number": <edition number or null>,
               "names": [{"n": <name as segmented>, "id": <registry id or null>, "how": ..., "verbs": [...],
                          "ties": [<registry ids>] (only when undecided)}, ...]}, ...]}

`names` is the first chain of the record, the first 8 names (14 when the isnad has several chains), after the
compiler's own name (the Muwatta's «يحيى» loop, §6.2): the same chain the bench measures. The chain's first link
is compiler <- names[0]. When the isnad has several chains (tahwil: «ح», «قالا», tahwil.py) the record also has
"branches": [names, names, ...], every chain from the compiler upward, the first one included.
Records of kind "text" (the compiler's prose) have no chain and are left out.

In the 7 books whose Shamela edition links its narrators (`narrators` spans), an undecided name whose
aligned Shamela narrator maps (EXACT map only, links/map_s1.py) to one of its tied candidates takes that
candidate (how = "shamela_link"; user decision 2026-10-04).

Usage (any dir; the Shamela texts, tahdhib.json and the cache must exist):
  python export_chains_shamela.py [--books all|b1,b2] [--limit N] [--workers 4] [--no-link]
--limit N exports only the first N records of each book (a quick check).
"""
import argparse
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RIJAL = os.path.join(ROOT, 'data', 'shamela_rijal')
SHAMELA = os.path.join(ROOT, 'data', 'shamela')
OUT = os.environ.get('CHAINS_OUT') or os.path.join(RIJAL, 'chains')   # CHAINS_OUT: a scratch folder for checks
LINKED = ['bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'malik', 'musnad_tayalisi']


def book_records(slug: str) -> list[dict]:
    d = os.path.join(SHAMELA, slug)
    recs = []
    for f in os.listdir(d):
        if f.endswith('.json') and f not in ('book.json', 'index.json'):
            recs += json.load(open(os.path.join(d, f), encoding='utf-8'))
    return sorted((r for r in recs if r.get('arabic') and r.get('kind') != 'text'), key=lambda r: r['id'])


def worker(slug: str, limit: int | None, link: bool) -> None:
    """Resolve one book. Runs in its own process, from data/shamela_rijal, like compare_current.py."""
    sys.path.insert(0, HERE)
    sys.path.insert(0, os.path.join(HERE, 'links'))
    from bench import BOOKS
    compiler_prefix, _, _ = BOOKS[slug]
    os.makedirs(OUT, exist_ok=True)
    empty = os.path.join(OUT, '_empty.json')
    if not os.path.exists(empty):
        open(empty, 'w').write('[]')
    # Everything compare_current.py defines before its sampling loop (our_chain, the registry, the resolver),
    # executed in this module: same code as the bench, so the chains are the ones the bench measures.
    sys.argv = [os.path.join(HERE, 'compare_current.py'), 'tahdhib.json', os.path.join(SHAMELA, slug), empty,
                compiler_prefix, '1000000', '0']
    src = open(os.path.join(HERE, 'compare_current.py'), encoding='utf-8').read().split('matched, rows = 0, []')[0]
    g = globals()
    g['__file__'] = os.path.join(HERE, 'compare_current.py')
    exec(compile(src, 'compare_current.py', 'exec'), g)
    from pipeline import registry_ids
    n_tahdhib = sum(1 for e in json.load(open('tahdhib.json', encoding='utf-8')) if e.get('kind') == 'entry')
    reg_id = registry_ids(g['entries'], n_tahdhib)
    our_chain = g['our_chain']

    smap, align, span_tokens = {}, None, None
    if link and slug in LINKED:
        import eval_links
        align, span_tokens = eval_links.align, eval_links.span_tokens
        smap = json.load(open(os.path.join(ROOT, 'data', 'shamela_rijal', 'review', 'links', 's1_registry_map.json'),
                              encoding='utf-8'))

    recs = book_records(slug)[:limit]
    chains, stats = [], {'names': 0, 'resolved': 0, 'ties': 0, 'shamela_link': 0, 'branched': 0}
    t0 = time.time()
    def name_dicts(chain, verbs, ties_at):
        names = []
        for pos, (seg, j, how) in enumerate(chain):
            ties = [reg_id[x] for x in ties_at.get(pos, [])]
            nm = {'n': seg, 'id': reg_id[j] if j is not None else None, 'how': how,
                  'verbs': verbs[pos] if pos < len(verbs) else []}
            if j is None and ties:
                nm['ties'] = ties
            names.append(nm)
        return names

    for r in recs:
        text = r['arabic']
        branches = g['our_branches'](text)
        names = name_dicts(branches[0]['chain'], branches[0]['verbs'], branches[0]['ties'])
        others = [name_dicts(b['chain'], b['verbs'], b['ties']) for b in branches[1:]]
        if smap and any(nm['id'] is None and nm.get('ties') for nm in names):
            spans = sorted(r.get('narrators') or [], key=lambda s: s['start'])
            for s in spans:
                s['toks'] = span_tokens(text[s['start']:s['end']])
            for nm, k in zip(names, align([nm['n'] for nm in names], spans)):
                if k is None or nm['id'] is not None or not nm.get('ties'):
                    continue
                m = smap.get(str(spans[k]['man']))
                if m and m['confidence'] == 'exact' and m['registry'] in nm['ties']:
                    nm['id'], nm['how'] = m['registry'], 'shamela_link'
                    nm['ties'] = None
                    stats['shamela_link'] += 1
        stats['names'] += len(names)
        stats['resolved'] += sum(nm['id'] is not None for nm in names)
        stats['ties'] += sum(bool(nm.get('ties')) for nm in names)
        for nm in names + [x for b in others for x in b]:
            if not nm.get('ties'):
                nm.pop('ties', None)
        chain = {'id': r['id'], 'number': r.get('number'), 'names': names}
        if others:
            stats['branched'] += 1
            # Every chain of a tahwil isnad, the first one included, each from the compiler upward.
            chain['branches'] = [names] + others
        chains.append(chain)
    out = {'book': slug, 'compiler': reg_id[g['compiler']] if g['compiler'] is not None else None,
           'records': len(chains), 'chains': chains}
    with open(os.path.join(OUT, f'{slug}.json'), 'w', encoding='utf-8') as f:
        json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
    print(json.dumps({'book': slug, 'records': len(chains), 'seconds': round(time.time() - t0), **stats,
                      'coverage': round(stats['resolved'] / max(1, stats['names']), 3)}, ensure_ascii=False))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument('--books', default='all')
    ap.add_argument('--limit', type=int)
    ap.add_argument('--workers', type=int, default=4)
    ap.add_argument('--no-link', action='store_true', help="do not use Shamela's narrator links for ties")
    ap.add_argument('--worker', help=argparse.SUPPRESS)
    a = ap.parse_args()
    if a.worker:
        os.chdir(RIJAL)
        worker(a.worker, a.limit, not a.no_link)
        return
    sys.path.insert(0, HERE)
    from bench import BOOKS
    books = list(BOOKS) if a.books == 'all' else a.books.split(',')

    def run(slug: str) -> str:
        cmd = [sys.executable, os.path.abspath(__file__), '--worker', slug]
        if a.limit:
            cmd += ['--limit', str(a.limit)]
        if a.no_link:
            cmd.append('--no-link')
        p = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8',
                           env=dict(os.environ, PYTHONIOENCODING='utf-8'))
        return p.stdout.strip().splitlines()[-1] if not p.returncode else f'{slug} ERROR {p.stderr[-600:]}'

    # The first book alone: it rebuilds the registry cache (link_tahdhib.cached) the others then load.
    print(run(books[0]), flush=True)
    with ThreadPoolExecutor(max_workers=a.workers) as pool:
        for line in pool.map(run, books[1:]):
            print(line, flush=True)


if __name__ == '__main__':
    main()
