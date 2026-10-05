import { MatnComparisonDto, IlalTariqDto } from "@/types/api";

interface MatnDiffViewProps {
  comparison: MatnComparisonDto;
  turuq: IlalTariqDto[];
}

function tariqLabel(turuq: IlalTariqDto[], hadithId: string) {
  const t = turuq.find((x) => x.hadithId === hadithId);
  return t ? `${t.bookName} (${t.hadithNumber})` : "رواية";
}

/**
 * Shows a word-level comparison of two matn texts: words only in the compared tariq are
 * highlighted as additions, words only in the reference tariq are struck through.
 */
export default function MatnDiffView({ comparison, turuq }: MatnDiffViewProps) {
  return (
    <div className="mt-2 rounded-lg border border-line bg-surface p-2.5 text-sm">
      <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-ink-subtle">
        <span>
          المرجع: <strong className="text-slate-700">{tariqLabel(turuq, comparison.referenceHadithId)}</strong>
        </span>
        <span>
          المقارَن: <strong className="text-slate-700">{tariqLabel(turuq, comparison.comparedHadithId)}</strong>
        </span>
        <span>التشابه: {Math.round(comparison.similarity * 100)}%</span>
      </div>
      <p className="font-arabic leading-loose text-slate-700">
        {comparison.segments.map((segment, idx) => {
          if (segment.kind === "added") {
            return (
              <mark key={idx} className="mx-0.5 rounded bg-emerald-100 px-1 text-emerald-900" title="زيادة في الرواية المقارنة">
                {segment.text}
              </mark>
            );
          }
          if (segment.kind === "removed") {
            return (
              <del key={idx} className="mx-0.5 rounded bg-rose-100 px-1 text-rose-800 decoration-rose-400" title="ليست في الرواية المقارنة">
                {segment.text}
              </del>
            );
          }
          return <span key={idx}> {segment.text} </span>;
        })}
      </p>
      <div className="mt-2 flex gap-3 text-[10px] text-ink-subtle">
        <span className="flex items-center gap-1"><span className="h-2.5 w-2.5 rounded bg-emerald-200" /> زيادة</span>
        <span className="flex items-center gap-1"><span className="h-2.5 w-2.5 rounded bg-rose-200" /> نقص / مخالفة</span>
      </div>
    </div>
  );
}
