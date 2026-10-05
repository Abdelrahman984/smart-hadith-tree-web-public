"use client";

import { ShieldCheck } from "lucide-react";
import EvidenceBadge from "@/components/EvidenceBadge";
import { isLowConfidenceFinding, SEVERITY_STYLES } from "@/features/ilal/utils/ilalLabels";
import type { ComparativeTreeResponseDto, IllahSeverity } from "@/types/api";
import { useGraphViewStore } from "../store/useGraphViewStore";

const SEVERITY_ORDER: IllahSeverity[] = ["Qadihah", "GhayrQadihah", "Tanbih"];

interface TakhreejSummaryProps {
  data: ComparativeTreeResponseDto;
  /** Opens the findings (the sidebar's «العلل» tab). */
  onOpenIlal: () => void;
  /** How many of the narrations are shawahid (through another Companion). */
  shawahidCount?: number;
}

/** The answer at a glance, above the graph: how many routes and books, the madar, the grade, and the findings. */
export default function TakhreejSummary({ data, onOpenIlal, shawahidCount = 0 }: TakhreejSummaryProps) {
  const revealNodes = useGraphViewStore((s) => s.revealNodes);

  const bookCount = new Set(data.sources.map((s) => s.bookName)).size;
  const report = data.ilalReport;
  const madars = report?.madars ?? [];
  const visibleFindings = (report?.findings ?? []).filter((f) => !isLowConfidenceFinding(f));

  return (
    <section
      aria-label="ملخص التخريج"
      className="shrink-0 flex items-center gap-x-5 gap-y-1 overflow-x-auto border-b border-line bg-surface-muted px-3 py-1.5 text-xs text-ink-muted sm:px-4 [&>*]:shrink-0"
    >
      <span className="font-semibold text-ink">
        {data.sources.length} {data.sources.length === 1 ? "رواية" : "روايات"} من {bookCount} {bookCount === 1 ? "كتاب" : "كتب"}
        {shawahidCount > 0 && <span className="font-normal text-ink-muted"> (منها {shawahidCount === 1 ? "شاهد" : `${shawahidCount} شواهد`})</span>}
      </span>

      {madars.length > 0 && (
        <span className="flex items-center gap-1.5">
          <span>{madars.length === 1 ? "المدار:" : "المدارات:"}</span>
          {madars.slice(0, 3).map((m) => (
            <button
              key={m.narratorId}
              type="button"
              onClick={() => revealNodes([m.narratorId])}
              title={`مدار الإسناد: تتفرع عنده ${m.branchCount} طرق. اضغط لإظهاره على الشجرة`}
              className="rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 font-semibold text-amber-900 hover:bg-amber-100 cursor-pointer"
            >
              {m.narratorName}
            </button>
          ))}
        </span>
      )}

      {data.calculatedGrade && (
        <span className="flex items-center gap-1.5">
          <span>التقدير الآلي:</span>
          <span className="rounded-md bg-slate-200/70 px-2 py-0.5 font-bold text-ink">{data.calculatedGrade}</span>
          <EvidenceBadge status="verify" compact />
        </span>
      )}

      {report && (
        <span className="flex items-center gap-1.5">
          <span>العلل:</span>
          {visibleFindings.length === 0 ? (
            <span className="flex items-center gap-1 font-semibold text-emerald-800">
              <ShieldCheck className="h-3.5 w-3.5" aria-hidden /> لم تُرصد علة
            </span>
          ) : (
            SEVERITY_ORDER.map((severity) => {
              const count = visibleFindings.filter((f) => f.severity === severity).length;
              if (count === 0) return null;
              return (
                <button
                  key={severity}
                  type="button"
                  onClick={onOpenIlal}
                  className={`rounded-full border px-2 py-0.5 font-bold cursor-pointer hover:brightness-95 ${SEVERITY_STYLES[severity].badge}`}
                >
                  {count} {SEVERITY_STYLES[severity].label}
                </button>
              );
            })
          )}
        </span>
      )}
    </section>
  );
}
