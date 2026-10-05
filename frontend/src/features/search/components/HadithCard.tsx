import { useState } from "react";
import { HadithSearchResultDto, SearchJudgeItemDto, SearchJudgeLevel } from "@/types/api";
import { getBookMeta } from "@/lib/bookTheme";
import {
  Book,
  GitCompareArrows,
  CheckSquare,
  Square,
  Loader2,
  Copy,
  Check,
  Network,
  ChevronDown,
  ChevronUp,
} from "lucide-react";

interface HadithCardProps {
  hadith: HadithSearchResultDto;
  searchQuery: string;
  highlightPhrases?: string[];
  /** The AI's reading of this result, present only after the user asked for the AI check. */
  judgement?: SearchJudgeItemDto;
  isSelected: boolean;
  isAutoTakhreejLoading: boolean;
  onToggleSelect: (id: string) => void;
  onOpenSingleTree: (id: string) => void;
  onAutoTakhreej: (id: string) => void;
}

import HighlightedText from "./HighlightedText";

const JUDGE_UI: Record<SearchJudgeLevel, { label: string; className: string }> = {
  match: { label: "يطابق البحث", className: "bg-emerald-50 text-emerald-800 border-emerald-200" },
  partial: { label: "يطابق جزئياً", className: "bg-amber-50 text-amber-800 border-amber-200" },
  scattered: { label: "كلمات متفرقة", className: "bg-rose-50 text-rose-800 border-rose-200" },
  "not-judged": { label: "لم يُحكم عليه", className: "bg-surface-muted text-ink-muted border-line" },
};

function relevanceColor(percent: number) {
  if (percent >= 80) return "bg-emerald-500";
  if (percent >= 50) return "bg-amber-500";
  return "bg-slate-400";
}

export default function HadithCard({
  hadith,
  searchQuery,
  highlightPhrases,
  judgement,
  isSelected,
  isAutoTakhreejLoading,
  onToggleSelect,
  onOpenSingleTree,
  onAutoTakhreej,
}: HadithCardProps) {
  const [copied, setCopied] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);

  const fullText = hadith.matnArabic || hadith.matnSnippet || "";
  const isLongText = fullText.length > 280;
  const bookMeta = getBookMeta(hadith.bookName);

  const handleCopy = (e: React.MouseEvent) => {
    e.stopPropagation();
    navigator.clipboard.writeText(fullText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleCardClick = () => {
    // Selecting matn text to copy it is not a click on the card.
    if (window.getSelection()?.toString()) return;
    onToggleSelect(hadith.id);
  };

  return (
    <article
      onClick={handleCardClick}
      className={`group relative bg-surface rounded-2xl p-5 border transition-all duration-200 cursor-pointer select-none ${
        isSelected
          ? "border-brand-blue ring-2 ring-brand-blue/30 bg-blue-50/20 shadow-md"
          : "border-line/80 hover:border-slate-300 hover:shadow-md hover:-translate-y-0.5"
      }`}
      style={{
        borderRightWidth: "4px",
        borderRightColor: isSelected ? "#1A3A5C" : bookMeta.color,
      }}
    >
      {/* Top Meta Bar */}
      <div className="flex items-start justify-between gap-3 pb-3 border-b border-slate-100/90">
        <div className="flex items-center gap-2.5 flex-wrap">
          {/* Always-active Takhreej Selection Checkbox */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onToggleSelect(hadith.id);
            }}
            className="text-brand-blue hover:scale-110 transition-transform cursor-pointer p-0.5"
            aria-pressed={isSelected}
            aria-label="تحديد الحديث للتخريج"
            title={isSelected ? "إلغاء التحديد" : "تحديد الحديث للمقارنة والتخريج"}
          >
            {isSelected ? (
              <CheckSquare className="w-5 h-5 text-brand-blue fill-brand-blue/15" />
            ) : (
              <Square className="w-5 h-5 text-slate-300 hover:text-ink-subtle transition-colors" />
            )}
          </button>

          {/* Book Badge */}
          <span
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-bold border transition-colors ${bookMeta.badgeClass}`}
          >
            <Book className="w-3.5 h-3.5" />
            <span>{hadith.bookName}</span>
          </span>

          {/* Hadith Number */}
          <span className="text-xs font-semibold text-ink-subtle bg-slate-100 px-2 py-1 rounded-lg">
            حديث #{hadith.hadithNumber}
          </span>

          {/* Chapter */}
          {hadith.chapter && (
            <span
              className="text-xs text-ink-muted bg-surface-muted border border-line/70 px-2.5 py-1 rounded-lg font-medium truncate max-w-xs"
              title={hadith.chapter}
            >
              <HighlightedText text={hadith.chapter} query={searchQuery} phrases={highlightPhrases} />
            </span>
          )}
        </div>

        {/* Quick Copy Action */}
        <div className="flex items-center gap-1.5 shrink-0">
          <button
            type="button"
            onClick={handleCopy}
            className={`flex items-center gap-1 text-xs px-2.5 py-1 rounded-lg transition-colors cursor-pointer border ${
              copied
                ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                : "bg-surface-muted hover:bg-slate-100 text-ink-muted border-line/80"
            }`}
            title="نسخ نص الحديث كاملاً"
          >
            {copied ? (
              <>
                <Check className="w-3.5 h-3.5 text-emerald-600" />
                <span className="font-semibold">تم النسخ</span>
              </>
            ) : (
              <>
                <Copy className="w-3.5 h-3.5 text-ink-subtle" />
                <span>نسخ</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Relevance (computed by the server, no AI) */}
      {hadith.relevancePercent != null && (
        <div className="mt-3 flex items-center gap-2 text-xs" title={hadith.relevanceReason}>
          <div
            className="h-1.5 w-24 shrink-0 overflow-hidden rounded-full bg-slate-100"
            role="meter"
            aria-label="درجة صلة الحديث بالبحث"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={hadith.relevancePercent}
          >
            <div className={`h-full rounded-full ${relevanceColor(hadith.relevancePercent)}`} style={{ width: `${hadith.relevancePercent}%` }} />
          </div>
          <span className="font-bold text-slate-700">{hadith.relevancePercent}%</span>
          {hadith.relevanceReason && <span className="text-ink-subtle">{hadith.relevanceReason}</span>}
        </div>
      )}

      {/* AI check (only after the user asked for it) */}
      {judgement && (
        <div className={`mt-2 rounded-lg border px-3 py-2 text-xs leading-relaxed ${JUDGE_UI[judgement.level].className}`}>
          <p className="font-bold">
            <span aria-hidden>✦ </span>
            {JUDGE_UI[judgement.level].label}
          </p>
          {judgement.reason && <p className="mt-0.5">{judgement.reason}</p>}
          {judgement.quote && <p className="mt-0.5 opacity-80">«{judgement.quote}»</p>}
        </div>
      )}

      {/* Matn Content */}
      <div className="pt-3.5">
        <p
          className={`text-ink text-base md:text-lg leading-loose font-arabic text-justify select-text whitespace-pre-wrap ${
            !isExpanded && isLongText ? "line-clamp-4" : ""
          }`}
        >
          <HighlightedText text={fullText} query={searchQuery} phrases={highlightPhrases} />
        </p>

        {isLongText && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              setIsExpanded(!isExpanded);
            }}
            className="mt-2 inline-flex items-center gap-1 text-xs font-bold text-brand-blue hover:text-blue-800 transition-colors cursor-pointer"
          >
            {isExpanded ? (
              <>
                <span>عرض أقل</span>
                <ChevronUp className="w-3.5 h-3.5" />
              </>
            ) : (
              <>
                <span>عرض المتن كاملاً...</span>
                <ChevronDown className="w-3.5 h-3.5" />
              </>
            )}
          </button>
        )}
      </div>

      {/* Footer Actions */}
      <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <div className="text-xs">
          {isSelected ? (
            <span className="font-bold text-brand-blue flex items-center gap-1">
              <span>✓ محدد للتخريج المقارن</span>
            </span>
          ) : (
            <span className="text-ink-subtle">انقر على البطاقة لتحديد الحديث للمقارنة</span>
          )}
        </div>

        <div className="flex items-center gap-2 mr-auto">
          {/* Subtle Single Chain button for inspection */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onOpenSingleTree(hadith.id);
            }}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold text-ink-muted hover:text-slate-900 hover:bg-slate-100 rounded-xl transition-colors cursor-pointer"
            title="معاينة إسناد هذا الكتاب منفرداً"
          >
            <Network className="w-3.5 h-3.5 text-slate-400" />
            <span>سند منفرد</span>
          </button>

          {/* Primary Auto Takhreej Button */}
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onAutoTakhreej(hadith.id);
            }}
            disabled={isAutoTakhreejLoading}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-bold bg-brand-blue text-white hover:bg-slate-900 rounded-xl transition-all shadow-xs hover:shadow-sm cursor-pointer"
            title="البحث التلقائي عن شواهد ومتون الروايات الأخرى ورسم شجرة التخريج الموحدة"
          >
            {isAutoTakhreejLoading ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin text-brand-teal" />
            ) : (
              <GitCompareArrows className="w-3.5 h-3.5 text-brand-teal" />
            )}
            <span>تخريج فوري</span>
          </button>
        </div>
      </div>
    </article>
  );
}
