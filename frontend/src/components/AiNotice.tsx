import { Info } from "lucide-react";

const DORAR_HADITH_URL = "https://dorar.net/hadith";

/**
 * Standard disclosure shown next to any AI-generated output: it is a tool-assisted summary,
 * it is not a fatwa or a final ruling on a hadith, and doubtful or disputed cases go to specialists.
 */
export default function AiNotice({ className = "" }: { className?: string }) {
  return (
    <div className={`rounded-lg border border-line bg-surface-muted p-2.5 text-[11px] leading-relaxed text-ink-muted ${className}`}>
      <p className="flex items-start gap-1.5">
        <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />
        <span>
          هذا المحتوى مولَّد بمساعدة الذكاء الاصطناعي من نصوص ومصادر مسجلة، وليس فتوى ولا حكماً نهائياً على حديث أو راوٍ.
          راجع الأقوال الأصلية، وفي المسائل الخلافية أو الدقيقة ارجع إلى أهل الاختصاص، وتحقق من حكم الحديث في{" "}
          <a href={DORAR_HADITH_URL} target="_blank" rel="noopener noreferrer" className="font-semibold text-indigo-700 underline">
            الدرر السنية
          </a>
          .
        </span>
      </p>
    </div>
  );
}
