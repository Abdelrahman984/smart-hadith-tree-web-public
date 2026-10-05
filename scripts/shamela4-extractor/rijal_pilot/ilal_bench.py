"""Phase 3: what the Ilal data finds on a saved bench run, against the app's hand-written seeds.

Usage: python ilal_bench.py <run_dir> <ilal.json> [--show N]
  <run_dir> is a bench.py run (data/shamela_rijal/results/<tag>/, SAVE= format with ids and verbs).

Mirrors the app's rules (TadlisRule, IkhtilatRule):
- tadlis: a narrator of tier 3+ narrates from the next, resolved narrator with an ambiguous formula
  (عن / أن / قال / ذكر) and no explicit one (حدثنا / أخبرنا / سمعت ...) between the two names.
- ikhtilat: a narrator narrates from a mukhtalit; by the hearing table, before / after / both / unknown.
- hidden: an undecided name whose tied candidates include a narrator who would give a finding there
  (a tier-3+ mudallis with an ambiguous formula, or a mukhtalit), the condition for fixing the
  deferred ties in Phase 3 (docs/shamela_migration.md, Phase 2).
"""
import json
import os
import random
import sys
from collections import Counter, defaultdict

RUN, ILAL, *rest = sys.argv[1:]
SHOW = int(rest[rest.index('--show') + 1]) if '--show' in rest else 0
MIN_TIER = 3

EXPLICIT = {'حدثنا', 'حدثني', 'حدثه', 'حدثناه', 'أخبرنا', 'أخبرني', 'أخبره', 'أخبرناه', 'أنبأنا', 'أنبأني',
            'أنبأ', 'أنبأناه', 'سمعت', 'سمع', 'ثنا', 'نا', 'أنا', 'أبنا'}
AMBIGUOUS = {'عن', 'أن', 'قال', 'قالت', 'يقول', 'ذكر'}


def formula(verbs: list[str]) -> str:
    if any(v in EXPLICIT for v in verbs):
        return 'explicit'
    return 'ambiguous' if any(v in AMBIGUOUS for v in verbs) else 'none'


ilal = json.load(open(ILAL, encoding='utf-8'))


def lists(prefix: str):
    mud = {m['id']: m for m in ilal.get(f'{prefix}mudallisin', []) if m.get('id') and (m.get('tier') or 0) >= MIN_TIER}
    mukh = {m['id']: m for m in ilal.get(f'{prefix}mukhtalitun', []) if m.get('id')}
    hear = {(m['id'], h['id']): h['timing'] for m in mukh.values() for h in m.get('hearings', []) if h.get('id')}
    return mud, mukh, hear


LISTS = {'new': lists(''), 'seed': lists('seed_')}


def severity(m: dict) -> str:
    """The books' own weight of the ikhtilat: harmful, light or disputed (any book says so), or unstated."""
    values = {s['value'] for s in m.get('severity') or [] if isinstance(s, dict) and s.get('value')}
    if values & {'light', 'disputed'}:
        return 'light/disputed'
    return 'harmful' if 'harmful' in values else 'unstated'


per_book = defaultdict(Counter)
examples = defaultdict(list)
hidden = Counter()
hidden_kind = Counter()
hidden_names = Counter()
by_severity = Counter()
by_mukhtalit = Counter()
for f in sorted(os.listdir(RUN)):
    if not f.endswith('.json') or f == 'summary.json':
        continue
    book = f[:-5]
    for h in json.load(open(os.path.join(RUN, f), encoding='utf-8')):
        ours = h['ours']
        per_book[book]['isnads'] += 1
        for i, o in enumerate(ours):
            nxt = ours[i + 1] if i + 1 < len(ours) else None
            form = formula(o.get('verbs', []))
            for tag, (mud, mukh, hear) in LISTS.items():
                if o['id'] in mud and form == 'ambiguous' and nxt and nxt['id']:
                    per_book[book][f'{tag} tadlis'] += 1
                    if tag == 'new' and o['id'] not in LISTS['seed'][0]:
                        examples['tadlis'].append((book, o['name'], mud[o['id']]['tier'], o['verbs'], nxt['name'], h['isnad'][:120]))
                if nxt and nxt['id'] in mukh and o['id']:
                    timing = hear.get((nxt['id'], o['id']), 'unknown')
                    per_book[book][f'{tag} ikhtilat'] += 1
                    per_book[book][f'{tag} ikhtilat {timing}'] += 1
                    if tag == 'new':
                        by_severity[severity(mukh[nxt['id']]), timing] += 1
                        by_mukhtalit[mukh[nxt['id']]['name'][:40], severity(mukh[nxt['id']])] += 1
                    if tag == 'new' and nxt['id'] not in LISTS['seed'][1]:
                        examples['ikhtilat'].append((book, o['name'], nxt['name'], timing, h['isnad'][:120]))
            # Ties that may hide a finding (new lists only).
            mud, mukh, _ = LISTS['new']
            if o.get('ties'):
                prev = ours[i - 1] if i else None
                kinds = {'tadlis' for t in o['ties'] if t in mud and form == 'ambiguous' and nxt}
                kinds |= {'ikhtilat' for t in o['ties'] if t in mukh and prev}
                if kinds:
                    hidden[book] += 1
                    hidden_kind.update(kinds)
                    hidden_names[o['name'], tuple(sorted(t for t in o['ties'] if t in mud or t in mukh))] += 1

tot = Counter()
print(f'{"book":24} {"isnads":>6} | tadlis new/seed | ikhtilat new/seed | new ikhtilat: before after both unknown | hidden')
for book, c in per_book.items():
    tot.update(c)
    print(f'{book:24} {c["isnads"]:6} | {c["new tadlis"]:6} {c["seed tadlis"]:6} | {c["new ikhtilat"]:6} {c["seed ikhtilat"]:6} | '
          f'{c["new ikhtilat before"]:6} {c["new ikhtilat after"]:5} {c["new ikhtilat both"]:4} {c["new ikhtilat unknown"]:7} | {hidden[book]:4}')
tot_hidden = sum(hidden.values())
print(f'{"TOTAL":24} {tot["isnads"]:6} | {tot["new tadlis"]:6} {tot["seed tadlis"]:6} | {tot["new ikhtilat"]:6} {tot["seed ikhtilat"]:6} | '
      f'{tot["new ikhtilat before"]:6} {tot["new ikhtilat after"]:5} {tot["new ikhtilat both"]:4} {tot["new ikhtilat unknown"]:7} | {tot_hidden:4}')
print('\nnew ikhtilat findings by the books\' severity and the hearing table:')
for sev in ('harmful', 'unstated', 'light/disputed'):
    row = {t: by_severity[sev, t] for t in ('before', 'after', 'both', 'conflict', 'unknown')}
    print(f'  {sev:15} {sum(row.values()):6}  ' + '  '.join(f'{t} {n}' for t, n in row.items()))
print('  most frequent mukhtalitun:', ', '.join(f'{n} {name} ({s})' for (name, s), n in by_mukhtalit.most_common(10)))
findings = tot['new tadlis'] + tot['new ikhtilat']
print(f'\nhidden by ties: {tot_hidden} of {findings} findings ({tot_hidden / max(1, findings):.2%}); threshold 1%; '
      f'by kind: tadlis {hidden_kind["tadlis"]} of {tot["new tadlis"]} ({hidden_kind["tadlis"] / max(1, tot["new tadlis"]):.2%}), '
      f'ikhtilat {hidden_kind["ikhtilat"]} of {tot["new ikhtilat"]} ({hidden_kind["ikhtilat"] / max(1, tot["new ikhtilat"]):.2%})')
print('top tied names hiding a finding:')
for (name, hides), n in hidden_names.most_common(12):
    print(f'  {n:4}  {name}  {hides}')
random.seed(3)
for kind, ex in examples.items():
    print(f'\n{kind}: {len(ex)} findings from narrators not in the seeds; sample:')
    for e in random.sample(ex, min(SHOW, len(ex))):
        print('  ', e)
