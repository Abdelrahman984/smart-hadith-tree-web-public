"""Map Shamela's narrator encyclopedia (S1 ids) to registry ids from INDEPENDENT evidence only (draft).

Evidence per (S1 narrator, registry entry):
  page   : the S1 quotes from Tahdhib al-Kamal (vol/page) / Taqrib (vol/page), turned into the entries printed on that page
  name   : S1 name / full_name versus the entry's header name, its «وقيل» forms and its Taqrib name (ism + nasab chain)
  death  : S1 death year versus Taqrib's «مات سنة ...» (year mod 100)
  tabaqa : S1 «طبقة رواة التقريب» versus the entry's Taqrib tabaqa
  verdict: S1 «الرتبة عند ابن حجر» versus the entry's Taqrib verdict
Never uses our resolutions (no circularity). Output: data/shamela_rijal/review/links/s1_registry_map.json
Usage (any dir): python map_s1.py [--only-used]
"""
import bisect
import collections
import sys

from s1_common import *
from s1_pages import entry_pages

A = str.maketrans('0123456789', '٠١٢٣٤٥٦٧٨٩')
TABAQA_ORD = {'الاولي': 1, 'الثانيه': 2, 'الثالثه': 3, 'الرابعه': 4, 'الخامسه': 5, 'السادسه': 6, 'السابعه': 7, 'الثامنه': 8,
              'التاسعه': 9, 'العاشره': 10, 'الحاديه عشره': 11, 'الثانيه عشره': 12}


def chain(s: str):
    """Name phrase -> list of segments (token tuples) split at بن / ابن."""
    segs, cur = [], []
    for w in nrm(s).split():
        if w in ('بن', 'ابن'):
            if cur:
                segs.append(tuple(cur))
            cur = []
        else:
            cur.append(w)
    if cur:
        segs.append(tuple(cur))
    return segs


def first_phrase(header: str) -> str:
    h = re.sub(r'\([^)]*\)', ' ', header)
    return re.split(r'[،\n.]|\s-\s', h, maxsplit=1)[0]


def alt_phrases(header: str):
    h = re.sub(r'\([^)]*\)', ' ', header)
    out = [first_phrase(header)]
    for m in re.finditer(r'(?:وقيل|ويقال|يقال|وقيل له|واسمه|اسمه)\s*:?\s*([^،\n.]{3,80})', h):
        out.append(m.group(1))
    return out[:5]


def tabaqa_key(s):
    if not s:
        return None
    s = nrm(re.sub(r'[.\d]', '', s)).strip()
    s = re.sub(r'^(?:من\s+)?(?:كبار|صغار|اوسط|اواسط)\s+', '', s)
    s = re.sub(r'\s+', ' ', s).strip()
    return TABAQA_ORD.get(s)


def verdict_key(s):
    if not s:
        return None
    s = re.sub(r'[,،.;:]+', ' ', HARAKAT.sub('', s))
    s = re.sub('[ئىي]', 'ي', s).replace('ء', '').replace('ة', 'ه')
    s = re.sub(r'[أإآ]', 'ا', s)
    return re.sub(r'\s+', ' ', s).strip()


def rel_score(s1chains, hchains):
    """(score, label) of the best S1-chain / registry-chain pair."""
    best = (-4.0, 'none')
    for sc in s1chains:
        k = len(sc)
        if not k:
            continue
        for hc, w in hchains:
            if not hc:
                continue
            m = 0
            for a, b in zip(sc, hc):
                if b[:len(a)] == a:
                    m += 1
                else:
                    break
            if m == k:
                r = (5.0, 'full_eq') if len(hc) == k else ((3.5, 'full_prefix') if k >= 2 else (1.5, 'single_prefix'))
            elif m == len(hc):
                r = (2.5, 'header_prefix') if len(hc) >= 2 else (0.5, 'ism')
            elif m >= 2:
                r = (1.5, 'ism_father')
            elif m == 1:
                r = (0.5, 'ism')
            else:
                r = (-4.0, 'none')
            r = (r[0] * w if r[0] > 0 else r[0], r[1])
            if r[0] > best[0]:
                best = r
    return best


def main():
    entries, nt, ids, taq, align = load_registry()
    only_used = '--only-used' in sys.argv or True
    # Taqrib per Tahdhib entry
    T = entries[:nt]
    tq = {i: taq[align[i]] for i in range(nt) if i in align}
    # --- page indexes: Tahdhib al-Kamal (3722) and Taqrib (8609) ---
    nums = [str(e['num']).translate(A) for e in T]
    heads = [HARAKAT.sub('', e['header'][:10]) for e in T]
    tpid, tpp, tmiss = entry_pages(3722, '722/3722.db', nums, heads, r'١\s*-[^\n]{0,60}أحمد بن إبراهيم بن خالد')
    qnums = [str(e['num']).translate(A) for e in taq]
    qheads = [HARAKAT.sub('', e['name'][:10]) for e in taq]
    qpid, qpp, qmiss = entry_pages(8609, '609/8609.db', qnums, qheads, r'١\s*-?[^\n]{0,30}أحمد ابن إبراهيم')
    print('page index: tahdhib miss', tmiss, 'of', nt, '; taqrib miss', qmiss, 'of', len(taq), file=sys.stderr)
    t_inv = {v: k for k, v in tpp.items()}
    q_inv = {v: k for k, v in qpp.items()}
    taq_to_t = {v: k for k, v in align.items()}

    def make_cover(pids, idxs):
        order = sorted((p, i) for p, i in zip(pids, idxs) if p)
        sv = [p for p, _ in order]

        def on(page_id):
            """Entries printed on the page: those starting on it, and the last one starting before it."""
            lo, hi = bisect.bisect_left(sv, page_id), bisect.bisect_right(sv, page_id)
            res = [order[j][1] for j in range(lo, hi)]
            if lo:
                res.append(order[lo - 1][1])
            return res
        return on
    t_on = make_cover(tpid, range(nt))
    q_on = make_cover(qpid, range(len(taq)))

    # --- name indexes over all registry entries ---
    hchains = []
    for i, e in enumerate(entries):
        forms = alt_phrases(e['header'])
        wts = [1.0] + [0.6] * (len(forms) - 1)
        if i in tq:
            forms.append(tq[i]['name']); wts.append(1.0)
        hchains.append([(chain(f), w) for f, w in zip(forms, wts) if chain(f)])
    head_norm = [set(nrm(e['header'][:300]).split()) | set(' '.join(nrm(e['header'][:300]).split()[k:k + 2]) for k in range(60))
                 | (set(nrm(tq[i]['name']).split()) if i in tq else set()) for i, e in enumerate(entries)]
    by_pair = collections.defaultdict(set)
    by_ism = collections.defaultdict(set)
    for i, hc in enumerate(hchains):
        for c, _w in hc:
            by_ism[c[0]].add(i)
            if len(c) > 1:
                by_pair[(c[0], c[1][:1])].add(i)

    S1 = json.load(open(os.path.join(SHAMELA, 'narrators.json'), encoding='utf-8'))
    used = set()
    for book in ('bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'malik', 'musnad_tayalisi'):
        d = os.path.join(SHAMELA, book)
        for f in os.listdir(d):
            if f.endswith('.json') and f not in ('book.json', 'index.json'):
                for r in json.load(open(os.path.join(d, f), encoding='utf-8')):
                    for nn in r.get('narrators') or ():
                        used.add(nn['man'])
    print('S1 ids used in the 7 books:', len(used), file=sys.stderr)

    result, stats = {}, collections.Counter()
    for x in S1:
        if x['id'] not in used:
            continue
        f = x['fields']
        s1chains = [c for c in (chain(x['name']), chain(re.split(r'[:،,]', x['full_name'])[0])) if c]
        s1_death = x['death'] % 100 if x['death'] else None
        s1_tab = tabaqa_key(f.get('طبقة رواة التقريب'))
        s1_ver = verdict_key(f.get('الرتبة عند ابن حجر'))
        s1_nisba = {w for w in nrm(f.get('النسب', '')).split() if w.endswith('ي') and len(w) >= 4 and w != 'مولي'}
        s1_kunya = ' '.join(nrm(f.get('الكنية', '')).split()[:2]) if f.get('الكنية') else None
        # page votes
        votes_t, votes_q = collections.Counter(), collections.Counter()
        npg_t = npg_q = 0
        for q in x['quotes']:
            if q['book'] == 'تهذيب الكمال':
                pg = t_inv.get((str(q['vol']), q['page']))
                if pg:
                    npg_t += 1
                    for i in set(t_on(pg)):
                        votes_t[i] += 1
            elif False:
                pg = q_inv.get((str(q['vol']), q['page']))
                if pg:
                    npg_q += 1
                    for qi in set(q_on(pg)):
                        if qi in taq_to_t:
                            votes_q[taq_to_t[qi]] += 1
        cands = set(votes_t) | set(votes_q)
        for sc in s1chains:
            if len(sc) >= 2:
                cands |= by_pair.get((sc[0], sc[1][:1]), set())
            elif len(by_ism.get(sc[0], ())) < 150:
                cands |= by_ism.get(sc[0], set())
        rows = []
        for i in cands:
            nscore, nlabel = rel_score(s1chains, hchains[i])
            sc = nscore
            ev = []
            if votes_t.get(i):
                sc += 6 * votes_t[i] / max(1, npg_t) + (2 if votes_t[i] >= 3 else 0)
                ev.append(f'tk_page{votes_t[i]}/{npg_t}')
            if votes_q.get(i):
                sc += 3 * votes_q[i] / max(1, npg_q)
                ev.append(f'taqrib_page{votes_q[i]}/{npg_q}')
            ev.append(nlabel)
            htext = head_norm[i]
            if s1_nisba:
                if any(w in htext for w in s1_nisba):
                    sc += 1.5; ev.append('nisba')
                else:
                    sc -= 1; ev.append('nisba_diff')
            if s1_kunya:
                if s1_kunya in htext:
                    sc += 1.5; ev.append('kunya')
                else:
                    sc -= 1; ev.append('kunya_diff')
            t = tq.get(i)
            if t:
                d = death_mod100(t['death'])
                if s1_death is not None and d is not None:
                    gap = min((d - s1_death) % 100, (s1_death - d) % 100)
                    if gap == 0:
                        sc += 3; ev.append('death')
                    elif gap <= 2:
                        sc += 1; ev.append('death_near')
                    else:
                        sc -= 3; ev.append('death_diff')
                tk = tabaqa_key(t['tabaqa'])
                if s1_tab and tk:
                    if s1_tab == tk:
                        sc += 1.5; ev.append('tabaqa')
                    elif abs(s1_tab - tk) > 1:
                        sc -= 1; ev.append('tabaqa_diff')
                vk = verdict_key(t['grade'])
                if s1_ver and vk:
                    if s1_ver == vk or vk.startswith(s1_ver + ' ') or s1_ver.startswith(vk + ' '):
                        sc += 1.5; ev.append('verdict')
                    else:
                        sc -= 0.7; ev.append('verdict_diff')
            rows.append((sc, i, ev, nscore))
        rows.sort(key=lambda r: -r[0])
        rec = {'name': x['name'], 'full_name': x['full_name'], 'death': x['death'], 'registry': None,
               'confidence': 'none', 'method': None, 'score': None, 'runner_up': None}
        if rows:
            sc, i, ev, ns = rows[0]
            top_chain = hchains[i][0][0] if hchains[i] else None
            dups = [r for r in rows[1:] if i < nt and r[1] >= nt and hchains[r[1]] and hchains[r[1]][0][0] == top_chain
                    and 'death_diff' not in r[2]]
            dup_ids = {r[1] for r in dups}
            others = [r for r in rows[1:] if r[1] not in dup_ids]
            margin = sc - (others[0][0] if others else -10)
            page = any(e.startswith('tk_page') for e in ev)
            pg = next((e for e in ev if e.startswith('tk_page')), None)
            pshare = (int(pg[7:].split('/')[0]) / int(pg.split('/')[1])) if pg else 0
            hard = ns < 0 or ('death_diff' in ev and not (page and pshare >= 0.5 and ns >= 3.5))
            corro = sum(1 for e in ev if e in ('death', 'tabaqa', 'verdict', 'nisba', 'kunya', 'death_near'))
            conf = 'none'
            if sc >= 8 and margin >= 3 and not hard and ns >= 2.5 and (page or corro >= 3):
                conf = 'exact'
            elif sc >= 5 and margin >= 2 and not hard and ns >= 2.5:
                conf = 'probable'
            rec.update(registry=ids[i] if conf != 'none' else None, confidence=conf, method='+'.join(ev),
                       score=round(sc, 1), margin=round(margin, 1),
                       best=ids[i], best_header=entries[i]['header'][:80].replace('\n', ' '),
                       runner_up=(ids[others[0][1]] if others else None),
                       dups=[ids[j] for j in sorted(dup_ids)] or None)
        stats[rec['confidence']] += 1
        result[str(x['id'])] = rec
    os.makedirs(LINKS, exist_ok=True)
    json.dump(result, open(os.path.join(LINKS, 's1_registry_map.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    print(dict(stats), file=sys.stderr)


if __name__ == '__main__':
    main()
