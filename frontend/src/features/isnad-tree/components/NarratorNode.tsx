import { Handle, Position } from "@xyflow/react";
import { memo } from "react";
import { AlertTriangle, FileDiff, MapPin } from "lucide-react";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { getGradeStyle, getNodeColors, isUnrated, UNRATED_HINT, UNRATED_STYLE } from "@/features/narrator-details/utils/gradeStyle";
import { isCompactDensity, useGraphViewStore } from "../store/useGraphViewStore";
import type { NarratorNodeData } from "../utils/graphTypes";
import { NarratorBadges, SourceBookBadges } from "./NarratorBadges";

const HANDLE = "!w-2 !h-2 !bg-transparent !border-none opacity-0 pointer-events-none";

/** A narrator card, used by the single tree and the comparative tree (which adds source-book badges and wording marks). */
const NarratorNode = ({ id, data, selected }: { id: string; data: NarratorNodeData; selected?: boolean }) => {
  const isIlalHighlighted = useIlalStore((s) => s.highlightedNarratorIds.includes(id));
  const isMadar = useIlalStore((s) => Boolean(s.report?.madars.some((m) => m.narratorId === id)));
  const showWeakOnly = useGraphViewStore((s) => s.showWeakOnly);
  const compact = useGraphViewStore((s) => isCompactDensity(s.density, s.nodeCount));

  const { borderColor, bgColor } = getNodeColors(data.gradeEn);
  const grade = getGradeStyle(data.gradeEn);
  const unrated = isUnrated(data.gradeEn);
  // «إبراز الضعفاء» dims everyone who is not known to be weak: the reliable, and the unrated (no verdict is not weakness).
  const opacity = showWeakOnly && (unrated || (grade?.isReliable ?? false)) ? 0.3 : 1;
  const books = data.sourceBooks ?? [];

  const ringClass = selected
    ? "ring-2 ring-brand-blue border-brand-blue"
    : isIlalHighlighted
      ? "ring-4 ring-rose-400 ring-offset-2 scale-105"
      : isMadar
        ? "ring-2 ring-amber-400 ring-offset-1"
        : "";

  return (
    <div
      dir="rtl"
      className={`${compact ? "px-3 py-2 min-w-[150px] max-w-[190px]" : "px-4 py-3 min-w-[200px] max-w-[250px]"} shadow-md rounded-lg border-2 text-center transition-all break-words relative ${ringClass}`}
      style={{
        backgroundColor: bgColor,
        borderColor: selected ? undefined : borderColor,
        borderStyle: unrated ? "dashed" : undefined,
        opacity,
      }}
    >
      {data.isAnomaly && (
        <div className="absolute -top-3 -right-3 bg-surface rounded-full p-1 shadow border border-red-200" title={data.anomalyReason}>
          <AlertTriangle className="w-5 h-5 text-red-500" />
        </div>
      )}
      {data.travelNote && (
        <div className="absolute -bottom-3 -right-3 bg-surface rounded-full p-1 shadow border border-sky-200" title={data.travelNote}>
          <MapPin className="w-4 h-4 text-sky-500" />
        </div>
      )}
      {data.hasMatnVariation && (
        <div
          className="absolute -bottom-3 -left-3 bg-surface rounded-full p-1 shadow border border-amber-200"
          title={data.matnVariationSnippet || "اختلاف باللفظ"}
        >
          <FileDiff className="w-4 h-4 text-amber-500" />
        </div>
      )}

      {/* Input from the sheikh */}
      <Handle type="target" position={Position.Top} className={HANDLE} />

      <SourceBookBadges books={books} />

      {data.transmissionTerm && !compact && (
        <div className={`text-xs text-ink-subtle mb-1 border-b pb-1 ${books.length > 0 ? "mt-2" : ""}`}>{data.transmissionTerm}</div>
      )}

      <div className={`font-bold text-ink ${compact ? "text-base" : "text-lg"}`} title={data.fullName || data.narratorName}>
        {data.narratorName}
      </div>

      {data.generationTier && !compact && <div className="text-sm text-ink-subtle mt-1">{data.generationTier}</div>}

      {grade ? (
        <span className={`inline-block mt-1.5 px-2 py-0.5 text-[11px] font-bold rounded-full border ${grade.badgeClass}`}>
          {grade.label}
        </span>
      ) : (
        <span
          className={`inline-block mt-1.5 px-2 py-0.5 text-[11px] font-semibold rounded-full border ${UNRATED_STYLE.badgeClass}`}
          title={UNRATED_HINT}
        >
          {UNRATED_STYLE.label}
        </span>
      )}

      {!compact && <NarratorBadges data={data} />}

      {/* Output to the student */}
      <Handle type="source" position={Position.Bottom} className={HANDLE} />
    </div>
  );
};

export default memo(NarratorNode);
