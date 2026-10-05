"use client";

import { useState, useMemo } from "react";
import { useLegendOpen } from "../hooks/useLegendOpen";
import { useGraphViewStore } from "../store/useGraphViewStore";
import { GRADE_STYLE, UNRATED_STYLE } from "@/features/narrator-details/utils/gradeStyle";
import { Panel } from "@xyflow/react";
import {
 
  ChevronUp,
  Layers,
  BookOpen,
  GitFork,
  UserCheck,
  AlertTriangle,
  FileDiff,
} from "lucide-react";
import { CANONICAL_31_BOOKS, getBookMeta } from "@/lib/bookTheme";

type LegendTab = "all" | "books" | "paths" | "narrators";

const ILAL_EDGES = [
  { label: "عنعنة مدلس", hint: "مدلس روى بصيغة محتملة ولم يصرح بالسماع", color: "#ea580c", dash: "2 4" },
  { label: "لم يثبت اللقاء", hint: "ليس في شيوخه ولا تلاميذه في كتب التراجم", color: "#d97706", dash: "8 4" },
  { label: "رواية عن مختلط", hint: "لم يتبين أسمع منه قبل الاختلاط", color: "#ca8a04", dash: "6 3" },
];

/** Legend rows: a label per grade colour, taken from the same map the nodes use so they never drift apart. */
const NARRATOR_RANKS = [
  { label: "صحابي", color: GRADE_STYLE.companion.color },
  { label: "ثقة / عدل ضابط", color: GRADE_STYLE.reliable.color },
  { label: "صدوق / حسن", color: GRADE_STYLE.mostly_reliable.color },
  { label: "ضعيف", color: GRADE_STYLE.weak.color },
  { label: "متروك / كذاب", color: GRADE_STYLE.fabricator.color },
  { label: "مجهول", color: GRADE_STYLE.unknown.color },
  { label: "غير مُقيَّم (لا حكم)", color: UNRATED_STYLE.color, dashed: true },
];

interface BookLegendProps {
  activeBooks?: string[];
  /** Canonical name of the book whose edges are highlighted on the graph. */
  focusBook?: string | null;
  /** When provided, book rows become toggle buttons that focus that book on the graph. */
  onFocusBook?: (book: string | null) => void;
}

export default function BookLegend({ activeBooks, focusBook, onFocusBook }: BookLegendProps) {
  // Opened and closed from the toolbar (and its own close button); see useLegendOpen.
  const isOpen = useLegendOpen();
  const setIsOpen = useGraphViewStore((s) => s.setLegendOpen);
  const [activeTab, setActiveTab] = useState<LegendTab>("all");
  const [showAllBooks, setShowAllBooks] = useState(false);

  const displayedBooks = useMemo(() => {
    if (activeTab === "books" || showAllBooks || !activeBooks || activeBooks.length === 0) {
      return CANONICAL_31_BOOKS;
    }
    const uniqueNames = Array.from(new Set(activeBooks));
    return uniqueNames.map((name) => getBookMeta(name));
  }, [activeTab, showAllBooks, activeBooks]);

  if (!isOpen) return null;

  return (
    <Panel
      position="top-right"
      className="m-2 bg-surface/95 backdrop-blur-md w-80 max-w-[calc(100vw-1rem)] max-h-[70vh] flex flex-col overflow-hidden rounded-2xl shadow-xl border border-line/90 text-xs transition-all"
      dir="rtl"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3.5 py-2.5 bg-surface-muted/80 border-b border-line">
        <div className="flex items-center gap-2">
          <Layers className="w-4 h-4 text-brand-blue" />
          <span className="font-bold text-ink text-sm">مفتاح الرموز والمصطلحات</span>
        </div>
        <button
          onClick={() => setIsOpen(false)}
          className="p-1 rounded-lg hover:bg-slate-200/70 text-ink-subtle hover:text-slate-700 transition-colors cursor-pointer"
          title="تصغير المفتاح"
        >
          <ChevronUp className="w-4 h-4" />
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-100 bg-surface-muted/40 p-1 gap-1">
        <button
          onClick={() => setActiveTab("all")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "all"
              ? "bg-surface text-brand-blue shadow-sm border border-line/60"
              : "text-ink-subtle hover:text-ink"
          }`}
        >
          الكل
        </button>
        <button
          onClick={() => setActiveTab("books")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "books"
              ? "bg-surface text-brand-blue shadow-sm border border-line/60"
              : "text-ink-subtle hover:text-ink"
          }`}
        >
          الكتب ({CANONICAL_31_BOOKS.length})
        </button>
        <button
          onClick={() => setActiveTab("paths")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "paths"
              ? "bg-surface text-brand-blue shadow-sm border border-line/60"
              : "text-ink-subtle hover:text-ink"
          }`}
        >
          المسارات
        </button>
        <button
          onClick={() => setActiveTab("narrators")}
          className={`flex-1 py-1 rounded-md text-[11px] font-semibold transition-all cursor-pointer ${
            activeTab === "narrators"
              ? "bg-surface text-brand-blue shadow-sm border border-line/60"
              : "text-ink-subtle hover:text-ink"
          }`}
        >
          الرواة
        </button>
      </div>

      {/* Body Content */}
      <div className="p-3.5 space-y-4 min-h-0 flex-1 overflow-y-auto">
        {/* Section 1: Books */}
        {(activeTab === "all" || activeTab === "books") && (
          <div>
            <div className="flex items-center justify-between mb-2">
              <div className="flex items-center gap-1.5 text-slate-700 font-bold text-xs">
                <BookOpen className="w-3.5 h-3.5 text-ink-subtle" />
                <span>
                  {activeTab === "all" && activeBooks && activeBooks.length > 0 && !showAllBooks
                    ? `كتب الشجرة الحالية (${displayedBooks.length})`
                    : `رموز دواوين السنة (${CANONICAL_31_BOOKS.length})`}
                </span>
              </div>
              {activeTab === "all" && activeBooks && activeBooks.length > 0 && (
                <button
                  onClick={() => setShowAllBooks(!showAllBooks)}
                  className="text-[10px] font-semibold text-brand-blue hover:underline cursor-pointer"
                >
                  {showAllBooks ? "كتب الشجرة فقط" : `عرض الكل (${CANONICAL_31_BOOKS.length})`}
                </button>
              )}
            </div>
            <div className="grid grid-cols-2 gap-x-2 gap-y-1.5">
              {displayedBooks.map((book) => {
                const isFocused = focusBook === book.name;
                const content = (
                  <>
                    <span
                      className="min-w-[20px] h-5 px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shadow-xs shrink-0"
                      style={{ backgroundColor: book.color }}
                    >
                      {book.code}
                    </span>
                    <span className="text-ink-muted text-[11px] truncate" title={book.name}>
                      {book.name}
                    </span>
                  </>
                );
                return onFocusBook ? (
                  <button
                    key={book.name}
                    type="button"
                    aria-pressed={isFocused}
                    onClick={() => onFocusBook(isFocused ? null : book.name)}
                    title="إبراز مسارات هذا الكتاب"
                    className={`flex items-center gap-1.5 py-0.5 px-1 rounded-md text-start cursor-pointer transition-colors ${
                      isFocused ? "bg-slate-200 ring-1 ring-slate-400" : "hover:bg-slate-100"
                    }`}
                  >
                    {content}
                  </button>
                ) : (
                  <div key={book.name} className="flex items-center gap-1.5 py-0.5">
                    {content}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Section 2: Paths & Matn Variations */}
        {(activeTab === "all" || activeTab === "paths") && (
          <div className={activeTab === "all" ? "border-t border-slate-100 pt-3" : ""}>
            <div className="flex items-center gap-1.5 text-slate-700 font-bold mb-2 text-xs">
              <GitFork className="w-3.5 h-3.5 text-ink-subtle" />
              <span>مسارات الأسانيد والمتون</span>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0.5 bg-blue-600 rounded shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium block leading-tight">مسار كتاب منفرد</span>
                  <span className="text-ink-subtle text-[10px]">مصبوغ بلون المصدر</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-1 bg-slate-600 rounded shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium block leading-tight">مسار مشترك (جامع)</span>
                  <span className="text-ink-subtle text-[10px]">تلتقي فيه أسانيد عدة كتب</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0 border-t-2 border-dashed border-red-500 shrink-0"></div>
                <div>
                  <span className="text-red-700 font-medium flex items-center gap-1 leading-tight">
                    <span>انقطاع في السند</span>
                    <AlertTriangle className="w-3 h-3 text-red-500" />
                  </span>
                  <span className="text-ink-subtle text-[10px]">سقط تاريخي أو عدم ثبوت معاصرة</span>
                </div>
              </div>

              <div className="flex items-center gap-2.5">
                <div className="w-6 h-0 border-t-2 border-dashed border-amber-500 shrink-0"></div>
                <div>
                  <span className="text-amber-700 font-medium flex items-center gap-1 leading-tight">
                    <span>اختلاف باللفظ</span>
                    <FileDiff className="w-3 h-3 text-amber-500" />
                  </span>
                  <span className="text-ink-subtle text-[10px]">تباين في المتن بين الروايات</span>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Section 3: Narrators & Madar */}
        {(activeTab === "all" || activeTab === "narrators") && (
          <div className={activeTab === "all" ? "border-t border-slate-100 pt-3" : ""}>
            <div className="flex items-center gap-1.5 text-slate-700 font-bold mb-2 text-xs">
              <UserCheck className="w-3.5 h-3.5 text-ink-subtle" />
              <span>الرواة ومدار الإسناد</span>
            </div>

            {/* Madar & Inqita' Indicators */}
            <div className="space-y-1.5 mb-3 bg-surface-muted/60 p-2 rounded-lg border border-slate-100">
              <div className="flex items-center gap-2">
                <div className="w-3.5 h-3.5 rounded-full border-2 border-amber-400 ring-2 ring-amber-400/50 shrink-0"></div>
                <div>
                  <span className="text-slate-700 font-medium text-[11px]">مدار الإسناد: </span>
                  <span className="text-ink-subtle text-[10px]">الراوي الذي تلتقي وتتفرع عنده الطرق</span>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <AlertTriangle className="w-3.5 h-3.5 text-red-500 shrink-0" />
                <div>
                  <span className="text-slate-700 font-medium text-[11px]">انقطاع زمني: </span>
                  <span className="text-ink-subtle text-[10px]">ولد التلميذ بعد وفاة الشيخ</span>
                </div>
              </div>
              {ILAL_EDGES.map((edge) => (
                <div key={edge.label} className="flex items-center gap-2">
                  <svg width="14" height="6" className="shrink-0" aria-hidden>
                    <line x1="0" y1="3" x2="14" y2="3" stroke={edge.color} strokeWidth="2.5" strokeDasharray={edge.dash} />
                  </svg>
                  <div>
                    <span className="text-slate-700 font-medium text-[11px]">{edge.label}: </span>
                    <span className="text-ink-subtle text-[10px]">{edge.hint}</span>
                  </div>
                </div>
              ))}
            </div>

            {/* Ranks Grid */}
            <span className="text-ink-subtle text-[10px] block mb-1.5 font-semibold">ألوان رتب الجرح والتعديل:</span>
            <div className="grid grid-cols-2 gap-x-2 gap-y-1">
              {NARRATOR_RANKS.map((rank) => (
                <div key={rank.label} className="flex items-center gap-1.5">
                  <span
                    className="w-2.5 h-2.5 rounded-full shrink-0 shadow-xs"
                    style={
                      "dashed" in rank
                        ? { border: `1.5px dashed ${rank.color}` }
                        : { backgroundColor: rank.color }
                    }
                  ></span>
                  <span className="text-ink-muted text-[11px] truncate">{rank.label}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </Panel>
  );
}

