"use client";

import { useState } from "react";
import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import { AlertTriangle, CheckCircle2, Loader2, SearchX, ShieldCheck } from "lucide-react";
import AiNotice from "@/components/AiNotice";
import { verifyHadith } from "@/lib/api";
import { useAutoTakhreej } from "@/features/search/hooks/useAutoTakhreej";
import type { HadithVerificationResultDto, HadithVerificationStatus } from "@/types/api";

const MAX_CHARS = 600;

const STATUS_UI: Record<HadithVerificationStatus, { label: string; tone: string; Icon: typeof CheckCircle2 }> = {
  exact: { label: "النص موجود في المصدر", tone: "border-emerald-300 bg-emerald-50 text-emerald-900", Icon: CheckCircle2 },
  variant: { label: "لفظ قريب — قارن بالمتن", tone: "border-amber-300 bg-amber-50 text-amber-900", Icon: AlertTriangle },
  "not-found": { label: "لم نجد مصدراً مطابقاً", tone: "border-slate-300 bg-surface-muted text-ink", Icon: SearchX },
  invalid: { label: "تعذّر الفحص", tone: "border-slate-300 bg-surface-muted text-ink", Icon: AlertTriangle },
};

export default function VerifyPageClient() {
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<HadithVerificationResultDto | null>(null);
  const autoTakhreej = useAutoTakhreej();

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!text.trim() || loading) return;
    setLoading(true);
    setError(null);
    try {
      setResult(await verifyHadith(text));
    } catch {
      setResult(null);
      setError("تعذّر الاتصال بالخادم. حاول مرة أخرى.");
    } finally {
      setLoading(false);
    }
  }

  const ui = result ? STATUS_UI[result.status] : null;

  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-3xl p-4 pt-8 sm:p-6 sm:pt-12">
        <h1 className="mb-2 flex items-center gap-2 text-3xl font-bold text-brand-dark">
          <ShieldCheck className="h-7 w-7 text-brand-teal-ink" aria-hidden /> التحقق من نص حديث
        </h1>
        <p className="mb-6 text-sm leading-relaxed text-ink-muted">
          الصق نصاً يُنسب إلى النبي ﷺ لنبحث عنه في الدواوين المتاحة. لا ننسب النص إلا إلى مصدر وجدناه فعلاً، وإن لم نجده قلنا ذلك دون الحكم
          على النص. وهذه الأداة لا تُصدر حكماً بالصحة أو الضعف.
        </p>

        <form onSubmit={onSubmit} className="space-y-3">
          <label htmlFor="hadith-text" className="block text-sm font-semibold text-slate-700">
            نص الحديث
          </label>
          <textarea
            id="hadith-text"
            value={text}
            onChange={(e) => setText(e.target.value.slice(0, MAX_CHARS))}
            rows={4}
            dir="rtl"
            className="w-full rounded-xl border border-slate-300 p-3 text-base leading-relaxed focus:border-brand-blue focus:outline-none focus:ring-2 focus:ring-brand-blue/30"
            placeholder="مثال: إنما الأعمال بالنيات وإنما لكل امرئ ما نوى"
          />
          <div className="flex items-center justify-between">
            <span className="text-xs text-ink-subtle">
              {text.length} / {MAX_CHARS}
            </span>
            <button
              type="submit"
              disabled={loading || !text.trim()}
              className="inline-flex items-center gap-2 rounded-xl bg-brand-blue px-5 py-2 text-sm font-bold text-white shadow-sm transition-colors hover:bg-brand-dark disabled:opacity-50"
            >
              {loading && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
              تحقق
            </button>
          </div>
        </form>

        {error && (
          <p role="alert" className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
            {error}
          </p>
        )}

        {result && ui && (
          <section aria-live="polite" className="mt-6 space-y-4">
            <div className={`rounded-xl border p-4 ${ui.tone}`}>
              <p className="flex items-center gap-2 text-lg font-bold">
                <ui.Icon className="h-5 w-5" aria-hidden /> {ui.label}
              </p>
              <p className="mt-1.5 text-sm leading-relaxed">{result.explanation}</p>
              {result.unmatchedWords && result.unmatchedWords.length > 0 && (
                <p className="mt-2 text-sm font-semibold">
                  كلمات في نصك غير موجودة في المتن: {result.unmatchedWords.join("، ")}
                </p>
              )}
              {result.method === "ai+lexical" && <AiNotice className="mt-3" />}
            </div>

            {result.matches.length > 0 && (
              <ul className="space-y-3">
                {result.matches.map((m) => (
                  <li key={m.id} className="rounded-xl border border-line bg-surface p-4">
                    <div className="flex flex-wrap items-baseline justify-between gap-2">
                      <span className="font-bold text-slate-900">
                        {m.bookName} — رقم {m.hadithNumber}
                      </span>
                      <span className="text-xs text-ink-subtle">نسبة التطابق: {Math.round(m.similarity * 100)}%</span>
                    </div>
                    {m.chapter && <p className="mt-0.5 text-xs text-ink-subtle">{m.chapter}</p>}
                    <p className="mt-2 text-sm leading-loose text-ink">{m.matnArabic}</p>
                    <div className="mt-3 flex flex-wrap gap-3 text-sm font-semibold">
                      <Link href={`/tree/${m.id}`} className="text-brand-blue hover:underline">
                        شجرة الإسناد
                      </Link>
                      <button
                        type="button"
                        onClick={() => autoTakhreej.start(m.id)}
                        disabled={autoTakhreej.loadingId !== null}
                        className="inline-flex items-center gap-1.5 text-brand-blue hover:underline disabled:opacity-60 cursor-pointer"
                      >
                        {autoTakhreej.loadingId === m.id && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />}
                        التخريج والعلل
                      </button>
                    </div>
                    {autoTakhreej.error?.id === m.id && (
                      <p role="alert" className="mt-2 text-xs font-semibold text-rose-700">
                        {autoTakhreej.error.message}
                      </p>
                    )}
                  </li>
                ))}
              </ul>
            )}

            {result.status !== "invalid" && (
              <p className="text-xs leading-relaxed text-ink-subtle">
                لا يوجد حكم معتمد على الحديث في بياناتنا؛ للتحقق من حكمه راجع{" "}
                <a href="https://dorar.net/hadith" target="_blank" rel="noopener noreferrer" className="font-semibold text-indigo-700 underline">
                  الدرر السنية
                </a>
                .
              </p>
            )}
          </section>
        )}
      </main>
      <SiteFooter />
    </>
  );
}
