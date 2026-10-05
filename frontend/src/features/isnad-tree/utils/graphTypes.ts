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

/** Data of the compiler card at the bottom of a chain. */
export type ReferenceNodeData = {
  famousName: string;
  fullName?: string;
  twoPartName?: string;
  bookName: string;
  hadithNumber?: number | string;
  generationTier?: string | null;
  gradeSummary?: string;
  gradeEn?: string;
  sourceBooks?: string[];
  isSelected?: boolean;
};

/** Nodes and edges ready for layout, with the books the graph covers (for the legend). */
export interface BuiltGraph {
  nodes: Node[];
  edges: Edge[];
  bookNames: string[];
}
