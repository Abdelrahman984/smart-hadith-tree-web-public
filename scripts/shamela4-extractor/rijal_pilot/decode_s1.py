"""Decode Shamela 4's narrator encyclopedia (service/S1.db, table b) to JSON.

Encoding (worked out from anchor names linked from the book texts):
  * every text column is a per-byte substitution;
  * ASCII/JSON structure bytes follow EBCDIC cp500 ('"'=0x7f, ':'=0x7a, ','=0x6b,
    '['=0x4a, ']'=0x5a, '{'=0xc0, '}'=0xd0, '\'=0xe0, digits 0xf0..0xf9, letters = Latin);
  * Arabic letters/harakat/punctuation use bytes that are control codes in cp500
    (table ARABIC below);
  * `a` and `b` are JSON documents after decoding.
Read-only on the Shamela database.  Usage: python decode_s1.py [out.json]
"""
import json, os, re, sqlite3, sys
from collections import Counter

DB = os.environ.get('SHAMELA_S1', r'D:\Islamic\shamela4\database\service\S1.db')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'data', 'shamela', 'narrators.json')

ARABIC = {
    0x40: ' ', 0x68: 'ا', 0x45: 'ل', 0x55: 'ي', 0x74: 'ب', 0x43: 'ن', 0x46: 'م', 0x9c: 'و', 0xfe: 'ع',
    0x69: 'ر', 0x47: 'ه', 0x77: 'د', 0x72: 'ت', 0x75: 'ح', 0xae: 'ق', 0x66: 'أ', 0xee: 'س', 0x59: 'ك',
    0xad: 'ف', 0x71: 'ة', 0xac: 'ذ', 0x73: 'ث', 0x78: 'ج', 0xef: 'ص', 0x76: 'خ', 0x67: 'إ', 0x58: 'ى',
    0xed: 'ز', 0xeb: 'ش', 0x80: 'ط', 0xec: 'ض', 0x9e: 'ئ', 0xfb: 'غ', 0x65: 'ء', 0x62: 'آ', 0x63: 'ؤ',
    0xfd: 'ظ',
    # punctuation
    0xaa: '،', 0xab: '؟', 0x9b: '؛', 0xfc: 'ـ',
    # harakat
    0xce: '\u064E', 0xcc: '\u0650', 0xcf: '\u064F', 0xde: '\u0652', 0x70: '\u0651',
    0x8c: '\u064B', 0xcd: '\u064D', 0x49: '\u064C',
}
UNKNOWN = {}   # byte -> placeholder char (private use), filled for bytes no rule covers


def build_table():
    t = {}
    for b in range(256):
        if b in ARABIC:
            t[b] = ARABIC[b]
        else:
            t[b] = bytes([b]).decode('cp500')
    return t


TABLE = build_table()
# Rare bytes that are not letters/ASCII.  Inferred from context, NOT verified (see notes):
#   0x36 pairs like a dash around an aside            -> en dash
#   0x15 stands for an elision ("... al-hadith")       -> ellipsis
#   0x14 (prefix of ~58 names/words), 0x3e (once after ba'), 0x8e (once before a full stop)
#        have no recoverable visible form             -> dropped (counted in UNRESOLVED)
TABLE[0x36] = '–'
TABLE[0x15] = '…'
for _b in (0x14, 0x3e, 0x8e):
    TABLE[_b] = ''
UNRESOLVED = (0x14, 0x3e, 0x8e)


def dec(x):
    return None if x is None else ''.join(TABLE[b] for b in x)


SRC = re.compile(r'^(.*) \((\d+)/ (\d+)\)( \(بدون ترقيم\))?$', re.S)


def parse_source(src):
    """'تهذيب الكمال (2/ 301)' -> (book, vol, page, extra_text_before_book)."""
    m = SRC.match(src)
    if not m:
        return None, None, None, None
    book, extra = m.group(1), None
    if '\n' in book:
        extra, book = book.rsplit('\n', 1)
    return book, int(m.group(2)), int(m.group(3)), extra


def main(out_path=OUT):
    c = sqlite3.connect('file:%s?mode=ro' % DB, uri=True)
    res, bad = [], 0
    for i, s, l, d, a, b in c.execute('select i,s,l,d,a,b from b order by i'):
        rec = {'id': i, 'name': dec(s), 'full_name': dec(l), 'death': d, 'fields': {}, 'quotes': []}
        try:
            if b:
                bd = json.loads(dec(b))
                rec['fields'] = {lab: val for lab, val in bd.get('summary', [])}
                # 'free' (62 rows): free-text notes as [text, source] pairs
                rec['notes'] = [{'text': x, 'source': y} for x, y in bd.get('free', [])]
                extra_keys = set(bd) - {'summary', 'free'}
                assert not extra_keys, extra_keys
            if a:
                ad = json.loads(dec(a))
                # sections: 'garh' = jarh wa ta'dil quotes; 'fwaed' = fawa'id (evidence of hearing)
                for key, groups in ad.items():
                    for critic, quotes in groups:
                        for text, src, ref in quotes:
                            book, vol, page, extra = parse_source(src)
                            q = {'section': key, 'critic': critic, 'text': text, 'source': src,
                                 'book': book, 'vol': vol, 'page': page, 'ref': ref}
                            if extra:
                                q['source_extra'] = extra
                            rec['quotes'].append(q)
        except Exception as e:   # keep the raw decoded text rather than losing it
            bad += 1
            rec['error'] = repr(e)
            rec['raw_a'], rec['raw_b'] = dec(a), dec(b)
        res.append(rec)
    json.dump(res, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    print('narrators', len(res), 'parse errors', bad)


if __name__ == '__main__':
    main(*sys.argv[1:])
