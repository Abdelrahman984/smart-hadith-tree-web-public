"use client";

import { useCallback, useEffect, useMemo, useRef } from "react";
import {
  Background,
  MiniMap,
  Node,
  Panel,
  ReactFlow,
  ReactFlowInstance,
  useEdgesState,
  useNodesState,
  Edge,
} from "@xyflow/react";
import { X } from "lucide-react";
import { useMediaQuery } from "@/components/useMediaQuery";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { getIlalEdgeDecorations } from "@/features/ilal/utils/ilalLabels";
import { useLegendOpen } from "../hooks/useLegendOpen";
import { useMeasuredRelayout } from "../hooks/useMeasuredRelayout";
import { useRefitOnResize } from "../hooks/useRefitOnResize";
import { COMPACT_NODES_MIN, isCompactDensity, useGraphViewStore } from "../store/useGraphViewStore";
import { getNodeColors } from "@/features/narrator-details/utils/gradeStyle";
import type { NarratorSearchItem } from "../utils/narratorSearch";
import { COMPACT_LABELS_MIN_EDGES, decorateEdgeWithIlal } from "../utils/edgeStyle";
import { getLayoutedElements } from "../utils/elkLayout";
import { applyGraphFocus } from "../utils/graphFocus";
import type { BuiltGraph } from "../utils/graphTypes";
import BookLegend from "./BookLegend";
import ElkEdge from "./ElkEdge";
import GraphToolbar from "./GraphToolbar";
import NarratorNode from "./NarratorNode";
import ReferenceNode from "./ReferenceNode";

/** Height of the toolbar plus its margin and a gap. */
const TOOLBAR_RESERVE_PX = 64;

/** Width of the open legend (w-80) plus its margin and a gap. */
const LEGEND_RESERVE_PX = 352;

const edgeTypes = { elk: ElkEdge };
const nodeTypes = { narrator: NarratorNode, reference: ReferenceNode };

interface IsnadGraphCanvasProps {
  /** Memoize it: the canvas lays out again whenever this object changes. */
  graph: BuiltGraph;
  /** Lets the legend highlight one book's routes (comparative tree). */
  bookFocus?: boolean;
}

/**
 * The isnad graph, for one hadith or several: two-pass ELK layout, Ilal overlay on the edges, chain / book focus,
 * legend and controls. The page decides what to draw by building the `graph`.
 */
export default function IsnadGraphCanvas({ graph, bookFocus = false }: IsnadGraphCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const openDrawer = useNarratorDrawerStore((s) => s.openDrawer);

  const hoveredNodeId = useGraphViewStore((s) => s.hoveredNodeId);
  const focusBook = useGraphViewStore((s) => s.focusBook);
  const setHoveredNodeId = useGraphViewStore((s) => s.setHoveredNodeId);
  const setFocusBook = useGraphViewStore((s) => s.setFocusBook);
  const reveal = useGraphViewStore((s) => s.reveal);
  const resetView = useGraphViewStore((s) => s.reset);
  useEffect(() => resetView, [resetView]);

  const flowRef = useRef<ReactFlowInstance | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  // While the legend is open beside the graph, fit the graph into the space it leaves, so no card hides under it.
  const isWide = useMediaQuery("(min-width: 768px)", true);
  const legendOn = useLegendOpen();
  const reserveLegend = legendOn && isWide;
  const fitOptions = useMemo(
    () => ({
      duration: 0,
      padding: {
        top: `${TOOLBAR_RESERVE_PX}px` as const, // the toolbar sits over the top-left corner
        bottom: 0.1,
        left: 0.1,
        right: reserveLegend ? (`${LEGEND_RESERVE_PX}px` as const) : 0.1,
      },
    }),
    [reserveLegend]
  );
  const refit = useCallback(() => flowRef.current?.fitView(fitOptions), [fitOptions]);
  const { phase, visible, setPhase } = useMeasuredRelayout({ nodes, edges, setNodes, setEdges, onRelaid: refit });
  useRefitOnResize(containerRef, refit, phase === "ready");
  useEffect(() => {
    if (phase === "ready") refit(); // the legend was opened or closed
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reserveLegend]);

  // The automatic choices (compact cards, minimap) depend on how many narrators the graph has.
  const setNodeCount = useGraphViewStore((s) => s.setNodeCount);
  useEffect(() => setNodeCount(graph.nodes.length), [graph.nodes.length, setNodeCount]);

  // Compact and detailed cards differ in size: lay out again with the new measurements, after they have been measured.
  const compactCards = useGraphViewStore((s) => isCompactDensity(s.density, s.nodeCount));
  const lastCompact = useRef(compactCards);
  useEffect(() => {
    if (phase !== "ready" || compactCards === lastCompact.current) return;
    lastCompact.current = compactCards;
    const timer = setTimeout(() => setPhase("measure"), 150);
    return () => clearTimeout(timer);
  }, [compactCards, phase, setPhase]);

  const isLarge = useMediaQuery("(min-width: 1024px)", true);
  const minimapSetting = useGraphViewStore((s) => s.minimap);
  const minimapOn = minimapSetting ?? (isLarge && graph.nodes.length > COMPACT_NODES_MIN);

  // The graph's narrators, for the find box.
  const narrators = useMemo<NarratorSearchItem[]>(
    () =>
      nodes.filter((n) => n.type !== "reference").map((n) => {
        const d = n.data as { narratorName?: string; famousName?: string; fullName?: string; generationTier?: string | null; gradeEn?: string };
        const short = d.narratorName ?? d.famousName ?? "";
        return { id: n.id, name: d.fullName || short, shortName: short, tier: d.generationTier };
      }),
    [nodes]
  );

  // First layout pass, with estimated sizes; useMeasuredRelayout does the second one with the measured sizes.
  useEffect(() => {
    if (graph.nodes.length === 0) return;
    let cancelled = false;
    setPhase("idle");
    getLayoutedElements(graph.nodes, graph.edges).then(({ nodes: laidOut, edges: laidOutEdges }) => {
      if (cancelled) return;
      setNodes(laidOut);
      setEdges(laidOutEdges);
      setPhase("measure");
    });
    return () => {
      cancelled = true;
    };
  }, [graph, setNodes, setEdges, setPhase]);

  // Bring narrators into view when a panel asks (a madar chip, an Ilal finding), selecting a single one.
  useEffect(() => {
    if (!reveal || phase !== "ready") return;
    const ids = reveal.ids.filter((id) => nodes.some((n) => n.id === id));
    if (ids.length === 0) return;
    if (reveal.select) {
      setNodes((nds) => nds.map((n) => (n.selected === (n.id === ids[0]) ? n : { ...n, selected: n.id === ids[0] })));
    }
    flowRef.current?.fitView({ nodes: ids.map((id) => ({ id })), duration: 400, padding: 0.6, maxZoom: 1.1 });
    // Only a new request (seq) should act, not a re-layout that merely changes `nodes`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reveal?.seq, phase]);

  // An Ilal finding selected in the panel brings its narrators into view.
  const highlightedNarratorIds = useIlalStore((s) => s.highlightedNarratorIds);
  const revealNodes = useGraphViewStore((s) => s.revealNodes);
  useEffect(() => {
    if (highlightedNarratorIds.length > 0) revealNodes(highlightedNarratorIds, { select: false });
  }, [highlightedNarratorIds, revealNodes]);

  const onNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      if (node.type === "reference") return; // a book's card, not a narrator: there is no narrator to open
      const data = node.data as { anomalyReason?: string | null; travelNote?: string | null };
      openDrawer(node.id, { anomalyReason: data.anomalyReason, travelNote: data.travelNote });
    },
    [openDrawer]
  );

  // Overlay isnad-link ilal (tadlis, unproven meeting, ikhtilat) on the laid-out edges.
  const ilalReport = useIlalStore((s) => s.report);
  const showLowConfidence = useIlalStore((s) => s.showLowConfidence);
  const decoratedEdges = useMemo(() => {
    const decorations = getIlalEdgeDecorations(ilalReport, showLowConfidence);
    if (decorations.size === 0) return edges;
    return edges.map((edge) => {
      const decoration = decorations.get(edge.id);
      return decoration ? decorateEdgeWithIlal(edge, decoration) : edge;
    });
  }, [edges, ilalReport, showLowConfidence]);

  // A selected narrator (click, tap, or Enter on the focused card) keeps its chain highlighted; hovering only previews
  // while nothing is selected. Escape peels one layer per press: the narrator details, then the selection, then the book
  // focus. It listens in the capture phase because React Flow deselects a focused card on Escape itself, and that update
  // would otherwise remove this handler before the event reaches it.
  const selectedId = nodes.find((n) => n.selected)?.id ?? null;
  const activeBook = bookFocus ? focusBook : null;
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      const target = e.target as HTMLElement | null;
      if (target?.closest("input, textarea, select, [contenteditable=true]")) return;

      const narrator = useNarratorDrawerStore.getState();
      if (narrator.isOpen) {
        narrator.closeDrawer();
      } else if (selectedId) {
        setNodes((nds) => nds.map((n) => (n.selected ? { ...n, selected: false } : n)));
      } else if (activeBook) {
        setFocusBook(null);
      } else {
        return; // nothing of ours to close: let the key through
      }
      e.stopPropagation(); // one layer per press
    };
    window.addEventListener("keydown", onKeyDown, true);
    return () => window.removeEventListener("keydown", onKeyDown, true);
  }, [selectedId, activeBook, setNodes, setFocusBook]);

  // Enter on a focused card opens the narrator details, like a click does (React Flow itself only selects it).
  const onKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key !== "Enter") return;
      const id = (e.target as HTMLElement).closest<HTMLElement>(".react-flow__node")?.dataset.id;
      const node = id ? nodes.find((n) => n.id === id) : undefined;
      if (node) onNodeClick(e as unknown as React.MouseEvent, node);
    },
    [nodes, onNodeClick]
  );

  // Dim everything except the focused narrator's chain or the focused book.
  const focused = useMemo(
    () => applyGraphFocus(nodes, decoratedEdges, { nodeId: selectedId ?? hoveredNodeId, book: activeBook }),
    [nodes, decoratedEdges, selectedId, hoveredNodeId, activeBook]
  );

  // On a big graph the edge labels are markers until a narrator or book is focused, then the focused chain is labelled.
  const hasFocus = Boolean(selectedId ?? hoveredNodeId ?? activeBook);
  const compactLabels = focused.edges.length > COMPACT_LABELS_MIN_EDGES && !hasFocus;
  const displayEdges = useMemo(
    () => focused.edges.map((e) => (e.label ? { ...e, data: { ...e.data, compact: compactLabels } } : e)),
    [focused.edges, compactLabels]
  );

  return (
    <div
      ref={containerRef}
      className={`absolute inset-0 bg-surface-muted transition-opacity duration-150 ${visible ? "opacity-100" : "opacity-0"}`}
      dir="ltr"
      onKeyDown={onKeyDown}
    >
      <ReactFlow
        nodes={focused.nodes}
        edges={displayEdges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={onNodeClick}
        onNodeMouseEnter={(_, node) => setHoveredNodeId(node.id)}
        onNodeMouseLeave={() => setHoveredNodeId(null)}
        minZoom={0.1}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onInit={(instance) => {
          flowRef.current = instance;
        }}
        fitView
        fitViewOptions={fitOptions}
        attributionPosition="bottom-right"
        className="bg-surface-muted"
      >
        <GraphToolbar narrators={narrators} onFit={refit} minimapOn={minimapOn} legendOn={legendOn} />
        {activeBook && (
          <Panel position="top-center" className="m-2" dir="rtl">
            <button
              type="button"
              onClick={() => setFocusBook(null)}
              title="إلغاء الإبراز (Esc)"
              className="flex items-center gap-2 bg-surface/95 backdrop-blur-md px-3 py-1.5 rounded-full shadow-md border border-slate-300 text-xs font-bold text-slate-700 hover:bg-surface-muted cursor-pointer"
            >
              <span>إبراز: {activeBook}</span>
              <X className="w-3.5 h-3.5 text-ink-subtle" />
            </button>
          </Panel>
        )}
        <BookLegend
          activeBooks={graph.bookNames}
          focusBook={activeBook}
          onFocusBook={bookFocus ? setFocusBook : undefined}
        />
        <Background color="#cbd5e1" gap={16} />
        {minimapOn && (
          <MiniMap
            position="bottom-left"
            pannable
            zoomable
            ariaLabel="خريطة الشجرة"
            nodeColor={(n) => getNodeColors((n.data as { gradeEn?: string })?.gradeEn).borderColor}
            nodeStrokeWidth={2}
            className="!bg-surface/90"
          />
        )}
      </ReactFlow>
    </div>
  );
}
