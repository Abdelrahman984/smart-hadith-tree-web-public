"use client";

import { X } from "lucide-react";
import { useState } from "react";
import { getBookMeta } from "@/lib/bookTheme";
import type { ComparativeHadithSourceDto } from "@/types/api";

const MAX_VISIBLE = 4;
/** A takhreej compares at least two narrations, so the last two cannot be removed. */
const MIN_SOURCES = 2;

interface SourceChipsProps {
  sources: ComparativeHadithSourceDto[];
  /** Canonical name of the book highlighted on the graph, if any. */
  focusBook: string | null;
  /** Narrations through another Companion (shawahid), marked on their chip. */
  shawahidIds?: Set<string>;
  onSelect: (source: ComparativeHadithSourceDto) => void;
  onRemove: (source: ComparativeHadithSourceDto) => void;
}

/** One chip per compared narration: select it to highlight its book's routes, or remove it from the comparison. */
export default function SourceChips({ sources, focusBook, shawahidIds, onSelect, onRemove }: SourceChipsProps) {
  const [showAll, setShowAll] = useState(false);
  const shown = showAll ? sources : sources.slice(0, MAX_VISIBLE);
  const canRemove = sources.length > MIN_SOURCES;

  return (
    <div className="flex gap-2 items-center flex-nowrap overflow-x-auto sm:flex-wrap min-w-0 pb-0.5">
      {shown.map((source) => {
        const meta = getBookMeta(source.bookName);
        const isFocused = focusBook === meta.name;
        return (
          <div
            key={source.hadithId}
            className={`shrink-0 whitespace-nowrap rounded-full border text-xs sm:text-sm font-semibold flex items-center transition-colors ${meta.badgeClass} ${
              isFocused ? "ring-2 ring-offset-1 ring-brand-blue" : ""
            }`}
          >
            <button
              type="button"
              onClick={() => onSelect(source)}
              aria-pressed={isFocused}
              title="إبراز مسارات هذا الكتاب على الشجرة"
              className="flex items-center gap-1.5 rounded-full py-1 ps-2.5 pe-2 cursor-pointer"
            >
              <span
                className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
                style={{ backgroundColor: meta.color }}
              >
                {meta.code}
              </span>
              <span>{source.bookName}</span>
              <span className="opacity-50">|</span>
              <span>{source.hadithNumber}</span>
              {shawahidIds?.has(source.hadithId) && (
                <span className="rounded-full bg-purple-100 px-1.5 text-[10px] font-bold text-purple-800" title="رواية عن صحابي آخر">
                  شاهد
                </span>
              )}
            </button>
            <button
              type="button"
              onClick={() => onRemove(source)}
              disabled={!canRemove}
              aria-label={`إزالة ${source.bookName} ${source.hadithNumber} من المقارنة`}
              title={canRemove ? "إزالة هذه الرواية من المقارنة" : "تحتاج المقارنة روايتين على الأقل"}
              className="me-1 rounded-full p-1 opacity-60 hover:opacity-100 hover:bg-black/5 disabled:opacity-25 disabled:pointer-events-none cursor-pointer"
            >
              <X className="h-3 w-3" aria-hidden />
            </button>
          </div>
        );
      })}
      {sources.length > MAX_VISIBLE && (
        <button
          type="button"
          onClick={() => setShowAll((v) => !v)}
          aria-expanded={showAll}
          title={showAll ? "إخفاء" : sources.slice(MAX_VISIBLE).map((s) => `${s.bookName} ${s.hadithNumber}`).join("، ")}
          className="shrink-0 px-2.5 py-1 rounded-full border border-slate-300 bg-slate-100 text-xs sm:text-sm font-bold text-slate-700 hover:bg-slate-200 transition-colors cursor-pointer"
        >
          {showAll ? "إخفاء" : `+${sources.length - MAX_VISIBLE}`}
        </button>
      )}
    </div>
  );
}
