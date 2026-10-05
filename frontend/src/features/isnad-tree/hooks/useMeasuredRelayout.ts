import { useCallback, useEffect, useState, Dispatch, SetStateAction } from "react";
import { Node, Edge } from "@xyflow/react";
import { getLayoutedElements } from "../utils/elkLayout";

const MEASURE_TIMEOUT_MS = 2000;

export type LayoutPhase = "idle" | "measure" | "ready";

interface Options {
  nodes: Node[];
  edges: Edge[];
  setNodes: Dispatch<SetStateAction<Node[]>>;
  setEdges: Dispatch<SetStateAction<Edge[]>>;
  /** Called after the second layout pass has been applied (e.g. to refit the viewport). */
  onRelaid?: () => void;
}

/**
 * Second layout pass: after the first ELK layout (estimated sizes) is rendered, wait for
 * React Flow to measure every node, then lay out again with the real sizes so edge
 * routes and spacing match what is on screen.
 *
 * The canvas calls `setPhase("idle")` when it starts a layout and `setPhase("measure")`
 * once the first-pass result is in state. Hide the graph until `phase === "ready"`.
 */
export function useMeasuredRelayout({ nodes, edges, setNodes, setEdges, onRelaid }: Options) {
  const [phase, setPhaseState] = useState<LayoutPhase>("idle");
  // True from the first finished layout on. Later passes (a density change) re-lay out without hiding the graph;
  // asking for "idle" (a new graph) hides it again.
  const [visible, setVisible] = useState(false);
  const setPhase = useCallback((next: LayoutPhase) => {
    setPhaseState(next);
    if (next === "idle") setVisible(false);
  }, []);

  // Fallback: never leave the graph hidden if measurement stalls (e.g. a background tab).
  useEffect(() => {
    if (phase !== "measure") return;
    const timer = setTimeout(() => {
      setPhaseState("ready");
      setVisible(true);
      requestAnimationFrame(() => onRelaid?.());
    }, MEASURE_TIMEOUT_MS);
    return () => clearTimeout(timer);
  }, [phase, onRelaid]);

  useEffect(() => {
    if (phase !== "measure" || nodes.length === 0) return;
    if (!nodes.every((n) => n.measured?.width && n.measured?.height)) return;

    let cancelled = false;
    getLayoutedElements(nodes, edges).then(({ nodes: laidOut, edges: laidOutEdges }) => {
      if (cancelled) return;
      setNodes(laidOut);
      setEdges(laidOutEdges);
      setPhaseState("ready");
      setVisible(true);
      requestAnimationFrame(() => onRelaid?.());
    });
    return () => {
      cancelled = true;
    };
  }, [phase, nodes, edges, setNodes, setEdges, onRelaid]);

  return { phase, visible, setPhase };
}
