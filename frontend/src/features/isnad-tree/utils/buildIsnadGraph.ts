import type { Edge, Node } from "@xyflow/react";
import type {
  ComparativeIsnadNodeDto,
  ComparativeTreeResponseDto,
  IsnadNodeDto,
  IsnadTreeResponseDto,
  NarratorSummaryDto,
} from "@/types/api";
import { buildEdge, type EdgeFacts } from "./edgeStyle";
import { getFamousReferenceOwnerName } from "./formatFamousReferenceName";
import { formatScholarlyNarratorName, formatTwoPartNarratorName } from "./formatNarratorName";
import type { BuiltGraph, NarratorNodeData, ReferenceNodeData } from "./graphTypes";

/**
 * The API returns a flat list of transmissions. Here narrators become nodes and links become edges:
 *   n.id            the transmission's id
 *   n.narratorId    the sheikh's narrator id (the compiler's, for the anchor at step 0)
 *   n.parentNodeId  the parent transmission, whose narrator is the student
 * The functions are pure so the graph can be tested without rendering.
 */

type Tooltips = Record<string, NarratorSummaryDto> | undefined;

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

/**
 * One edge per sheikh → student pair. The same pair in another hadith merges into the same edge: its books are added
 * (so book focus finds it and a link used by several books is drawn as shared) and a break or wording difference seen
 * on either transmission counts.
 */
function buildEdges<T extends IsnadNodeDto>(nodes: T[], factsOf: (n: T) => EdgeFacts): Edge[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const merged = new Map<string, { source: string; target: string; facts: EdgeFacts }>();

  for (const n of nodes) {
    const parent = n.parentNodeId ? byId.get(n.parentNodeId) : undefined;
    if (!parent) continue;

    const id = `e-${n.narratorId}-${parent.narratorId}`;
    const facts = factsOf(n);
    const existing = merged.get(id);
    if (!existing) {
      merged.set(id, { source: n.narratorId, target: parent.narratorId, facts });
      continue;
    }
    existing.facts = {
      isAnomaly: existing.facts.isAnomaly || facts.isAnomaly,
      hasMatnVariation: existing.facts.hasMatnVariation || facts.hasMatnVariation,
      sourceBooks: Array.from(new Set([...(existing.facts.sourceBooks ?? []), ...(facts.sourceBooks ?? [])])),
    };
  }

  return Array.from(merged, ([id, { source, target, facts }]) => buildEdge(id, source, target, facts));
}

/** Tree of one hadith. */
export function buildSingleGraph(tree: IsnadTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const nodes = new Map<string, Node>();

  for (const n of tree.nodes) {
    if (nodes.has(n.narratorId)) continue;

    if (n.stepOrder === 0) {
      const data: ReferenceNodeData = {
        famousName: getFamousReferenceOwnerName(n.narratorName, n.knownAs, tree.bookName),
        fullName: n.narratorName,
        twoPartName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
        bookName: tree.bookName,
        hadithNumber: tree.hadithNumber,
        generationTier: n.generationTier,
        gradeSummary: tooltips?.[n.narratorId]?.gradeSummary,
        gradeEn: n.gradeEn,
      };
      nodes.set(n.narratorId, { id: n.narratorId, type: "reference", position: { x: 0, y: 0 }, data });
    } else {
      const data: NarratorNodeData = {
        narratorName: formatTwoPartNarratorName(n.narratorName || n.knownAs),
        ...narratorData(n, tooltips),
      };
      nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });
    }
  }

  return {
    nodes: Array.from(nodes.values()),
    edges: buildEdges(tree.nodes, (n) => ({ isAnomaly: n.isAnomaly })),
    bookNames: [tree.bookName],
  };
}

/** Comparative tree: the chains of several hadiths merged into one graph. */
export function buildComparativeGraph(tree: ComparativeTreeResponseDto, tooltips?: Tooltips): BuiltGraph {
  const sources = tree.sources ?? [];

  // A narrator that is a compiler in any of the merged chains is drawn as a compiler card.
  const compilerEntry = new Map<string, ComparativeIsnadNodeDto>();
  for (const n of tree.nodes) {
    if (n.stepOrder === 0 && !compilerEntry.has(n.narratorId)) compilerEntry.set(n.narratorId, n);
  }

  const nodes = new Map<string, Node>();
  for (const n of tree.nodes) {
    const existing = nodes.get(n.narratorId);

    if (existing) {
      // Seen again in another chain: merge what the cards show about it.
      if (existing.type === "reference") continue;
      const data = existing.data as NarratorNodeData;
      data.sourceBooks = Array.from(new Set([...(data.sourceBooks ?? []), ...(n.sourceBooks ?? [])]));
      data.travelNote ??= n.travelNote;
      if (n.hasMatnVariation && !data.hasMatnVariation) {
        data.hasMatnVariation = true;
        data.matnVariationSnippet = n.matnVariationSnippet;
      }
      continue;
    }

    const compiler = compilerEntry.get(n.narratorId);
    if (compiler) {
      const matched = sources.filter((s) => compiler.sourceHadithIds?.includes(s.hadithId));
      const bookName = matched.map((s) => s.bookName).join(" / ") || compiler.sourceBooks?.join(" / ") || "المصدر";
      const numbers = matched.map((s) => s.hadithNumber).filter(Boolean);
      const data: ReferenceNodeData = {
        famousName: getFamousReferenceOwnerName(compiler.narratorName, compiler.knownAs, bookName),
        fullName: compiler.narratorName,
        twoPartName: formatTwoPartNarratorName(compiler.narratorName || compiler.knownAs),
        bookName,
        hadithNumber: numbers.length > 0 ? numbers.join(", ") : undefined,
        generationTier: compiler.generationTier,
        gradeSummary: tooltips?.[n.narratorId]?.gradeSummary,
        gradeEn: compiler.gradeEn,
        sourceBooks: Array.from(new Set(compiler.sourceBooks ?? [])),
      };
      nodes.set(n.narratorId, { id: n.narratorId, type: "reference", position: { x: 0, y: 0 }, data });
    } else {
      const data: NarratorNodeData = {
        narratorName: formatScholarlyNarratorName(n.narratorName, n.knownAs),
        ...narratorData(n, tooltips),
        hasMatnVariation: n.hasMatnVariation,
        matnVariationSnippet: n.matnVariationSnippet,
        sourceBooks: n.sourceBooks ?? [],
      };
      nodes.set(n.narratorId, { id: n.narratorId, type: "narrator", position: { x: 0, y: 0 }, data });
    }
  }

  return {
    nodes: Array.from(nodes.values()),
    edges: buildEdges(tree.nodes, (n) => ({
      isAnomaly: n.isAnomaly,
      hasMatnVariation: n.hasMatnVariation,
      sourceBooks: n.sourceBooks,
    })),
    bookNames: Array.from(new Set(sources.map((s) => s.bookName))),
  };
}
