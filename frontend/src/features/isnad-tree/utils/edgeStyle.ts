import { MarkerType, type Edge } from "@xyflow/react";
import { getBookMeta } from "@/lib/bookTheme";
import type { IlalEdgeDecoration } from "@/features/ilal/utils/ilalLabels";

/** What is known about one sheikh → student link, in the order the styling rules look at it. */
export interface EdgeFacts {
  isAnomaly?: boolean;
  hasMatnVariation?: boolean;
  /** Books whose chains use this link (comparative tree only). */
  sourceBooks?: string[];
}

const COLOR_DEFAULT = "#64748b";
const COLOR_SHARED = "#334155";
const COLOR_ANOMALY = "#ef4444";
const COLOR_VARIATION = "#f59e0b";

const LABEL_BG_ANOMALY = { fill: "#fef2f2", stroke: "#fca5a5", strokeWidth: 1, rx: 4, ry: 4 };
const LABEL_BG_VARIATION = { fill: "#fffbeb", stroke: "#fcd34d", strokeWidth: 1, rx: 4, ry: 4 };

/** Above this many edges the labels would crowd the graph: they become markers until a chain is focused. */
export const COMPACT_LABELS_MIN_EDGES = 12;

/** What an edge's label says, for the marker's icon. */
export type EdgeKind = "anomaly" | "variation" | "ilal";

const marker = (color: string) => ({ type: MarkerType.ArrowClosed, width: 20, height: 20, color });

/**
 * Builds the edge for one link. The first matching rule wins:
 *   1. انقطاع (red, dashed, labelled)   2. اختلاف باللفظ (amber, dashed, labelled)
 *   3. a link shared by several books (dark, thick)   4. a link of one book (that book's colour)   5. default grey.
 * Ilal findings are laid over this afterwards by `decorateEdgeWithIlal`.
 */
export function buildEdge(id: string, source: string, target: string, facts: EdgeFacts): Edge {
  const books = facts.sourceBooks ?? [];
  let color = COLOR_DEFAULT;
  let strokeWidth = 2;
  let label: string | undefined;
  let kind: EdgeKind | undefined;
  let labelBg: typeof LABEL_BG_ANOMALY | undefined;

  if (facts.isAnomaly) {
    color = COLOR_ANOMALY;
    strokeWidth = 3;
    label = "انقطاع";
    kind = "anomaly";
    labelBg = LABEL_BG_ANOMALY;
  } else if (facts.hasMatnVariation) {
    color = COLOR_VARIATION;
    strokeWidth = 3;
    label = "اختلاف باللفظ";
    kind = "variation";
    labelBg = LABEL_BG_VARIATION;
  } else if (books.length === 1) {
    color = getBookMeta(books[0]).color;
  } else if (books.length > 1) {
    color = COLOR_SHARED;
    strokeWidth = 3;
  }

  const flagged = Boolean(facts.isAnomaly || facts.hasMatnVariation);
  return {
    id,
    source, // sheikh
    target, // student
    type: "bezier",
    markerEnd: marker(color),
    style: { stroke: color, strokeWidth, strokeDasharray: flagged ? "5 5" : undefined },
    animated: flagged,
    label,
    labelStyle: { fill: color, fontWeight: "bold", fontSize: 11 },
    labelBgStyle: labelBg,
    labelBgPadding: [4, 8],
    data: { books: [...books], isAnomaly: Boolean(facts.isAnomaly), kind },
  };
}

/**
 * Lays an Ilal finding (tadlis, unproven meeting, ikhtilat) over an edge.
 * A broken link stays red, because a break outweighs a hint, and shows both labels; any other edge takes the
 * finding's colour and dash.
 */
export function decorateEdgeWithIlal(edge: Edge, decoration: IlalEdgeDecoration): Edge {
  const label = edge.label ? `${edge.label} · ${decoration.label}` : decoration.label;
  if (edge.data?.isAnomaly) {
    return { ...edge, animated: true, label };
  }
  return {
    ...edge,
    animated: true,
    label,
    data: { ...edge.data, kind: "ilal" satisfies EdgeKind },
    style: { ...edge.style, stroke: decoration.color, strokeWidth: 3, strokeDasharray: decoration.dash },
    markerEnd: marker(decoration.color),
    labelStyle: { fill: decoration.color, fontWeight: "bold", fontSize: 11 },
    labelBgStyle: { fill: "#fff7ed", stroke: decoration.color, strokeWidth: 1, rx: 4, ry: 4 },
    labelBgPadding: [4, 8],
  };
}
