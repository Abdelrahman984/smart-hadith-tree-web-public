"""Parse Taqrib al-Tahdhib (Shamela 4 book 8609) dumped by ShamelaLuceneDumper.

Each entry is one line: "N- name [notes] grade من الطبقة [مات سنة ...] symbols".
Usage: python parse_taqrib.py <dump_dir> <out_json>
"""
import json
import re
import sys
from collections import Counter

DUMP, OUT = sys.argv[1], sys.argv[2]
AR_DIGITS = str.maketrans('٠١٢٣٤٥٦٧٨٩', '0123456789')
HARAKAT = re.compile(r'[ً-ْٰـ]')

pages = []
with open(f'{DUMP}/8609_pages.tsv', encoding='utf-8') as f:
    for line in f:
        pid, body = line.rstrip('\n').split('\t', 1)
        pages.append((int(pid.split('-')[1]), body.replace('\\n', '\n')))
pages.sort()
# Pages break mid-entry; an entry line always starts with "N-" or "[]".
text = ' '.join(b for _, b in pages)
text = HARAKAT.sub('', re.sub(r'<span[^>]*>|</span>', '\n', text))
text = re.sub(r'\s+(?=[٠-٩]+\s*-\s|\[\]\s)', '\n', text)

ORDINALS = ('الأولى|الثانية عشرة|الحادية عشرة|الثانية|الثالثة|الرابعة|الخامسة|السادسة|السابعة'
            '|الثامنة|التاسعة|العاشرة')
TABAQA = re.compile(rf'(?:من|في)\s+((?:كبار|صغار|أوساط|أواسط)\s+)?({ORDINALS})')
# Ibn Hajr's verdict vocabulary (the first word of the verdict phrase).
GRADE_LEAD = re.compile(
    r'(?:^|\s|\[)(صحابي[ة]?|وثقه\s+\S+|كذبه\s+\S+|غير مشهور|صدوقا|له صحبة|لها صحبة|له رؤية|مخضرم|ثق[ةه]|ثبت|حافظ|إمام|صدوق[ة]?|لا بأس به|ليس به بأس|'
    r'مقبول[ة]?|لين الحديث|لين|فيه لين|ضعيف[ة]?|مجهول[ة]?|مستور[ة]?|لا يعرف|لا تعرف|لا يدرى|متروك|'
    r'متهم|كذاب|كذبوه|وضاع|منكر الحديث|يخطئ|فقيه|عابد|زاهد|الفقيه|الحافظ|الإمام|متفق على)(?=\s|،|$|\])')
SYMBOLS = re.compile(r'((?:\s(?:ع|٤|خ|م|د|ت|س|ق|بخ|خت|عخ|ر|ي|كن|ز|مد|قد|خد|ف|سي|عس|ل|تم|ص|فق|جز|م ت|تمييز|تخ))+)\s*$')

entries, xrefs = [], []
for line in text.split('\n'):
    line = line.strip()
    m = re.match(r'([٠-٩]+)\s*-\s*(.+)', line)
    if not m:
        x = re.match(r'\[\]\s*(.+?)\s+هو\s+(.+)', line)
        if x:
            xrefs.append({'alias': x.group(1), 'target': x.group(2).strip()})
        continue
    num, body = int(m.group(1).translate(AR_DIGITS)), m.group(2)
    sym = SYMBOLS.search(body)
    symbols = sym.group(1).strip() if sym else ''
    core = body[:sym.start()] if sym else body
    tab = TABAQA.search(core)
    g = GRADE_LEAD.search(core)
    # The verdict runs from its first word to the end of the line, minus the tabaqa and death
    # clauses: "صدوق من السابعة خلط بعد احتراق كتبه" -> "صدوق خلط بعد احتراق كتبه".
    grade = None
    if g:
        grade = core[g.start():]
        if tab and tab.start() > g.start():
            grade = grade[:tab.start() - g.start()] + ' ' + grade[tab.end() - g.start():]
        grade = re.split(r'\s(?:مات|قتل|توفي|استشهد)\s', ' ' + grade)[0]
        grade = re.sub(r'\s+', ' ', grade).strip(' ،[]')
    name = core[:g.start()].strip(' ،[') if g else (core[:tab.start()].strip(' ،') if tab else core)
    death = re.search(r'(?:مات|قتل|استشهد|توفي)\s[^،]*', core[tab.end():] if tab else core)
    entries.append({
        'num': num, 'name': name, 'grade': grade,
        'tabaqa': ((tab.group(1) or '') + tab.group(2)).strip() if tab else None,
        'death': death.group(0).strip() if death else None,
        'symbols': symbols, 'raw': body,
    })

nums = [e['num'] for e in entries]
print(f'entries: {len(entries)}  max num {max(nums)}  missing numbers: {max(nums) - len(set(nums))}  xrefs: {len(xrefs)}')
for k in ('grade', 'tabaqa', 'death', 'symbols'):
    print(f'  with {k:8}: {sum(1 for e in entries if e[k]):5} ({sum(1 for e in entries if e[k]) / len(entries):.1%})')
lead = Counter(re.split(r'\s', e['grade'])[0] if e['grade'] else None for e in entries)
print('verdict lead words:', lead.most_common(25))
json.dump({'entries': entries, 'xrefs': xrefs}, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
