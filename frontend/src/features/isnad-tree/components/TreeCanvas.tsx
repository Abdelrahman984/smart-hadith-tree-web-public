"use client";

import { useMemo } from "react";
import type { IsnadTreeResponseDto, NarratorSummaryDto } from "@/types/api";
import { buildSingleGraph } from "../utils/buildIsnadGraph";
import IsnadGraphCanvas from "./IsnadGraphCanvas";

interface TreeCanvasProps {
  treeData: IsnadTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

/** The isnad tree of one hadith. */
export default function TreeCanvas({ treeData, narratorsTooltips }: TreeCanvasProps) {
  const graph = useMemo(() => buildSingleGraph(treeData, narratorsTooltips), [treeData, narratorsTooltips]);
  return <IsnadGraphCanvas graph={graph} />;
}
