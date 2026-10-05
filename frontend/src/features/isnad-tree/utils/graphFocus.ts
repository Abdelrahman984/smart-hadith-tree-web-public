import { Node, Edge } from "@xyflow/react";
import { getBookMeta } from "@/lib/bookTheme";

export interface GraphFocus {
  /** Narrator whose full chain (all sheikhs above, all students below) is highlighted. */
  nodeId?: string | null;
  /** Canonical book name whose edges are highlighted. */
  book?: string | null;
}

const DIM_EDGE_OPACITY = 0.07;
const DIM_NODE_OPACITY = 0.25;

/** Collects edges reachable from `start` following `next` (edge -> next node id) repeatedly. */
function traceEdges(
  start: string,
  edgesByNode: Map<string, Edge[]>,
  next: (edge: Edge) => string
): Set<string> {
  const edgeIds = new Set<string>();
  const seen = new Set<string>([start]);
  const queue = [start];
  while (queue.length > 0) {
    const current = queue.pop()!;
    for (const edge of edgesByNode.get(current) ?? []) {
      edgeIds.add(edge.id);
      const other = next(edge);
      if (!seen.has(other)) {
        seen.add(other);
        queue.push(other);
      }
    }
  }
  return edgeIds;
}

/**
 * Dims everything except the focused narrator's chain or the focused book's edges, and
 * raises the highlighted edges above the rest. Returns the inputs untouched when no focus is set.
 * Edges point sheikh (source) -> student (target).
 */
export function applyGraphFocus(nodes: Node[], edges: Edge[], focus: GraphFocus) {
  const { nodeId, book } = focus;
  if (!nodeId && !book) return { nodes, edges };

  let highlighted: Set<string>;
  if (nodeId) {
    const bySource = new Map<string, Edge[]>();
    const byTarget = new Map<string, Edge[]>();
    for (const edge of edges) {
      (bySource.get(edge.source) ?? bySource.set(edge.source, []).get(edge.source)!).push(edge);
      (byTarget.get(edge.target) ?? byTarget.set(edge.target, []).get(edge.target)!).push(edge);
    }
    highlighted = new Set([
      ...traceEdges(nodeId, byTarget, (e) => e.source), // sheikhs above
      ...traceEdges(nodeId, bySource, (e) => e.target), // students below
    ]);
  } else {
    highlighted = new Set(
      edges
        .filter((e) => ((e.data?.books as string[] | undefined) ?? []).some((b) => getBookMeta(b).name === book))
        .map((e) => e.id)
    );
  }

  const litNodes = new Set<string>();
  if (nodeId) litNodes.add(nodeId);
  for (const edge of edges) {
    if (highlighted.has(edge.id)) {
      litNodes.add(edge.source);
      litNodes.add(edge.target);
    }
  }

  return {
    nodes: nodes.map((node) =>
      litNodes.has(node.id)
        ? node
        : { ...node, style: { ...node.style, opacity: DIM_NODE_OPACITY } }
    ),
    edges: edges.map((edge) =>
      highlighted.has(edge.id)
        ? { ...edge, zIndex: 1000 }
        : {
            ...edge,
            zIndex: 0,
            label: undefined,
            animated: false,
            style: { ...edge.style, opacity: DIM_EDGE_OPACITY },
          }
    ),
  };
}
