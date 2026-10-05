// A tiny stand-in for the ASP.NET API, so the tree / takhreej / verify / books pages can be tested without SQL Server.
// Started by playwright.config.ts on the API's default port (5147). Run it by hand with: node tests/fixtures/mock-api.mjs
import http from 'node:http';

const PORT = Number(process.env.MOCK_API_PORT ?? 5147);

const LONG = 'إنما الأعمال بالنيات، وإنما لكل امرئ ما نوى، فمن كانت هجرته إلى دنياه يصيبها أو إلى امرأة ينكحها فهجرته إلى ما هاجر إليه. '.repeat(4);
const SHORT = 'إنما الأعمال بالنيات';
const BUKHARI = 'صحيح البخاري';
const MUSLIM = 'صحيح مسلم';
const ABU_DAWUD = 'سنن أبي داود';
const IBN_MAJAH = 'سنن ابن ماجه';

// Realistic stored texts: isnad and matn together, as the real data has them.
const MATN_BUKHARI = 'حدثنا الحميدي حدثنا سفيان عن يحيى بن سعيد أن رسول الله صلى الله عليه وسلم قال إنما الأعمال بالنيات وإنما لكل امرئ ما نوى فمن كانت هجرته إلى الله ورسوله فهجرته إلى الله ورسوله';
const MATN_MUSLIM = 'عن عمر بن الخطاب رضي الله عنه قال سمعت رسول الله صلى الله عليه وسلم يقول إنما الأعمال بالنيات وإنما لكل امرئ ما نوى';
const MATN_OTHER = 'حدثنا أبو بكر بن أبي شيبة عن علي بن أبي طالب أن رسول الله صلى الله عليه وسلم قال الأعمال بالنيات';

const narrator = (id, name, over = {}) => ({ narratorId: id, narratorName: name, knownAs: null, generationTier: null, transmissionTerm: null, ...over });

// ── Single tree: C ← N1 ← N2 (mudallis, anomaly) ← N3 (fabricator, travel note) ← N4 (companion)
const SINGLE = [
  { id: 't0', stepOrder: 0, parentNodeId: null, ...narrator('C', 'محمد بن إسماعيل البخاري', { knownAs: 'البخاري', generationTier: 'الحادية عشرة', gradeEn: 'reliable' }) },
  { id: 't1', stepOrder: 1, parentNodeId: 't0', ...narrator('N1', 'عبد الله بن الزبير الحميدي', { knownAs: 'الحميدي', generationTier: 'العاشرة', transmissionTerm: 'حدثنا', gradeEn: 'reliable', residencePlaces: 'مكة', uniqueHadithCount: 800 }) },
  { id: 't2', stepOrder: 2, parentNodeId: 't1', ...narrator('N2', 'سفيان بن عيينة', { knownAs: 'سفيان', generationTier: 'الثامنة', transmissionTerm: 'عن', gradeEn: 'mostly_reliable', isMudallis: true, hasMukhtalit: true, isAnomaly: true, anomalyReason: 'عنعنة مدلس من المرتبة الثانية', uniqueHadithCount: 3 }) },
  { id: 't3', stepOrder: 3, parentNodeId: 't2', ...narrator('N3', 'راو متروك للاختبار', { generationTier: 'السادسة', transmissionTerm: 'عن', gradeEn: 'fabricator', travelNote: 'لا تشترك بلدانهما في الإقامة؛ راجع الرحلة' }) },
  { id: 't4', stepOrder: 4, parentNodeId: 't3', ...narrator('N4', 'عمر بن الخطاب', { knownAs: 'عمر', generationTier: 'صحابي', transmissionTerm: 'سمعت', gradeEn: 'companion' }) },
];

// ── Comparative tree (takhreej): three books meet at N2 and N4.
//   Bukhari:  C ← N1 ← N2 ← N4      Muslim: D ← N1 ← N2 ← N4      Abu Dawud: E ← N5 ← N2 ← N4
const cn = (id, narratorId, name, stepOrder, parentNodeId, hadith, book, over = {}) => ({
  id, stepOrder, parentNodeId, sourceHadithIds: hadith, sourceBooks: book, ...narrator(narratorId, name, over),
});
const N1 = ['N1', 'عبد الله بن الزبير الحميدي', { generationTier: 'العاشرة', gradeEn: 'reliable', residencePlaces: 'مكة', uniqueHadithCount: 800 }];
const N2 = ['N2', 'سفيان بن عيينة', { knownAs: 'سفيان', generationTier: 'الثامنة', gradeEn: 'mostly_reliable', isMudallis: true, hasMukhtalit: true, uniqueHadithCount: 3 }];
const N4 = ['N4', 'عمر بن الخطاب', { knownAs: 'عمر', generationTier: 'صحابي', gradeEn: 'companion' }];
// No gradeEn: a narrator outside Taqrib, shown as «غير مُقيَّم».
const N5 = ['N5', 'قتيبة بن سعيد', { generationTier: 'العاشرة' }];
const COMPARATIVE = [
  cn('c1', 'C', 'محمد بن إسماعيل البخاري', 0, null, ['h1'], [BUKHARI], { knownAs: 'البخاري', gradeEn: 'reliable', generationTier: 'الحادية عشرة' }),
  cn('c2', 'D', 'مسلم بن الحجاج', 0, null, ['h2'], [MUSLIM], { knownAs: 'مسلم', gradeEn: 'reliable', generationTier: 'الحادية عشرة' }),
  cn('c3', 'E', 'أبو داود السجستاني', 0, null, ['h3'], [ABU_DAWUD], { knownAs: 'أبو داود', gradeEn: 'reliable', generationTier: 'الحادية عشرة' }),
  cn('n1a', ...N1.slice(0, 2), 1, 'c1', ['h1'], [BUKHARI], { ...N1[2], transmissionTerm: 'حدثنا' }),
  cn('n1b', ...N1.slice(0, 2), 1, 'c2', ['h2'], [MUSLIM], { ...N1[2], transmissionTerm: 'حدثنا' }),
  cn('n5', ...N5.slice(0, 2), 1, 'c3', ['h3'], [ABU_DAWUD], { ...N5[2], transmissionTerm: 'حدثنا' }),
  cn('n2a', ...N2.slice(0, 2), 2, 'n1a', ['h1'], [BUKHARI], { ...N2[2], transmissionTerm: 'عن', hasMatnVariation: true, matnVariationSnippet: 'زيادة «إلى الله ورسوله»' }),
  cn('n2b', ...N2.slice(0, 2), 2, 'n1b', ['h2'], [MUSLIM], { ...N2[2], transmissionTerm: 'عن' }),
  cn('n2c', ...N2.slice(0, 2), 2, 'n5', ['h3'], [ABU_DAWUD], { ...N2[2], transmissionTerm: 'عن' }),
  cn('n4a', ...N4.slice(0, 2), 3, 'n2a', ['h1'], [BUKHARI], { ...N4[2], transmissionTerm: 'سمعت' }),
  cn('n4b', ...N4.slice(0, 2), 3, 'n2b', ['h2'], [MUSLIM], { ...N4[2], transmissionTerm: 'سمعت' }),
  cn('n4c', ...N4.slice(0, 2), 3, 'n2c', ['h3'], [ABU_DAWUD], { ...N4[2], transmissionTerm: 'سمعت' }),
  // h4: Ibn Majah ← Abu Bakr ibn Abi Shayba ← Ali (a different Companion)
  cn('c4', 'F', 'ابن ماجه القزويني', 0, null, ['h4'], [IBN_MAJAH], { knownAs: 'ابن ماجه', gradeEn: 'reliable', generationTier: 'الحادية عشرة' }),
  cn('n7', 'N7', 'أبو بكر بن أبي شيبة', 1, 'c4', ['h4'], [IBN_MAJAH], { generationTier: 'العاشرة', gradeEn: 'reliable', transmissionTerm: 'حدثنا' }),
  cn('n8', 'N8', 'علي بن أبي طالب', 2, 'n7', ['h4'], [IBN_MAJAH], { knownAs: 'علي', generationTier: 'صحابي', gradeEn: 'companion', transmissionTerm: 'عن' }),
];

const ALL_SOURCES = [
  { hadithId: 'h1', bookName: BUKHARI, hadithNumber: 1, matnArabic: MATN_BUKHARI, matnSnippet: SHORT },
  { hadithId: 'h2', bookName: MUSLIM, hadithNumber: 1907, matnArabic: MATN_MUSLIM, matnSnippet: SHORT },
  { hadithId: 'h3', bookName: ABU_DAWUD, hadithNumber: 2201, matnArabic: MATN_MUSLIM, matnSnippet: SHORT },
  // Through another Companion (علي): a witness, only when asked for with ids=...,h4.
  { hadithId: 'h4', bookName: IBN_MAJAH, hadithNumber: 4227, matnArabic: MATN_OTHER, matnSnippet: SHORT },
];

const SOURCES = ALL_SOURCES.slice(0, 3);

// `ids` selects which of the three narrations are compared: the graph keeps their books' chains.
function takhreejFor(ids) {
  const wanted = ids.length > 0 ? ALL_SOURCES.filter((src) => ids.includes(src.hadithId)) : ALL_SOURCES.slice(0, 3);
  const books = new Set(wanted.map((src) => src.bookName));
  const keep = new Set(wanted.map((src) => src.hadithId));
  const nodes = COMPARATIVE.filter((n) => n.sourceBooks.some((b) => books.has(b)) && (n.stepOrder > 0 || n.sourceHadithIds.some((h) => keep.has(h))))
    // A shared narrator only lists the books that are still compared.
    .map((n) => ({ ...n, sourceBooks: n.sourceBooks.filter((b) => books.has(b)) }));
  const ids2 = new Set(nodes.map((n) => n.id));
  return { sources: wanted, nodes: nodes.filter((n) => !n.parentNodeId || ids2.has(n.parentNodeId)), calculatedGrade: 'صحيح', taqwiyahDetails: 'تقوّى بتعدد الطرق إلى المدار.', ilalReport: ILAL_REPORT };
}

// `ids=big`: more than 12 links, so the edge labels become markers. `ids=huge`: 35 narrators, so cards go compact.
function bigTakhreej(branches = 4) {
  const base = takhreejFor([]);
  const extra = [];
  for (let k = 1; k <= branches; k += 1) {
    extra.push(
      cn(`kc${k}`, `K${k}`, `مصنف إضافي ${k}`, 0, null, ['h2'], [MUSLIM], { gradeEn: 'reliable', generationTier: 'الحادية عشرة' }),
      cn(`kn${k}`, `M${k}`, `راو إضافي ${k}`, 1, `kc${k}`, ['h2'], [MUSLIM], { gradeEn: 'reliable', generationTier: 'العاشرة' }),
      cn(`kx${k}`, 'N2', 'سفيان بن عيينة', 2, `kn${k}`, ['h2'], [MUSLIM], { ...N2[2] })
    );
  }
  return { ...base, nodes: [...base.nodes, ...extra] };
}

// Findings: [sheikh, student] for most rules, [student, sheikh] for tadlis (see getIlalEdgeDecorations).
const ILAL_REPORT = {
  analyzedHadithIds: ['h1', 'h2', 'h3'],
  turuq: SOURCES.map((s) => ({ hadithId: s.hadithId, bookName: s.bookName, hadithNumber: s.hadithNumber, isMarfu: true })),
  madars: [{ narratorId: 'N2', narratorName: 'سفيان بن عيينة', branchCount: 3, hadithIds: ['h1', 'h2', 'h3'] }],
  findings: [
    { type: 'Tadlis', severity: 'Qadihah', titleAr: 'عنعنة مدلس', evidenceAr: 'سفيان بن عيينة مدلس ورواه بالعنعنة عن الحميدي.', narratorIds: ['N1', 'N2'], hadithIds: ['h1'], confidence: 0.8 },
    { type: 'HiddenInqita', severity: 'Tanbih', titleAr: 'لم يثبت اللقاء', evidenceAr: 'لا يذكر في قوائم التهذيب لقاء بين الراويين.', narratorIds: ['N4', 'N2'], hadithIds: ['h2'], confidence: 0.2 },
  ],
  hasQadihah: true,
  summaryAr: 'وُجدت علة قادحة واحدة وتنبيه واحد.',
};

const hadith = (i, over = {}) => ({ id: `h${i}`, bookName: i % 2 ? BUKHARI : MUSLIM, hadithNumber: i, chapter: 'باب كيف كان بدء الوحي', matnArabic: i === 1 ? LONG : SHORT, matnSnippet: SHORT, relevancePercent: 90 - i, relevanceReason: 'كل الكلمات متتالية', ...over });

const routes = [
  [/^\/api\/__health$/, () => [200, { mock: true }]],
  [/^\/api\/Tree\/missing$/, () => [404, 'not found']],
  [/^\/api\/Tree\/[^/]+$/, () => [200, { hadithId: 'h1', bookName: BUKHARI, hadithNumber: 1, matnArabic: LONG, nodes: SINGLE }]],
  [/^\/api\/Takhreej\/related\/h2$/, () => [200, []]],
  [/^\/api\/Takhreej\/related\//, () => [200, [hadith(2), hadith(3)]]],
  [/^\/api\/Takhreej$/, (u) => { const ids = (u.searchParams.get('ids') ?? '').split(',').filter(Boolean); return [200, ids.includes('huge') ? bigTakhreej(14) : ids.includes('big') ? bigTakhreej() : takhreejFor(ids)]; }],
  [/^\/api\/Ilal\/explain$/, () => [200, { explanationAr: 'شرح تجريبي.', caveats: [] }]],
  [/^\/api\/Ilal(\/[^/]+)?$/, () => [200, ILAL_REPORT]],
  [/^\/api\/Narrators\/N3$/, () => [200, { id: 'N3', fullName: 'راو متروك للاختبار', knownAs: null, kunyah: null, generationTier: 'السادسة', birthYearHijri: null, deathYearHijri: 150, biography: null, gradeEn: 'fabricator', evaluations: [{ scholarName: 'يحيى بن معين', evaluationText: 'كذاب', sourceBook: 'تهذيب الكمال', verdictRating: 'fabricator' }] }]],
  [/^\/api\/Narrators\/[^/]+$/, (u) => [200, { id: u.pathname.split('/').pop(), fullName: 'عبد الله بن الزبير الحميدي', knownAs: 'الحميدي', kunyah: null, generationTier: 'العاشرة', birthYearHijri: null, deathYearHijri: 219, biography: null, gradeEn: 'reliable', evaluations: [] }]],
  [/^\/api\/Search$/, (u) => { const p = Number(u.searchParams.get('page') ?? 1); return [200, Array.from({ length: p === 1 ? 50 : 3 }, (_, i) => hadith(i + 1 + (p - 1) * 50))]; }],
  [/^\/api\/Books\/[^/]+\/chapters\/[^/]+\/hadiths$/, () => [200, [hadith(1), hadith(3)]]],
  [/^\/api\/Books\/[^/]+\/chapters$/, () => [200, ['باب كيف كان بدء الوحي', 'كتاب الإيمان', 'كتاب العلم']]],
  [/^\/api\/Verify/, () => [200, { status: 'exact', explanation: 'وجدنا النص.', method: 'lexical', matches: [{ id: 'h1', bookName: BUKHARI, hadithNumber: 1, chapter: 'بدء الوحي', matnArabic: SHORT, similarity: 1 }, { id: 'h2', bookName: MUSLIM, hadithNumber: 2, chapter: null, matnArabic: SHORT, similarity: 0.9 }] }]],
];

http
  .createServer((req, res) => {
    const u = new URL(req.url, 'http://x');
    const headers = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Content-Type': 'application/json' };
    if (req.method === 'OPTIONS') { res.writeHead(204, headers); return res.end(); }
    const route = routes.find(([re]) => re.test(u.pathname));
    const [code, body] = route ? route[1](u) : [404, 'no route'];
    res.writeHead(code, headers);
    res.end(JSON.stringify(body));
  })
  .listen(PORT, () => console.log(`mock API on ${PORT}`));
