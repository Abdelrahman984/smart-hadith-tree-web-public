"use client";

import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { countVisibleFindings } from "@/features/ilal/utils/ilalLabels";

/** Count of findings on the «العلل» tab: red when one is قادحة. Nothing until a report is loaded. */
export default function IlalTabBadge() {
  const report = useIlalStore((s) => s.report);
  const count = report ? countVisibleFindings(report.findings) : 0;
  if (!report || count === 0) return null;
  return (
    <span className={`rounded-full px-1.5 text-[11px] text-white ${report.hasQadihah ? "bg-red-500" : "bg-amber-500"}`}>{count}</span>
  );
}
