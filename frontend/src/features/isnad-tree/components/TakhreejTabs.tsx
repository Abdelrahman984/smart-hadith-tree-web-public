"use client";

import Link from "next/link";
import { useState } from "react";
import { BookOpen, Check, Copy, ExternalLink, FileDiff } from "lucide-react";
import EvidenceBadge from "@/components/EvidenceBadge";
import IlalPanel from "@/features/ilal/components/IlalPanel";
import MatnDiffView from "@/features/ilal/components/MatnDiffView";
import { getBookMeta } from "@/lib/bookTheme";
import type { ComparativeHadithSourceDto, ComparativeTreeResponseDto, IlalTariqDto } from "@/types/api";
import { useGraphViewStore } from "../store/useGraphViewStore";
import { compareMatns } from "../utils/matnDiff";
import type { CompanionRef, SourceGroups } from "../utils/groupSources";

/** «المتون»: the automatic grade, then the text of each compared narration: routes of the hadith, then witnesses. */
export function MatnTab({ data, groups }: { data: ComparativeTreeResponseDto; groups: SourceGroups }) {
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [comparing, setComparing] = useState<Set<string>>(new Set());
  const focusBook = useGraphViewStore((s) => s.focusBook);
  const setFocusBook = useGraphViewStore((s) => s.setFocusBook);

  // Narrations are compared with the first route ("الأصل"); the labels of the comparison need the book and number.
  const base = groups.turuq[0] ?? data.sources[0];
  const turuq: IlalTariqDto[] = data.sources.map((s) => ({ hadithId: s.hadithId, bookName: s.bookName, hadithNumber: s.hadithNumber, isMarfu: true }));
  const textOf = (s: ComparativeHadithSourceDto) => s.matnArabic || s.matnSnippet;

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };
  const toggleCompare = (id: string) =>
    setComparing((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });

  const renderCard = (source: ComparativeHadithSourceDto, companion?: CompanionRef) => {
    const meta = getBookMeta(source.bookName);
    const text = textOf(source);
    const isBase = source.hadithId === base?.hadithId;
    const isFocused = focusBook === meta.name;
    const isComparing = comparing.has(source.hadithId);
    return (
      <div
        id={`matn-card-${source.hadithId}`}
        key={source.hadithId}
        className={`bg-surface-muted/70 p-3.5 rounded-xl border space-y-2.5 scroll-mt-2 ${isFocused ? "border-brand-blue ring-1 ring-brand-blue/40" : "border-line/80"}`}
      >
        <div className="flex items-center justify-between gap-2">
          <div className="flex min-w-0 flex-wrap items-center gap-1.5">
            <button
              type="button"
              onClick={() => setFocusBook(isFocused ? null : meta.name)}
              aria-pressed={isFocused}
              title="إبراز مسارات هذا الكتاب على الشجرة"
              className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md border text-xs font-bold cursor-pointer ${meta.badgeClass}`}
            >
              <span
                className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
                style={{ backgroundColor: meta.color }}
              >
                {meta.code}
              </span>
              <span>{source.bookName}</span>
              <span className="opacity-50">•</span>
              <span>حديث رقم {source.hadithNumber}</span>
            </button>
            {isBase && groups.turuq.length > 1 && <span className="rounded-full bg-slate-200/70 px-2 py-0.5 text-[11px] font-semibold text-ink-muted">الأصل</span>}
            {companion && (
              <span className="rounded-full border border-purple-200 bg-purple-50 px-2 py-0.5 text-[11px] font-semibold text-purple-800" title="رواية عن صحابي آخر">
                شاهد: {companion.name}
              </span>
            )}
          </div>
          <div className="flex items-center gap-1 shrink-0">
            <Link
              href={`/tree/${source.hadithId}`}
              className="p-1 rounded-md text-slate-400 hover:text-brand-blue hover:bg-slate-200/60 transition-colors"
              title="عرض شجرة هذه الرواية منفردة"
            >
              <ExternalLink className="w-4 h-4" />
            </Link>
            <button
              onClick={() => handleCopy(text, source.hadithId)}
              className="p-1 rounded-md text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
              title="نسخ نص المتن"
            >
              {copiedId === source.hadithId ? <Check className="w-4 h-4 text-emerald-600" /> : <Copy className="w-4 h-4" />}
            </button>
          </div>
        </div>
        <p className="text-ink leading-loose font-arabic text-sm text-justify whitespace-pre-wrap select-text">{text}</p>
        {!isBase && base && (
          <div>
            <button
              type="button"
              onClick={() => toggleCompare(source.hadithId)}
              aria-expanded={isComparing}
              className="flex items-center gap-1 text-[11px] font-semibold text-brand-blue hover:underline cursor-pointer"
            >
              <FileDiff className="h-3.5 w-3.5" aria-hidden />
              {isComparing ? "إخفاء المقارنة" : "قارن بالأصل"}
            </button>
            {isComparing && (
              <MatnDiffView
                comparison={compareMatns({ hadithId: base.hadithId, text: textOf(base) }, { hadithId: source.hadithId, text })}
                turuq={turuq}
              />
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <>
      {data.calculatedGrade && (
        <div className="bg-surface-muted border border-line rounded-xl p-3.5">
          <h3 className="font-bold text-ink mb-1 flex items-center gap-2 text-sm">
            <span>التقدير الآلي للطرق:</span>
            <span className="text-ink bg-slate-200/70 px-2 py-0.5 rounded-md">{data.calculatedGrade}</span>
          </h3>
          <div className="mt-1.5 flex flex-wrap items-center gap-2">
            <EvidenceBadge status="verify" />
            <span className="text-[11px] text-ink">
              تقدير آلي من الأسانيد المسجلة وليس حكماً معتمداً؛ للحكم المعتمد راجع{" "}
              <a href="https://dorar.net/hadith" target="_blank" rel="noopener noreferrer" className="font-semibold underline">
                الدرر السنية
              </a>
              .
            </span>
          </div>
          {data.taqwiyahDetails && <p className="text-xs text-slate-700 leading-relaxed mt-1">{data.taqwiyahDetails}</p>}
        </div>
      )}

      <div className="flex items-center justify-between border-b border-slate-100 pb-2">
        <div className="flex items-center gap-2">
          <BookOpen className="w-5 h-5 text-brand-blue" />
          <h2 className="font-bold text-base text-ink">متون الروايات</h2>
        </div>
        <span className="text-xs bg-slate-100 text-ink-muted px-2 py-0.5 rounded-full font-semibold">
          {groups.turuq.length} {groups.turuq.length === 1 ? "رواية" : "روايات"}
        </span>
      </div>

      <div className="space-y-4">{groups.turuq.map((s) => renderCard(s))}</div>

      {groups.shawahid.length > 0 && (
        <section aria-label="الشواهد" className="space-y-3">
          <div className="border-b border-slate-100 pb-2">
            <h2 className="font-bold text-base text-ink">
              الشواهد <span className="text-xs font-semibold text-ink-subtle">({groups.shawahid.length})</span>
            </h2>
            <p className="mt-1 text-[11px] leading-relaxed text-ink-subtle">
              روايات عن صحابي آخر غير {groups.main?.name}: حديث بمعنى الأول، وليست طريقاً آخر له. عُرضت منفصلة هنا للتنبيه، وما
              تزال داخل الشجرة وفحص العلل.
            </p>
          </div>
          <div className="space-y-4">{groups.shawahid.map(({ source, companion }) => renderCard(source, companion))}</div>
        </section>
      )}
    </>
  );
}

/** «العلل»: the findings the comparison carries. */
export function IlalTab({ data }: { data: ComparativeTreeResponseDto }) {
  return data.ilalReport ? (
    <IlalPanel report={data.ilalReport} />
  ) : (
    <p className="text-sm text-ink-subtle">لا يتوفر فحص للعلل لهذه الطرق.</p>
  );
}
