"use client";

import { useMemo } from "react";
import type { ComparativeTreeResponseDto, NarratorSummaryDto } from "@/types/api";
import { buildComparativeGraph } from "../utils/buildIsnadGraph";
import IsnadGraphCanvas from "./IsnadGraphCanvas";

interface ComparativeTreeCanvasProps {
  treeData: ComparativeTreeResponseDto;
  narratorsTooltips?: Record<string, NarratorSummaryDto>;
}

/** The merged isnad tree of several hadiths (takhreej), with book focus. */
export default function ComparativeTreeCanvas({ treeData, narratorsTooltips }: ComparativeTreeCanvasProps) {
  const graph = useMemo(() => buildComparativeGraph(treeData, narratorsTooltips), [treeData, narratorsTooltips]);
  return <IsnadGraphCanvas graph={graph} bookFocus />;
}
