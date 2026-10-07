import { test, expect } from '@playwright/test';
import { buildComparativeGraph, buildSingleGraph } from '../src/features/isnad-tree/utils/buildIsnadGraph';
import { getFamousReferenceOwnerName } from '../src/features/isnad-tree/utils/formatFamousReferenceName';
import { decorateEdgeWithIlal } from '../src/features/isnad-tree/utils/edgeStyle';
import { getIlalEdgeDecorations } from '../src/features/ilal/utils/ilalLabels';
import { normalizeArabic, searchNarrators } from '../src/features/isnad-tree/utils/narratorSearch';
import { COMPACT_NODES_MIN, isCompactDensity } from '../src/features/isnad-tree/store/useGraphViewStore';
import { takhreejTitle } from '../src/features/isnad-tree/utils/takhreejTitle';
import { isUnrated } from '../src/features/narrator-details/utils/gradeStyle';
import type { ComparativeIsnadNodeDto, ComparativeTreeResponseDto, IlalReportDto, IsnadNodeDto } from '../src/types/api';

const node = (over: Partial<IsnadNodeDto> & Pick<IsnadNodeDto, 'id' | 'narratorId' | 'stepOrder' | 'parentNodeId'>): IsnadNodeDto => ({
  narratorName: `name-${over.narratorId}`,
  knownAs: null,
  generationTier: null,
  transmissionTerm: null,
  ...over,
});

const cnode = (
  over: Partial<ComparativeIsnadNodeDto> & Pick<ComparativeIsnadNodeDto, 'id' | 'narratorId' | 'stepOrder' | 'parentNodeId'>
): ComparativeIsnadNodeDto => ({ ...node(over), sourceHadithIds: [], sourceBooks: [], ...over });

// Compiler C <- N1 <- N2 (N2 is the sheikh of N1, who is the sheikh of C).
const chain: IsnadNodeDto[] = [
  node({ id: 't0', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
  node({ id: 't1', narratorId: 'N1', stepOrder: 1, parentNodeId: 't0', transmissionTerm: 'حدثنا' }),
  node({ id: 't2', narratorId: 'N2', stepOrder: 2, parentNodeId: 't1', isAnomaly: true, anomalyReason: 'x' }),
];

test.describe('buildSingleGraph', () => {
  const graph = buildSingleGraph({ hadithId: 'h', bookName: 'صحيح البخاري', hadithNumber: 1, matnArabic: '', nodes: chain });

  test('every chain narrator is a narrator card; the book gets its own source card', () => {
    expect(graph.nodes.map((n) => [n.id, n.type])).toEqual([
      ['C', 'narrator'],
      ['ref-صحيح البخاري', 'reference'],
      ['N1', 'narrator'],
      ['N2', 'narrator'],
    ]);
    expect((graph.nodes.find((n) => n.id === 'ref-صحيح البخاري')!.data as { famousName: string }).famousName).toBe('البخاري');
    expect(graph.bookNames).toEqual(['صحيح البخاري']);
  });

  test('edges run from the sheikh to the student', () => {
    expect(graph.edges.map((e) => [e.source, e.target])).toEqual([
      ['N1', 'C'],
      ['N2', 'N1'],
      ['C', 'ref-صحيح البخاري'],
    ]);
  });

  test('a broken link is red, dashed and labelled; a normal one is grey', () => {
    const [normal, broken] = graph.edges; // the source edge comes last
    expect(normal.style?.stroke).toBe('#64748b');
    expect(normal.label).toBeUndefined();
    expect(broken.style?.stroke).toBe('#ef4444');
    expect(broken.style?.strokeDasharray).toBe('5 5');
    expect(broken.label).toBe('انقطاع');
  });

  test('a transmission whose parent is missing makes no edge', () => {
    const g = buildSingleGraph({
      hadithId: 'h', bookName: 'b', hadithNumber: 1, matnArabic: '',
      nodes: [node({ id: 'a', narratorId: 'A', stepOrder: 1, parentNodeId: 'gone' })],
    });
    expect(g.edges).toHaveLength(0);
    expect(g.nodes).toHaveLength(1);
    expect(g.nodes[0].type).toBe('narrator');
  });
});

test.describe('a first narrator who is the compiler himself', () => {
  const BUKHARI = 'محمد بن إسماعيل بن إبراهيم بن المغيرة ابن بذدزبة';
  const compilerChain: IsnadNodeDto[] = [
    node({ id: 't0', narratorId: 'B', stepOrder: 0, parentNodeId: null, narratorName: BUKHARI }),
    node({ id: 't1', narratorId: 'N1', stepOrder: 1, parentNodeId: 't0' }),
  ];

  test('is not drawn as a narrator: the source card stands in his place, the students link to it', () => {
    const g = buildSingleGraph({ hadithId: 'h', bookName: 'صحيح البخاري', hadithNumber: 1, matnArabic: '', nodes: compilerChain });
    expect(g.nodes.map((n) => [n.id, n.type])).toEqual([['ref-صحيح البخاري', 'reference'], ['N1', 'narrator']]);
    expect(g.edges.map((e) => [e.source, e.target])).toEqual([['N1', 'ref-صحيح البخاري']]);
  });

  test('the same name in another book is only a narrator', () => {
    const g = buildSingleGraph({ hadithId: 'h', bookName: 'صحيح مسلم', hadithNumber: 1, matnArabic: '', nodes: compilerChain });
    expect(g.nodes.map((n) => [n.id, n.type])).toEqual([['B', 'narrator'], ['ref-صحيح مسلم', 'reference'], ['N1', 'narrator']]);
  });

  test('comparative: one source card for the compiler and for a first narrator who is only his sheikh', () => {
    const sources = [
      { hadithId: 'h1', bookName: 'صحيح البخاري', hadithNumber: 1, matnSnippet: '' },
      { hadithId: 'h2', bookName: 'صحيح البخاري', hadithNumber: 2, matnSnippet: '' },
    ];
    const g = buildComparativeGraph({
      sources,
      nodes: [
        cnode({ id: 'a0', narratorId: 'B', stepOrder: 0, parentNodeId: null, narratorName: BUKHARI, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
        cnode({ id: 'a1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'a0', sourceBooks: ['صحيح البخاري'] }),
        cnode({ id: 'b0', narratorId: 'S', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح البخاري'] }),
      ],
    } as ComparativeTreeResponseDto);
    expect(g.nodes.map((n) => [n.id, n.type]).sort()).toEqual([['N1', 'narrator'], ['S', 'narrator'], ['ref-صحيح البخاري', 'reference']]);
    expect(g.edges.map((e) => [e.source, e.target]).sort()).toEqual([['N1', 'ref-صحيح البخاري'], ['S', 'ref-صحيح البخاري']]);
    expect((g.nodes.find((n) => n.type === 'reference')!.data as { hadithNumber: string }).hadithNumber).toBe('1, 2');
  });

  test('a compiler who is also an intermediate sheikh in another book keeps his narrator card there', () => {
    const sources = [
      { hadithId: 'h1', bookName: 'صحيح البخاري', hadithNumber: 1, matnSnippet: '' },
      { hadithId: 'h2', bookName: 'صحيح مسلم', hadithNumber: 2, matnSnippet: '' },
    ];
    const g = buildComparativeGraph({
      sources,
      nodes: [
        cnode({ id: 'a0', narratorId: 'B', stepOrder: 0, parentNodeId: null, narratorName: BUKHARI, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
        cnode({ id: 'b0', narratorId: 'M', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح مسلم'] }),
        cnode({ id: 'b1', narratorId: 'B', stepOrder: 1, parentNodeId: 'b0', narratorName: BUKHARI, sourceBooks: ['صحيح مسلم'] }),
      ],
    } as ComparativeTreeResponseDto);
    expect(g.nodes.find((n) => n.id === 'B')!.type).toBe('narrator');
    expect(g.edges.map((e) => [e.source, e.target]).sort()).toEqual([['B', 'M'], ['M', 'ref-صحيح مسلم']]);
  });
});

test.describe('buildComparativeGraph', () => {
  const sources = [
    { hadithId: 'h1', bookName: 'صحيح البخاري', hadithNumber: 1, matnSnippet: '' },
    { hadithId: 'h2', bookName: 'صحيح مسلم', hadithNumber: 2, matnSnippet: '' },
  ];
  const build = (nodes: ComparativeIsnadNodeDto[]) =>
    buildComparativeGraph({ sources, nodes } as ComparativeTreeResponseDto);

  test('the same narrator pair in two hadiths is one edge with both books', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'a1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'a0', sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'b0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح مسلم'] }),
      cnode({ id: 'b1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'b0', sourceBooks: ['صحيح مسلم'] }),
    ]);
    expect(g.nodes.filter((n) => n.type === 'narrator')).toHaveLength(2);
    expect(g.nodes.filter((n) => n.type === 'reference')).toHaveLength(2); // one source card per book
    expect(g.edges).toHaveLength(3); // C → N1 shared, plus one source edge per book
    expect(g.edges[0].data?.books).toEqual(['صحيح البخاري', 'صحيح مسلم']);
    expect(g.bookNames).toEqual(['صحيح البخاري', 'صحيح مسلم']);
    // Shared by two books: dark and thick.
    expect(g.edges[0].style?.stroke).toBe('#334155');
    expect(g.edges[0].style?.strokeWidth).toBe(3);
    expect((g.nodes.find((n) => n.id === 'N1')!.data as { sourceBooks: string[] }).sourceBooks).toEqual([
      'صحيح البخاري',
      'صحيح مسلم',
    ]);
  });

  test('a link used by one book takes that book\'s colour', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'a1', narratorId: 'N1', stepOrder: 1, parentNodeId: 'a0', sourceBooks: ['صحيح البخاري'] }),
    ]);
    expect(g.edges[0].style?.stroke).toBe('#2563eb');
  });

  test('rule order: broken beats wording difference beats book colour', () => {
    const base = { stepOrder: 1, sourceBooks: ['صحيح البخاري'] };
    const g = build([
      cnode({ id: 'r', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'x', narratorId: 'X', parentNodeId: 'r', ...base, isAnomaly: true, hasMatnVariation: true }),
      cnode({ id: 'y', narratorId: 'Y', parentNodeId: 'r', ...base, hasMatnVariation: true }),
    ]);
    const x = g.edges.find((e) => e.source === 'X')!;
    const y = g.edges.find((e) => e.source === 'Y')!;
    expect([x.style?.stroke, x.label]).toEqual(['#ef4444', 'انقطاع']);
    expect([y.style?.stroke, y.label]).toEqual(['#f59e0b', 'اختلاف باللفظ']);
  });

  test('a first narrator who is also an intermediate sheikh stays one narrator card, with a source card under the first', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
      cnode({ id: 'b0', narratorId: 'D', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h2'], sourceBooks: ['صحيح مسلم'] }),
      cnode({ id: 'b1', narratorId: 'C', stepOrder: 1, parentNodeId: 'b0', sourceBooks: ['صحيح مسلم'] }),
    ]);
    expect(g.nodes.find((n) => n.id === 'C')!.type).toBe('narrator');
    expect(g.edges.map((e) => [e.source, e.target])).toEqual([
      ['C', 'D'],
      ['C', 'ref-صحيح البخاري'],
      ['D', 'ref-صحيح مسلم'],
    ]);
  });

  test('two different narrators with the same short name are told apart by their grandfather', () => {
    const g = build([
      cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'] }),
      cnode({ id: 'a1', narratorId: 'Y1', stepOrder: 1, parentNodeId: 'a0', narratorName: 'يحيى بن سعيد بن قيس بن عمرو الأنصاري' }),
      cnode({ id: 'a2', narratorId: 'Y2', stepOrder: 1, parentNodeId: 'a0', narratorName: 'يحيى بن سعيد بن فروخ القطان' }),
      cnode({ id: 'a3', narratorId: 'Z', stepOrder: 1, parentNodeId: 'a0', narratorName: 'سفيان بن عيينة بن أبي عمران' }),
    ]);
    const name = (id: string) => (g.nodes.find((n) => n.id === id)!.data as { narratorName: string }).narratorName;
    expect([name('Y1'), name('Y2'), name('Z')]).toEqual(['يحيى بن سعيد بن قيس', 'يحيى بن سعيد بن فروخ', 'سفيان بن عيينة']);
  });

  test('a book has one source card, whatever the number of its hadiths and chains, linked to each first narrator', () => {
    const more = [...sources, { hadithId: 'h3', bookName: 'صحيح البخاري', hadithNumber: 3, matnSnippet: '' }];
    const g = buildComparativeGraph({
      sources: more,
      nodes: [
        cnode({ id: 'a0', narratorId: 'C', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1'], sourceBooks: ['صحيح البخاري'] }),
        cnode({ id: 'b0', narratorId: 'D', stepOrder: 0, parentNodeId: null, sourceHadithIds: ['h1', 'h3'], sourceBooks: ['صحيح البخاري'] }),
      ],
    } as ComparativeTreeResponseDto);
    const refs = g.nodes.filter((n) => n.type === 'reference');
    expect(refs.map((n) => [n.id, (n.data as { hadithNumber: string }).hadithNumber])).toEqual([['ref-صحيح البخاري', '1, 3']]);
    expect(g.edges.map((e) => [e.source, e.target])).toEqual([['C', 'ref-صحيح البخاري'], ['D', 'ref-صحيح البخاري']]);
  });

  test('variation and travel hints seen on a later transmission are kept on the card', () => {
    const g = build([
      cnode({ id: 'r', narratorId: 'C', stepOrder: 0, parentNodeId: null }),
      cnode({ id: 'a', narratorId: 'N', stepOrder: 1, parentNodeId: 'r' }),
      cnode({ id: 'b', narratorId: 'N', stepOrder: 1, parentNodeId: 'r', hasMatnVariation: true, matnVariationSnippet: 's', travelNote: 't' }),
    ]);
    const data = g.nodes.find((n) => n.id === 'N')!.data as { hasMatnVariation?: boolean; matnVariationSnippet?: string; travelNote?: string };
    expect([data.hasMatnVariation, data.matnVariationSnippet, data.travelNote]).toEqual([true, 's', 't']);
  });
});

test.describe('source card names', () => {
  test('named after the compiler in the book title, never after the first narrator', () => {
    const names = ['المعجم الأوسط للطبراني', 'السنن الكبرى للبيهقي', 'صحيح البخاري', 'كتاب غير معروف'].map(getFamousReferenceOwnerName);
    expect(names).toEqual(['الطبراني', 'البيهقي', 'البخاري', 'كتاب غير معروف']);
  });
});

test.describe('decorateEdgeWithIlal', () => {
  const deco = { label: 'عنعنة مدلس', color: '#ea580c', dash: '2 4' };
  const single = buildSingleGraph({ hadithId: 'h', bookName: 'b', hadithNumber: 1, matnArabic: '', nodes: chain });

  test('an ordinary edge takes the finding\'s colour, dash and label', () => {
    const out = decorateEdgeWithIlal(single.edges[0], deco);
    expect(out.style?.stroke).toBe('#ea580c');
    expect(out.style?.strokeDasharray).toBe('2 4');
    expect(out.label).toBe('عنعنة مدلس');
    expect(out.animated).toBe(true);
  });

  test('a broken edge stays red and shows both labels', () => {
    const out = decorateEdgeWithIlal(single.edges[1], deco);
    expect(out.style?.stroke).toBe('#ef4444');
    expect(out.label).toBe('انقطاع · عنعنة مدلس');
  });
});

test.describe('edge kinds', () => {
  test('each marker kind follows the rule that styled the edge', () => {
    const g = buildSingleGraph({ hadithId: 'h', bookName: 'b', hadithNumber: 1, matnArabic: '', nodes: chain });
    expect(g.edges.map((e) => e.data?.kind)).toEqual([undefined, 'anomaly', undefined]);
    expect(decorateEdgeWithIlal(g.edges[0], { label: 'x', color: '#000', dash: '1' }).data?.kind).toBe('ilal');
    // A broken link keeps its own kind when a finding lands on it.
    expect(decorateEdgeWithIlal(g.edges[1], { label: 'x', color: '#000', dash: '1' }).data?.kind).toBe('anomaly');
  });
});

test.describe('getIlalEdgeDecorations', () => {
  const finding = (type: 'Tadlis' | 'HiddenInqita', severity: 'Qadihah' | 'Tanbih', confidence: number, narratorIds: string[]) => ({
    type, severity, confidence, narratorIds, hadithIds: [], titleAr: '', evidenceAr: '',
  });
  const report = {
    analyzedHadithIds: [], turuq: [], madars: [], hasQadihah: true, summaryAr: '',
    findings: [
      finding('Tadlis', 'Qadihah', 0.8, ['STUDENT', 'SHEIKH']), // tadlis lists [student, sheikh]
      finding('HiddenInqita', 'Tanbih', 0.2, ['SHEIKH2', 'STUDENT2']), // low confidence: a data gap
    ],
  } as IlalReportDto;

  test('maps a finding to the sheikh → student edge, whatever the order the rule lists them in', () => {
    expect([...getIlalEdgeDecorations(report).keys()]).toEqual(['e-SHEIKH-STUDENT']);
  });

  test('low-confidence findings are drawn only when asked', () => {
    expect([...getIlalEdgeDecorations(report, true).keys()]).toEqual(['e-SHEIKH-STUDENT', 'e-SHEIKH2-STUDENT2']);
  });

  test('null report: nothing', () => {
    expect(getIlalEdgeDecorations(null).size).toBe(0);
  });
});

test.describe('isUnrated', () => {
  test('no grade, an empty one or a code the app does not know is "no verdict"', () => {
    expect([undefined, null, '', 'something-new'].map(isUnrated)).toEqual([true, true, true, true]);
  });
  test('every known grade, including «مجهول», is a verdict', () => {
    expect(['reliable', 'weak', 'unknown', 'fabricator', 'companion'].map(isUnrated)).toEqual([false, false, false, false, false]);
  });
});

test.describe('takhreejTitle', () => {
  const src = (bookName: string, hadithNumber: number) => ({ hadithId: bookName, bookName, hadithNumber, matnSnippet: '' });
  test('names the first narration and how many others are compared', () => {
    expect(takhreejTitle([src('صحيح البخاري', 1)])).toBe('تخريج صحيح البخاري 1');
    expect(takhreejTitle([src('صحيح البخاري', 1), src('صحيح مسلم', 2)])).toBe('تخريج صحيح البخاري 1 ورواية أخرى');
    expect(takhreejTitle([src('صحيح البخاري', 1), src('a', 1), src('b', 1)])).toBe('تخريج صحيح البخاري 1 و2 روايات أخرى');
  });
  test('has a fallback for no sources', () => {
    expect(takhreejTitle([])).toBe('شجرة التخريج المقارنة');
  });
});

test.describe('narrator search', () => {
  const items = [
    { id: '1', name: 'سفيان بن عيينة' },
    { id: '2', name: 'سفيان الثوري' },
    { id: '3', name: 'عبد الله بن سفيان' },
    { id: '4', name: 'أبو هريرة', shortName: 'أبو هريرة' },
  ];

  test('ignores diacritics, hamza and taa marbuta', () => {
    expect(normalizeArabic('أَبُو هُرَيْرَة')).toBe(normalizeArabic('ابو هريره'));
    expect(searchNarrators(items, 'ابو هريره').map((i) => i.id)).toEqual(['4']);
  });

  test('every word must match; names starting with the query come first', () => {
    expect(searchNarrators(items, 'سفيان').map((i) => i.id)).toEqual(['1', '2', '3']);
    expect(searchNarrators(items, 'سفيان عيينة').map((i) => i.id)).toEqual(['1']);
    // The phrase as typed beats names that merely contain both words.
    expect(searchNarrators(items, 'بن سفيان').map((i) => i.id)).toEqual(['3', '1']);
  });

  test('nothing for an empty query or no match; the limit holds', () => {
    expect(searchNarrators(items, '  ')).toEqual([]);
    expect(searchNarrators(items, 'زيد')).toEqual([]);
    expect(searchNarrators(items, 'سفيان', 2)).toHaveLength(2);
  });
});

test.describe('card density', () => {
  test('automatic is compact only above the threshold; a choice always wins', () => {
    expect(isCompactDensity('auto', COMPACT_NODES_MIN)).toBe(false);
    expect(isCompactDensity('auto', COMPACT_NODES_MIN + 1)).toBe(true);
    expect(isCompactDensity('compact', 3)).toBe(true);
    expect(isCompactDensity('detailed', 500)).toBe(false);
  });
});
