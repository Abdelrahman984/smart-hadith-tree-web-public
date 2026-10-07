import type { Edge, Node } from "@xyflow/react";

/** Data of a narrator card (single tree and comparative tree). Comparative-only fields are optional. */
export type NarratorNodeData = {
  narratorName: string;
  fullName?: string;
  generationTier: string | null;
  transmissionTerm: string | null;
  gradeSummary?: string;
  gradeEn?: string;
  isAnomaly?: boolean;
  anomalyReason?: string;
  travelNote?: string;
  isMudallis?: boolean;
  hasMukhtalit?: boolean;
  residencePlaces?: string | null;
  deathPlace?: string | null;
  gawamiRank?: string | null;
  totalNarrationsCount?: number | null;
  uniqueHadithCount?: number | null;
  /** Books whose chains pass through this narrator (comparative tree only). */
  sourceBooks?: string[];
  hasMatnVariation?: boolean;
  matnVariationSnippet?: string;
};

/** Data of a source card: a book's hadith, drawn below the first narrator of its chain. */
export type ReferenceNodeData = {
  famousName: string;
  bookName: string;
  hadithNumber?: number | string;
  sourceBooks?: string[];
};

/** Nodes and edges ready for layout, with the books the graph covers (for the legend). */
export interface BuiltGraph {
  nodes: Node[];
  edges: Edge[];
  bookNames: string[];
}
