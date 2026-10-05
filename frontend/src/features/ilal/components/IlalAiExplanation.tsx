"use client";

import { Sparkles, Loader2 } from "lucide-react";
import { IlalReportDto } from "@/types/api";
import { useIlalExplanation } from "../hooks/useIlal";
import AiNotice from "@/components/AiNotice";

/** Button that asks the AI to explain the report, grounded only in the rule engine's findings. */
export default function IlalAiExplanation({ report }: { report: IlalReportDto }) {
  const { mutate, data, isPending, isError, reset } = useIlalExplanation();

  if (data) {
    return (
      <div className="rounded-xl border border-indigo-200 bg-indigo-50/60 p-3 space-y-2">
        <div className="flex items-center justify-between">
          <h4 className="flex items-center gap-1.5 text-sm font-bold text-indigo-900">
            <Sparkles className="h-4 w-4" /> شرح الناقد الآلي
          </h4>
          <button onClick={reset} className="text-[11px] text-indigo-600 hover:underline cursor-pointer">
            إخفاء
          </button>
        </div>
        <p className="whitespace-pre-wrap text-sm leading-loose text-ink">{data.explanationAr}</p>
        {data.caveats.length > 0 && (
          <ul className="list-disc space-y-1 ps-5 text-xs text-indigo-800">
            {data.caveats.map((c, i) => <li key={i}>{c}</li>)}
          </ul>
        )}
        <AiNotice />
      </div>
    );
  }

  return (
    <div className="space-y-1">
      <button
        onClick={() => mutate(report)}
        disabled={isPending}
        className="flex w-full items-center justify-center gap-2 rounded-lg border border-indigo-200 bg-surface px-3 py-2 text-sm font-semibold text-indigo-700 transition-colors hover:bg-indigo-50 disabled:opacity-60 cursor-pointer"
      >
        {isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
        {isPending ? "جاري توليد الشرح..." : "اشرح العلل بالذكاء الاصطناعي"}
      </button>
      {isError && <p className="text-xs text-red-600">تعذّر توليد الشرح.</p>}
    </div>
  );
}
