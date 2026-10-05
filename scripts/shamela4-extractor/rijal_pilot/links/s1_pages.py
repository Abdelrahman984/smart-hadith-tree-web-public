"""Tahdhib / Taqrib entry -> Shamela page id, and page id -> printed (part, page) (draft, used by map_s1.py)."""
import bisect
import re

from s1_common import *

CAND = re.compile(r'(?:^|[\n\x01])\s*([٠-٩]+)\s*(?:-|:)')


def entry_pages(book_id, db_rel, nums, heads, start_re):
    """For each entry (book order): the Shamela page id where it starts, by its number among the numbers
    at line starts after the first entry; fallback: the first words of its name."""
    pages, part_page = page_index(book_id, db_rel)
    offs, parts, pos = [], [], 0
    for pid, t in pages:
        offs.append(pos); parts.append(pid)
        pos += len(t) + 1
    text = '\x01'.join(t for _, t in pages)
    s = re.search(start_re, text)
    base = s.start() if s else 0
    cands = [(m.start(), m.group(1)) for m in CAND.finditer(text, base)]
    cpos = [c[0] for c in cands]
    out, ci, miss = [], 0, 0
    for num, head in zip(nums, heads):
        found = None
        if num:
            for k in range(ci, min(len(cands), ci + 400)):
                if cands[k][1] == num:
                    found, ci = cands[k][0], k + 1
                    break
        if found is None and head:
            prev = cands[ci - 1][0] if ci else base
            k = text.find(head, prev, prev + 60000)
            if k >= 0:
                found = k
                ci = max(ci, bisect.bisect_left(cpos, k))
        if found is None:
            miss += 1
            out.append(None)
            continue
        out.append(parts[bisect.bisect_right(offs, found) - 1])
    return out, part_page, miss
