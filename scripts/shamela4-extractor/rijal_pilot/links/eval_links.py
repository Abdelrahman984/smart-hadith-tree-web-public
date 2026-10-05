"""Precision of our isnad resolver against Shamela's own narrator links (draft).

Inputs : data/shamela_rijal/review/links/<book>.json (run_links.py: our resolutions),
         data/shamela_rijal/review/links/s1_registry_map.json (map_s1.py: S1 id -> registry id, independent evidence),
         data/shamela/<book>/*.json (records with `narrators` spans).
Output : data/shamela_rijal/review/links/links_eval.json (+ links_sample60.json, links_disagreements.json)
Usage  : python eval_links.py
"""
import collections
import random
import sys

from map_s1 import chain, rel_score, alt_phrases
from s1_common import *

BOOKS = ['bukhari', 'muslim', 'abudawud', 'tirmidhi', 'nasai', 'malik', 'musnad_tayalisi']
KEY = lambda t: re.sub(r'\s+', ' ', HARAKAT.sub('', t or ''))
BARE = {'سفيان': 'سفيان', 'حماد': 'حماد', 'عطاء': 'عطاء', 'هشام': 'هشام', 'ابو معاويه': 'أبو معاوية',
        'ابو اسحاق': 'أبو إسحاق', 'عبد_له': 'عبد الله'}


def span_tokens(s):
    return nrm(s).split()


STOP1 = {'ابو', 'ابن', 'بن', 'ام', 'و'}


def sim(a, b):
    """1.0 equal token lists; ~0.5-0.9 contiguous containment (shorter has 2+ tokens, or 1 non-stop token at an end
    of the longer); else token Jaccard (must reach .6)."""
    if not a or not b:
        return 0.0
    if a == b:
        return 1.0
    short, long_ = (a, b) if len(a) <= len(b) else (b, a)
    k = len(short)
    for i in range(len(long_) - k + 1):
        if long_[i:i + k] == short:
            if k >= 2 or (short[0] not in STOP1 and (i == 0 or i == len(long_) - 1)):
                return 0.5 + 0.4 * k / len(long_)
            break
    A, B = set(a), set(b)
    return len(A & B) / len(A | B)


def align(names, spans):
    """In order: each of our names -> the best span within the next 6 (sim >= .6; earliest on ties), else None."""
    out, cur = [], 0
    global LAST_SIMS
    LAST_SIMS = []
    for n in names:
        nt = span_tokens(n)
        best = None
        for k in range(cur, min(len(spans), cur + 6)):
            sc = sim(nt, spans[k]['toks'])
            if sc >= 0.6 and (best is None or sc > best[0] + 1e-9):
                best = (sc, k)
        if best:
            out.append(best[1]); cur = best[1] + 1; LAST_SIMS.append(best[0])
        else:
            out.append(None); LAST_SIMS.append(0)
    return out


def main():
    entries, nt, ids, taq, align_t = load_registry()
    idx = {ids[i]: i for i in range(len(entries))}
    smap = json.load(open(os.path.join(LINKS, 's1_registry_map.json'), encoding='utf-8'))
    S1 = {str(x['id']): x for x in json.load(open(os.path.join(SHAMELA, 'narrators.json'), encoding='utf-8'))}
    hchain_cache = {}

    def hchains(rid):
        if rid not in hchain_cache:
            e = entries[idx[rid]]
            hchain_cache[rid] = [chain(f) for f in alt_phrases(e['header'])[:1]]
        return hchain_cache[rid]

    def compat(seg, rid):
        return rel_score([c for c in [chain(seg)] if c], [(c, 1.0) for c in hchains(rid)])[0]

    def all_chains(rid):
        e = entries[idx[rid]]
        return [c for c in (chain(f) for f in alt_phrases(e['header'])) if c]

    def pre(x, y):
        return x[:len(y)] == y or y[:len(x)] == x

    def is_dup(a, b, man=None):
        """Heuristic 'same person registered twice'. (1) a non-Tahdhib entry and another entry (either source) with the
        same chain (spelling variants tolerated), or the shorter an ordered subsequence of the longer with 3+ segments, or
        2 segments with a skipped ancestor ('أحمد بن حنبل' ~ 'أحمد بن محمد بن حنبل'); never two Tahdhib entries.
        (2) our entry is the kunya-only entry of the S1 narrator ('أم سلمة' for هند بنت أبي أمية)."""
        if man is not None:
            kun = S1[man]['fields'].get('الكنية', '')
            fp = nrm(entries[idx[a]]['header'][:80]).split()
            if fp and fp[0] in ('ابو', 'ام'):
                for k in re.split(r'[،,]|ويقال|وقيل|[.]', kun):
                    kt = nrm(k).split()
                    if kt and fp[:len(kt)] == kt:
                        return True
        if idx[a] < nt and idx[b] < nt:
            return False
        if man is not None and idx[a] >= nt:
            # (3) same ism + father as the S1 narrator, and its kunya and a nisba both appear in our entry's header
            f = S1[man]['fields']
            ht = set(nrm(entries[idx[a]]['header'][:300]).split())
            sc = chain(S1[man]['name'])
            nis = {w for w in nrm(f.get('النسب', '')).split() if w.endswith('ي') and len(w) >= 4 and w != 'مولي'}
            kt = nrm(f.get('الكنية', '')).split()[:2]
            if (len(sc) >= 2 and any(len(c) >= 2 and c[0] == sc[0] and c[1][:1] == sc[1][:1] for c in all_chains(a))
                    and kt and set(kt) <= ht and nis & ht):
                return True
        for ca in all_chains(a):
            for cb in all_chains(b):
                if ca[0] != cb[0] and not pre(ca[0], cb[0]):
                    continue
                short, long_ = (ca, cb) if len(ca) <= len(cb) else (cb, ca)
                if len(short) < 2:
                    continue
                it = iter(long_)
                if not all(any(pre(x, y) for x in it) for y in short):
                    continue
                if len(short) == len(long_) or len(short) >= 3:
                    return True
                if len(short) == 2 and not pre(long_[1], short[1]):
                    return True
        return False

    tot = collections.Counter()
    by = {k: collections.defaultdict(collections.Counter) for k in ('book', 'how', 'pos', 'conf')}
    cases = []             # all disagreements
    dup_pairs = collections.Counter()
    bare_book = collections.defaultdict(collections.Counter)
    unres_seg = collections.Counter()
    unal_samples = []
    per_man = collections.defaultdict(collections.Counter)
    bare = collections.defaultdict(lambda: {'n': 0, 'unres': 0, 'unres_mapped': collections.Counter(),
                                            'unres_in_ties': 0, 'unres_tie_n': 0, 'res_ok': 0, 'res_n': 0,
                                            'all_mapped': collections.Counter()})
    align_stats = collections.Counter()
    for book in BOOKS:
        rows = json.load(open(os.path.join(LINKS, f'{book}.json'), encoding='utf-8'))
        d = os.path.join(SHAMELA, book)
        recs = collections.defaultdict(list)
        for f in sorted(os.listdir(d)):
            if f.endswith('.json') and f not in ('book.json', 'index.json'):
                for r in json.load(open(os.path.join(d, f), encoding='utf-8')):
                    if r.get('arabic'):
                        recs[KEY(r['arabic'])[:400]].append(r)
        used = collections.Counter()
        for row in rows:
            cand = recs.get(row['isnad'])
            if not cand:
                align_stats[book, 'record_not_found'] += 1
                continue
            r = cand[used[row['isnad']] % len(cand)]
            used[row['isnad']] += 1
            arabic = r['arabic']
            spans = sorted(r.get('narrators') or [], key=lambda s: s['start'])
            for s in spans:
                s['toks'] = span_tokens(arabic[s['start']:s['end']])
            names = [o['name'] for o in row['ours']]
            al = align(names, spans)
            sims = list(LAST_SIMS)
            for pos, (o, k) in enumerate(zip(row['ours'], al)):
                tot[book, 'names'] += 1
                if k is None:
                    tot[book, 'unaligned'] += 1
                    tot[book, 'unaligned_resolved'] += o['id'] is not None
                    if o['id'] is not None and len(unal_samples) < 4000:
                        unal_samples.append((book, o['name'], o['how']))
                    continue
                tot[book, 'aligned'] += 1
                man = str(spans[k]['man'])
                m = smap.get(man)
                conf = m['confidence'] if m else 'none'
                nm = nrm(o['name']).strip()
                bk = BARE.get(nm)
                if bk and conf in ('exact', 'probable'):
                    bare[bk]['all_mapped'][m['registry']] += 1
                if conf == 'none':
                    tot[book, 'aligned_unmapped'] += 1
                    continue
                tot[book, 'aligned_mapped_' + conf] += 1
                mid = m['registry']
                lbl = {'book': book, 'how': o['how'], 'pos': min(pos, 7), 'conf': conf}
                if o['id'] is None:
                    unres_seg[(o['how'], HARAKAT.sub('', o['name']).strip())] += 1
                    if bk:
                        bare_book[bk, book][mid] += 1
                    for dim in by:
                        by[dim][lbl[dim]]['unresolved'] += 1
                    if bk:
                        b = bare[bk]
                        b['n'] += 1; b['unres'] += 1; b['unres_mapped'][mid] += 1
                        if o.get('ties'):
                            b['unres_tie_n'] += 1; b['unres_in_ties'] += mid in o['ties']
                            fm = sorted(o['ties'], key=lambda t: -len(entries[idx[t]]['talamidh']))
                            b.setdefault('fame_ok', 0); b.setdefault('fame3_ok', 0); b.setdefault('fame3_pick', 0)
                            b['fame_ok'] += fm[0] == mid
                            if len(fm) == 1 or len(entries[idx[fm[0]]]['talamidh']) >= 3 * max(1, len(entries[idx[fm[1]]]['talamidh'])):
                                b['fame3_pick'] += 1; b['fame3_ok'] += fm[0] == mid
                            b.setdefault('tie_sizes', collections.Counter())[len(o['ties'])] += 1
                    continue
                per_man[man][o['id']] += 1
                same = o['id'] == mid
                dup = (not same) and (is_dup(o['id'], mid, man) or bool(m.get('dups') and o['id'] in m['dups']))
                status = 'agree' if same else ('dup' if dup else 'differ')
                for dim in by:
                    by[dim][lbl[dim]][status] += 1
                if bk:
                    bare[bk]['res_n'] += 1; bare[bk]['res_ok'] += bool(same or dup)
                if status == 'dup':
                    dup_pairs[(o['id'], mid)] += 1
                if status == 'differ':
                    s1 = S1[man]
                    cases.append({'book': book, 'conf': conf, 'how': o['how'], 'pos': pos, 'seg': o['name'], 'ours': o['id'],
                                  'ours_header': entries[idx[o['id']]]['header'][:90].replace('\n', ' '),
                                  's1_man': man, 's1_name': s1['name'], 's1_full': s1['full_name'][:90], 's1_death': s1['death'],
                                  'mapped': mid, 'mapped_header': entries[idx[mid]]['header'][:90].replace('\n', ' '),
                                  'isnad': row['isnad'], 'ties': o.get('ties'), 'span_text': arabic[spans[k]['start']:spans[k]['end']],
                                  'align_sim': round(sims[pos], 2), 'n_spans': len(spans)})
    # ---- classify the disagreements automatically ----
    def tokset(*texts):
        out = set()
        for t in texts:
            out |= {w for w in nrm(t).split() if w not in ('بن', 'ابن')}
        return out

    for c in cases:
        seg = c['seg']
        st = tokset(seg)
        e_o, e_m = entries[idx[c['ours']]], entries[idx[c['mapped']]]
        o_tok = tokset(e_o['header'][:250], taq[align_t[idx[c['ours']]]]['name'] if idx[c['ours']] in align_t else '')
        m_tok = tokset(e_m['header'][:250], c['s1_name'], c['s1_full'], taq[align_t[idx[c['mapped']]]]['name'] if idx[c['mapped']] in align_t else '')
        c['fit'] = {'ours': st <= o_tok, 'shamela': st <= m_tok}
        fo, fm = c['fit']['ours'], c['fit']['shamela']
        n = nrm(seg).strip()
        if n.startswith('ام ') or n.startswith('ام_'):
            cls = 'umm_resolved_to_other'
        elif c['align_sim'] < 1.0 and not (tokset(c['span_text']) <= st or st <= tokset(c['span_text'])):
            cls = 'alignment_wrong (span text differs from our name)'
        elif fo and fm:
            cls = 'both_fit (shared name: resolver picked another)'
        elif not fo and fm:
            cls = 'ours_name_does_not_fit (our error)'
        elif fo and not fm:
            cls = 'shamela_name_does_not_fit (Shamela link or mapping)'
        else:
            cls = 'neither_fits (segmentation / cut name)'
        c['class'] = cls
        nt_ = nrm(seg).split()
        if n.startswith('ام ') or n.startswith('ام_'):
            ac = 'umm: «أم X» resolved to someone else'
        elif c['how'] == 'father':
            ac = "father rule: «أبيه» resolved to the wrong father"
        elif nt_ and nt_[0] in ('ابن', 'بن') or (c['seg'].strip().startswith(('يعني', 'هو'))):
            ac = 'fragment: «يعني ابن X» / «ابن X» cut from its name'
        elif idx[c['ours']] >= nt and idx[c['mapped']] < nt:
            ac = 'shaykh-book entry (tuhfa/tajil/khatib/rawd/...) beats the Tahdhib entry'
        elif len([w for w in nt_ if w not in ('بن', 'ابن')]) <= 1 or (nt_ and nt_[0] in ('ابو', 'ام')) and len(nt_) <= 3:
            ac = 'bare name (ism / kunya / nisba only) resolved to another bearer'
        elif cls.startswith('alignment') or cls.startswith('neither'):
            ac = 'alignment / segmentation (span text differs)'
        else:
            ac = 'named form (ism + nasab) resolved to another person'
        c['auto_class'] = ac
    # ---- output ----
    def summ(c):
        ag, df, du, un = c['agree'], c['differ'], c['dup'], c['unresolved']
        n = ag + df + du
        return {'resolved_compared': n, 'agree': ag, 'dup_agree': du, 'differ': df,
                'precision_strict': round(ag / n, 4) if n else None, 'precision_with_dup': round((ag + du) / n, 4) if n else None,
                'unresolved': un, 'coverage': round(n / (n + un), 4) if n + un else None}
    for dim in by.values():
        for k in dim:
            for s in ('agree', 'differ', 'dup', 'unresolved'):
                dim[k].setdefault(s, 0)
    res = {'alignment': {}, 'overall': {}, 'by_book': {}, 'by_how': {}, 'by_pos': {}, 'by_conf': {}}
    for book in BOOKS:
        res['alignment'][book] = {'names': tot[book, 'names'], 'aligned': tot[book, 'aligned'],
                                  'aligned_rate': round(tot[book, 'aligned'] / max(1, tot[book, 'names']), 4),
                                  'mapped_exact': tot[book, 'aligned_mapped_exact'], 'mapped_probable': tot[book, 'aligned_mapped_probable'],
                                  'unmapped': tot[book, 'aligned_unmapped'], 'unaligned_but_resolved_by_us': tot[book, 'unaligned_resolved'], 'record_not_found': align_stats[book, 'record_not_found']}
    for key, dim in (('by_book', 'book'), ('by_how', 'how'), ('by_pos', 'pos'), ('by_conf', 'conf')):
        res[key] = {str(k): summ(v) for k, v in sorted(by[dim].items(), key=lambda kv: str(kv[0]))}
    allc = collections.Counter()
    for v in by['book'].values():
        allc.update(v)
    res['overall'] = summ(allc)
    for conf in ('exact', 'probable'):
        res['overall_' + conf] = summ(by['conf'][conf])
    res['auto_classes'] = dict(collections.Counter(c['auto_class'] for c in cases).most_common())
    res['auto_classes_by_how'] = {k: dict(collections.Counter(c['how'] for c in cases if c['auto_class'] == k).most_common()) for k in res['auto_classes']}
    res['top_segments'] = [{'seg': k, 'n': n} for k, n in collections.Counter(HARAKAT.sub('', c['seg']).strip() for c in cases).most_common(40)]
    res['classes'] = dict(collections.Counter(c['class'] for c in cases).most_common())
    res['classes_by_conf'] = {conf: dict(collections.Counter(c['class'] for c in cases if c['conf'] == conf).most_common())
                              for conf in ('exact', 'probable')}
    res['top_pairs'] = [{'ours': a, 'mapped': b, 'n': n, 'ours_header': entries[idx[a]]['header'][:60], 'mapped_header': entries[idx[b]]['header'][:60]}
                        for (a, b), n in collections.Counter((c['ours'], c['mapped']) for c in cases).most_common(40)]
    res['unresolved_top_segments'] = [{'how': h, 'seg': g, 'n': n} for (h, g), n in unres_seg.most_common(30)]
    res['unresolved_by_how'] = {h: sum(n for (hh, _), n in unres_seg.items() if hh == h) for h in ('ambiguous', 'missing')}
    res['bare_unresolved_by_book'] = {f'{k[0]} | {k[1]}': [{'header': entries[idx[i]]['header'][:30], 'n': n} for i, n in c.most_common(4)]
                                      for k, c in sorted(bare_book.items())}
    cons = []
    for man, c in per_man.items():
        n = sum(c.values())
        mid = smap[man]['registry']
        top, tn = c.most_common(1)[0]
        if n >= 8 and top != mid and tn / n >= 0.6 and c[mid] / n < 0.3 and not is_dup(top, mid, man):
            cons.append({'s1_man': man, 's1_name': S1[man]['name'], 'conf': smap[man]['confidence'], 'n': n, 'our_top': top,
                         'our_top_header': entries[idx[top]]['header'][:60].replace(chr(10), ' '), 'our_top_n': tn,
                         'mapped': mid, 'mapped_header': entries[idx[mid]]['header'][:60].replace(chr(10), ' '), 'mapped_n': c[mid]})
    cons.sort(key=lambda r: -r['n'])
    res['map_vs_cooccurrence'] = {'s1_ids_where_our_consistent_choice_differs': len(cons), 'top': cons[:25]}
    res['bare'] = {}
    for k, b in bare.items():
        res['bare'][k] = {'aligned_mapped': b['n'] + b['res_n'], 'left_unresolved': b['unres'],
                          'unresolved_mapped_distribution': [{'id': i, 'header': entries[idx[i]]['header'][:60].replace('\n', ' '), 'n': n}
                                                             for i, n in b['unres_mapped'].most_common(8)],
                          'unresolved_with_ties': b['unres_tie_n'], 'shamela_id_inside_ties': b['unres_in_ties'],
                          'most_students_pick_agrees': b.get('fame_ok', 0), 'three_x_rule_picks': b.get('fame3_pick', 0),
                          'three_x_rule_agrees': b.get('fame3_ok', 0), 'tie_sizes': dict(b.get('tie_sizes', {})),
                          'resolved': b['res_n'], 'resolved_agree_with_shamela': b['res_ok'],
                          'all_mapped_distribution': [{'id': i, 'header': entries[idx[i]]['header'][:60].replace('\n', ' '), 'n': n}
                                                      for i, n in b['all_mapped'].most_common(8)]}
    res['dup_pairs_top'] = [{'ours': a, 'mapped': b, 'n': n, 'ours_header': entries[idx[a]]['header'][:60].replace(chr(10), ' '),
                             'mapped_header': entries[idx[b]]['header'][:60].replace(chr(10), ' ')} for (a, b), n in dup_pairs.most_common(400)]
    json.dump(res, open(os.path.join(LINKS, 'links_eval.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    json.dump(cases, open(os.path.join(LINKS, 'links_disagreements.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    random.seed(11)
    json.dump(random.sample(cases, min(60, len(cases))), open(os.path.join(LINKS, 'links_sample60.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print(json.dumps({k: res[k] for k in ('overall', 'overall_exact', 'overall_probable', 'classes')}, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
