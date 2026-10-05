"use client";

import { BaseEdge, EdgeLabelRenderer, EdgeProps, getBezierPath, useInternalNode } from "@xyflow/react";
import { AlertTriangle, FileDiff, ShieldAlert } from "lucide-react";
import type { EdgeKind } from "../utils/edgeStyle";

export interface ElkPoint {
  x: number;
  y: number;
}

/** Layout-time data attached to an edge by `getLayoutedElements`. */
export interface ElkEdgeData extends Record<string, unknown> {
  /** Routed polyline (start, bends, end) in flow coordinates. */
  points?: ElkPoint[];
  /** Positions of the source/target nodes when the route was computed. */
  sourcePos?: ElkPoint;
  targetPos?: ElkPoint;
  /** Draw the label as a small marker (hover for the text) instead of printing it on the edge. */
  compact?: boolean;
  kind?: EdgeKind;
}

const MARKER_ICON = { anomaly: AlertTriangle, variation: FileDiff, ilal: ShieldAlert } as const;

const CORNER_RADIUS = 14;

/** Builds an SVG path through the points, rounding each corner. */
function roundedPolyline(points: ElkPoint[], radius: number): string {
  if (points.length < 2) return "";
  let d = `M ${points[0].x} ${points[0].y}`;
  for (let i = 1; i < points.length - 1; i++) {
    const prev = points[i - 1];
    const cur = points[i];
    const next = points[i + 1];
    const lenIn = Math.hypot(cur.x - prev.x, cur.y - prev.y);
    const lenOut = Math.hypot(next.x - cur.x, next.y - cur.y);
    const r = Math.min(radius, lenIn / 2, lenOut / 2);
    if (r < 1) {
      d += ` L ${cur.x} ${cur.y}`;
      continue;
    }
    const inX = cur.x - ((cur.x - prev.x) / lenIn) * r;
    const inY = cur.y - ((cur.y - prev.y) / lenIn) * r;
    const outX = cur.x + ((next.x - cur.x) / lenOut) * r;
    const outY = cur.y + ((next.y - cur.y) / lenOut) * r;
    d += ` L ${inX} ${inY} Q ${cur.x} ${cur.y} ${outX} ${outY}`;
  }
  const last = points[points.length - 1];
  return `${d} L ${last.x} ${last.y}`;
}

/** Point halfway along the polyline's total length (for the label). */
function polylineMidpoint(points: ElkPoint[]): ElkPoint {
  const lengths: number[] = [];
  let total = 0;
  for (let i = 1; i < points.length; i++) {
    const len = Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y);
    lengths.push(len);
    total += len;
  }
  let remaining = total / 2;
  for (let i = 0; i < lengths.length; i++) {
    if (remaining <= lengths[i] && lengths[i] > 0) {
      const t = remaining / lengths[i];
      return {
        x: points[i].x + (points[i + 1].x - points[i].x) * t,
        y: points[i].y + (points[i + 1].y - points[i].y) * t,
      };
    }
    remaining -= lengths[i];
  }
  return points[0];
}

/**
 * Edge that draws the route computed by ELK, so long edges pass through the
 * lanes ELK reserved for them instead of cutting across other nodes. Falls back
 * to a bezier when no route exists or a node was dragged away from its layout position.
 */
export default function ElkEdge({
  id,
  source,
  target,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  markerEnd,
  style,
  label,
  labelStyle,
  labelBgStyle,
  labelBgPadding,
  data,
}: EdgeProps) {
  const sourceNode = useInternalNode(source);
  const targetNode = useInternalNode(target);
  const edgeData = data as ElkEdgeData | undefined;

  const srcAbs = sourceNode?.internals.positionAbsolute;
  const tgtAbs = targetNode?.internals.positionAbsolute;
  const moved =
    !edgeData?.sourcePos ||
    !edgeData?.targetPos ||
    !srcAbs ||
    !tgtAbs ||
    Math.abs(srcAbs.x - edgeData.sourcePos.x) > 1 ||
    Math.abs(srcAbs.y - edgeData.sourcePos.y) > 1 ||
    Math.abs(tgtAbs.x - edgeData.targetPos.x) > 1 ||
    Math.abs(tgtAbs.y - edgeData.targetPos.y) > 1;

  const points = edgeData?.points;
  let path: string;
  let labelX: number;
  let labelY: number;

  if (points && points.length >= 2 && !moved) {
    path = roundedPolyline(points, CORNER_RADIUS);
    const mid = polylineMidpoint(points);
    labelX = mid.x;
    labelY = mid.y;
  } else {
    [path, labelX, labelY] = getBezierPath({
      sourceX,
      sourceY,
      targetX,
      targetY,
      sourcePosition,
      targetPosition,
    });
  }

  const compact = Boolean(edgeData?.compact && label);
  const MarkerIcon = MARKER_ICON[edgeData?.kind ?? "ilal"];
  const color = (style?.stroke as string | undefined) ?? "#64748b";

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        markerEnd={markerEnd}
        style={style}
        label={compact ? undefined : label}
        labelX={labelX}
        labelY={labelY}
        labelStyle={labelStyle}
        labelBgStyle={labelBgStyle}
        labelBgPadding={labelBgPadding}
        labelShowBg={Boolean(labelBgStyle)}
      />
      {compact && (
        <EdgeLabelRenderer>
          <span
            role="img"
            aria-label={String(label)}
            title={String(label)}
            data-edge-marker={id}
            className="nodrag nopan absolute flex h-5 w-5 items-center justify-center rounded-full border bg-surface shadow-xs"
            style={{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`, borderColor: color, pointerEvents: "all" }}
          >
            <MarkerIcon className="h-3 w-3" style={{ color }} aria-hidden />
          </span>
        </EdgeLabelRenderer>
      )}
    </>
  );
}
