import type { Edge, Node } from "@xyflow/react";
import type {
  ComparativeIsnadNodeDto,
  ComparativeTreeResponseDto,
  IsnadNodeDto,
  IsnadTreeResponseDto,
  NarratorSummaryDto,
} from "@/types/api";
import { isCompilerOf } from "./bookCompilers";
import { buildEdge, type EdgeFacts } from "./edgeStyle";
import { getFamousReferenceOwnerName } from "./formatFamousReferenceName";
import { disambiguateNarratorNames, formatScholarlyNarratorName, formatTwoPartNarratorName } from "./formatNarratorName";
import type { BuiltGraph, NarratorNodeData, ReferenceNodeData } from "./graphTypes";

/**
 * The API returns a flat list of transmissions. Here narrators become nodes and links become edges:
 *   n.id            the transmission's id
 *   n.narratorId    the sheikh's narrator id (the compiler's, for the anchor at step 0)
 *   n.parentNodeId  the parent transmission, whose narrator is the student
 * A book also gets a source card (book, hadith numbers, compiler's name). A chain's first narrator who is the compiler
 * himself is not drawn as a narrator: that card stands in his place, so one person is never drawn twice in a row. A first
 * narrator who is only the compiler's sheikh keeps his card, and the source card hangs below it.
 * The functions are pure so the graph can be tested without rendering.
 */

type Tooltips = Record<string, NarratorSummaryDto> | undefined;
type Row = IsnadNodeDto & { sourceBooks?: string[] };

/** Narrator card data shared by both trees. */
function narratorData(n: IsnadNodeDto, tooltips: Tooltips): Omit<NarratorNodeData, "narratorName"> {
  return {
    fullName: n.narratorName,
    generationTier: n.generationTier,
    transmissionTerm: n.transmissionTerm,
    gradeSummary: tooltips?.[n.narratorId]?.gradeSummary,
    gradeEn: n.gradeEn,
    isAnomaly: n.isAnomaly,
    anomalyReason: n.anomalyReason,
    travelNote: n.travelNote,
    isMudallis: n.isMudallis,
    hasMukhtalit: n.hasMukhtalit,
    residencePlaces: n.residencePlaces,
    deathPlace: n.deathPlace,
    gawamiRank: n.gawamiRank,
    totalNarrationsCount: n.totalNarrationsCount,
    uniqueHadithCount: n.uniqueHadithCount,
  };
}

const refId = (bookName: string) => `ref-${bookName}`;

const booksOf = (row: Row, fallbackBooks: string[]) =>
  row.sourceBooks && row.sourceBooks.length > 0 ? row.sourceBooks : fallbackBooks;

/** The books of a chain's first narrator in which he is the compiler himself. */
function compilerBooks(row: Row, fallbackBooks: string[]): string[] {
  return row.stepOrder === 0 ? booksOf(row, fallbackBooks).filter((book) => isCompilerOf(row.narratorName, book)) : [];
}

/** A first narrator who is the compiler in every one of his books needs no narrator card. */
function isOnlyCompiler(row: Row, fallbackBooks: string[]): boolean {
  const books = booksOf(row, fallbackBooks);
  return row.stepOrder === 0 && books.length > 0 && compilerBooks(row, fallbackBooks).length === books.length;
}

/** Where the link from a narrator to the narrator he narrated to (the parent row) ends: one or more cards. */
type Route = { target: string; book?: string };

function routeToParent(fallbackBooks: string[]) {
  return (parent: Row, child: Row): Route[] => {
    const parentBooks = compilerBooks(parent, fallbackBooks);
    if (parentBooks.length === 0) return [{ target: parent.narratorId }];

    const childBooks = booksOf(child, fallbackBooks);
    const shared = childBooks.length > 0 ? parentBooks.filter((b) => childBooks.includes(b)) : parentBooks;
    const routes: Route[] = (shared.length > 0 ? shared : parentBooks).map((book) => ({ target: refId(book), book }));
    // A book of this link in which he is not the compiler still goes through his own card.
    if (!isOnlyCompiler(parent, fallbackBooks) && childBooks.some((b) => !parentBooks.includes(b))) {
      routes.push({ target: parent.narratorId });
    }
    return routes;
  };
}

/**
 * One edge per sheikh → student pair. The same pair in another hadith merges into the same edge: its books are added
 * (so book focus finds it and a link used by several books is drawn as shared) and a break or wording difference seen
 * on either transmission counts. A link into a source card belongs to that book alone.
 */
function buildEdges<T extends Row>(nodes: T[], factsOf: (n: T) => EdgeFacts, routeOf: (parent: T, child: T) => Route[]): Edge[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const merged = new Map<string, { source: string; target: string; facts: EdgeFacts }>();

  for (const n of nodes) {
    const parent = n.parentNodeId ? byId.get(n.parentNodeId) : undefined;
    if (!parent) continue;

    for (const { target, book } of routeOf(parent, n)) {
      const id = `e-${n.narratorId}-${target}`;
      const facts = book ? { ...factsOf(n), sourceBooks: [book] } : factsOf(n);
      const existing = merged.get(id);
      if (!existing) {
        merged.set(id, { source: n.narratorId, target, facts });
        continue;
      }
      existing.facts = {
        isAnomaly: existing.facts.isAnomaly || facts.isAnomaly,
        hasMatnVariation: existing.facts.hasMatnVariation || facts.hasMatnVariation,
        sourceBooks: Array.from(new Set([...(existing.facts.sourceBooks ?? []), ...(facts.sourceBooks ?? [])])),
      };
    }
  }

  return Array.from(merged, ([id, { source, target, facts }]) => buildEdge(id, source, target, facts));
}

/** The source card of a book: the book and its hadith numbers, named after its compiler. */
function referenceNode(id: string, bookName: string, hadithNumber: ReferenceNodeData["hadithNumber"], sourceBooks?: string[]): Node {
  const data: ReferenceNodeData = {
    famousName: getFamousReferenceOwnerName(bookName),
    bookName,
    hadithNumber,
    sourceBooks,
  };
  return { id, type: "reference", position: { x: 0, y: 0 }, data };
}

/** The data of the narrator cards, so names can be adjusted in place. */
function narratorCards(nodes: Map<string, Node>): NarratorNodeData[] {
  return Array.from(nodes.values())
    .filter((n) => n.type === "narrator")
    .map((n) => n.data as NarratorNodeData);
}

/** Tree of one hadith. */
export function buildSingleGraph(tree: IsnadTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const books = [tree.bookName];
  const nodes = new Map<string, Node>();
  const edges = buildEdges<IsnadNodeDto>(tree.nodes, (n) => ({ isAnomaly: n.isAnomaly }), routeToParent(books));

  for (const n of tree.nodes) {
    const compilerOnly = isOnlyCompiler(n, books);
    if (!compilerOnly && !nodes.has(n.narratorId)) {
      const data: NarratorNodeData = {
        narratorName: n.mentionedName || formatTwoPartNarratorName(n.narratorName || n.knownAs),
        ...narratorData(n, tooltips),
      };
      nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });
    }

    if (n.stepOrder === 0) {
      const id = refId(tree.bookName);
      if (!nodes.has(id)) nodes.set(id, referenceNode(id, tree.bookName, tree.hadithNumber));
      if (!compilerOnly) edges.push(buildEdge(`e-${n.narratorId}-${id}`, n.narratorId, id, {}));
    }
  }

  disambiguateNarratorNames(narratorCards(nodes));
  return { nodes: Array.from(nodes.values()), edges, bookNames: [tree.bookName] };
}

/** Comparative tree: the chains of several hadiths merged into one graph. */
export function buildComparativeGraph(tree: ComparativeTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const sources = tree.sources ?? [];
  const edges = buildEdges<ComparativeIsnadNodeDto>(
    tree.nodes,
    (n) => ({ isAnomaly: n.isAnomaly, hasMatnVariation: n.hasMatnVariation, sourceBooks: n.sourceBooks }),
    routeToParent([])
  );

  const nodes = new Map<string, Node>();
  // One source card per book, listing its hadiths. It hangs below every first narrator who is not the compiler himself;
  // the compiler's own students link to it directly.
  const references = new Map<string, { bookName: string; numbers: Set<string | number>; narratorIds: Set<string> }>();

  for (const n of tree.nodes) {
    if (n.stepOrder === 0) {
      const own = sources.filter((s) => n.sourceHadithIds?.includes(s.hadithId));
      const found = own.length > 0
        ? own.map((s) => ({ bookName: s.bookName, hadithNumber: s.hadithNumber }))
        : (n.sourceBooks ?? []).map((bookName) => ({ bookName, hadithNumber: undefined }));
      for (const f of found) {
        const key = refId(f.bookName);
        const ref = references.get(key) ?? { bookName: f.bookName, numbers: new Set(), narratorIds: new Set<string>() };
        if (f.hadithNumber) ref.numbers.add(f.hadithNumber);
        if (!isCompilerOf(n.narratorName, f.bookName)) ref.narratorIds.add(n.narratorId);
        references.set(key, ref);
      }
    }
    if (isOnlyCompiler(n, [])) continue;

    const existing = nodes.get(n.narratorId);
    if (existing) {
      // Seen again in another chain: merge what the cards show about it.
      const data = existing.data as NarratorNodeData;
      data.sourceBooks = Array.from(new Set([...(data.sourceBooks ?? []), ...(n.sourceBooks ?? [])]));
      data.travelNote ??= n.travelNote;
      if (n.hasMatnVariation && !data.hasMatnVariation) {
        data.hasMatnVariation = true;
        data.matnVariationSnippet = n.matnVariationSnippet;
      }
      continue;
    }

    const data: NarratorNodeData = {
      narratorName: n.mentionedName || formatScholarlyNarratorName(n.narratorName, n.knownAs),
      ...narratorData(n, tooltips),
      hasMatnVariation: n.hasMatnVariation,
      matnVariationSnippet: n.matnVariationSnippet,
      sourceBooks: n.sourceBooks ?? [],
    };
    nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });
  }

  for (const [id, { bookName, numbers, narratorIds }] of references) {
    const hadithNumber = numbers.size > 0 ? Array.from(numbers).sort((a, b) => Number(a) - Number(b)).join(", ") : undefined;
    nodes.set(id, referenceNode(id, bookName, hadithNumber, [bookName]));
    for (const narratorId of narratorIds) {
      edges.push(buildEdge(`e-${narratorId}-${id}`, narratorId, id, { sourceBooks: [bookName] }));
    }
  }

  disambiguateNarratorNames(narratorCards(nodes));

  return {
    nodes: Array.from(nodes.values()),
    edges,
    bookNames: Array.from(new Set(sources.map((s) => s.bookName))),
  };
}
