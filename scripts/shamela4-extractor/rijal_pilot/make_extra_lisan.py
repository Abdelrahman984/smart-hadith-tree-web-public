"""Lisan al-Mizan as a fallback registry file: data/shamela_rijal/lisan.json (parse_lisan.py) -> extra_lisan.json.

Run from data/shamela_rijal:  python ../../scripts/shamela4-extractor/rijal_pilot/make_extra_lisan.py

Every entry is marked "fallback": link_tahdhib.py builds a second set of name indexes for such entries and uses it only
when nobody else matches a name (its namesakes made «أنس بن مالك» and «بندار» ties). The header is cut at the first word
that starts narrating («روى»، «ذكره»، «عن»…): the rest mentions other people, whose names then matched this entry.
"""
import json
import re
import sys

NARRATING = re.compile(r'\s(?:عن|روى|يروي|ذكره|ذكر|قال|وقال|له|حدث|حدثنا|سمع|لقيه|أخذ|وثقه|ضعفه|ليس|كان|مات|توفي|ترجمه)\s')

src = json.load(open('lisan.json', encoding='utf-8'))
out = []
for e in src:
    if e['kind'] != 'entry':
        continue
    e = dict(e)
    e['header'] = NARRATING.split(e['header'] + ' ')[0].strip(' ،.')
    if len(e['header'].split()) < 2:
        continue
    e.setdefault('verdict', None)
    e.setdefault('aliases', [])
    e['source'] = 'lisan'
    e['fallback'] = True
    out.append(e)
json.dump(out, open('extra_lisan.json', 'w', encoding='utf-8'), ensure_ascii=False)
print('extra_lisan.json:', len(out), 'of', sum(1 for e in src if e['kind'] == 'entry'), 'entries')
