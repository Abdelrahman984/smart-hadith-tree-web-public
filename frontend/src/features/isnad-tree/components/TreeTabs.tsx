"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Check, Copy } from "lucide-react";
import { ErrorState, LoadingState } from "@/components/StateViews";
import IlalPanel from "@/features/ilal/components/IlalPanel";
import { useIlalForHadith } from "@/features/ilal/hooks/useIlal";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import { bookHref } from "@/features/books/bookParams";
import type { IsnadTreeResponseDto } from "@/types/api";

/** «المتن»: the full text of the hadith, to read and copy, and where to find its book. */
export function TreeMatnTab({ tree }: { tree: IsnadTreeResponseDto }) {
  const [copied, setCopied] = useState(false);

  const copy = () => {
    navigator.clipboard.writeText(tree.matnArabic);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <>
      <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-2">
        <h2 className="font-bold text-base text-ink">
          {tree.bookName} · حديث رقم {tree.hadithNumber}
        </h2>
        <button
          type="button"
          onClick={copy}
          className="flex items-center gap-1 rounded-md px-2 py-1 text-xs font-semibold text-ink-muted hover:bg-slate-100 cursor-pointer"
          title="نسخ نص المتن"
        >
          {copied ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
          <span>{copied ? "تم النسخ" : "نسخ"}</span>
        </button>
      </div>
      <p className="text-ink leading-loose font-arabic text-sm text-justify whitespace-pre-wrap select-text">{tree.matnArabic}</p>
      <Link href={bookHref(tree.bookName)} className="inline-block text-sm font-semibold text-brand-blue hover:underline">
        أبواب {tree.bookName}
      </Link>
    </>
  );
}

/**
 * «العلل»: gathers the hadith's turuq and analyzes them. It asks the server only once the tab has been shown, and the
 * report is shared with the graph so it can mark the links and narrators the findings name.
 */
export function TreeIlalTab({ hadithId, active }: { hadithId: string; active: boolean }) {
  const [requested, setRequested] = useState(false);
  if (active && !requested) setRequested(true);

  const { data, isLoading, isError, refetch } = useIlalForHadith(hadithId, requested);
  const setReport = useIlalStore((s) => s.setReport);

  useEffect(() => {
    if (data) setReport(data);
  }, [data, setReport]);
  useEffect(() => () => useIlalStore.getState().reset(), []);

  if (!requested) return null;
  if (isLoading) return <LoadingState message="جارٍ جمع الطرق وفحصها..." />;
  if (isError) {
    return <ErrorState title="تعذر فحص العلل" message="تعذر فحص العلل لهذا الحديث. تأكد من تشغيل الخادم ثم أعد المحاولة." onRetry={() => refetch()} />;
  }
  return data ? <IlalPanel report={data} /> : null;
}
