import { AlertTriangle, Info } from "lucide-react";
import type { ConsensusResult } from "../utils/evaluationConsensus";

/** Tells the reader whether the recorded scholars agree or differ about the narrator. */
export default function ConsensusBanner({ consensus }: { consensus: ConsensusResult }) {
  const { status, tadilCount, jarhCount, agreedCamp } = consensus;
  if (status === "none") return null;

  if (status === "dispute") {
    return (
      <div role="note" className="mb-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-xs leading-relaxed text-amber-900">
        <p className="mb-1 flex items-center gap-1.5 text-sm font-bold">
          <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden /> اختلف العلماء في هذا الراوي
        </p>
        <p>
          في البيانات {tadilCount.toLocaleString("ar-EG")} قول بالتعديل و{jarhCount.toLocaleString("ar-EG")} بالجرح. تُعرض
          الأقوال أدناه مجموعةً بحسب كل فريق، ولا يُعد أي ملخص آلي إجماعاً. التصنيف بحسب بيانات المصدر، فراجع نص كل قول.
        </p>
      </div>
    );
  }

  const text =
    status === "agree"
      ? `اتفقت الأقوال المسجلة في البيانات (${(tadilCount + jarhCount).toLocaleString("ar-EG")}) على ${
          agreedCamp === "tadil" ? "التعديل" : "الجرح"
        }، وهذا لا ينفي وجود أقوال أخرى في مصادر لم تُدرج.`
      : "لا يوجد في البيانات إلا قول واحد مصنَّف، وهو لا يكفي للحكم باتفاق العلماء أو اختلافهم.";

  return (
    <p role="note" className="mb-3 flex items-start gap-1.5 rounded-lg border border-line bg-surface-muted p-2.5 text-xs leading-relaxed text-ink-muted">
      <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />
      <span>{text}</span>
    </p>
  );
}
