"use client";

import { useState, useRef, useEffect, useMemo, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useHadithSearch, MIN_QUERY_LENGTH } from "@/features/search/hooks/useHadithSearch";
import { judgeSearchResults, RateLimitedError } from "@/lib/api";
import { useAutoTakhreej } from "@/features/search/hooks/useAutoTakhreej";
import AiNotice from "@/components/AiNotice";
import type { SearchJudgeItemDto } from "@/types/api";
import HadithCard from "@/features/search/components/HadithCard";
import SearchSkeleton from "@/features/search/components/SearchSkeleton";
import SearchEmptyState from "@/features/search/components/SearchEmptyState";
import SearchBookFilters from "@/features/search/components/SearchBookFilters";
import TakhreejFloatingBar from "@/features/search/components/TakhreejFloatingBar";
import {
  Search,
  X,
  Loader2,
  FilterX,
  CheckSquare,
  SlidersHorizontal,
  Sliders,
  Sparkles,
} from "lucide-react";
import ShamelaSearchModal from "@/features/search/components/ShamelaSearchModal";
import SiteHeader from "@/components/SiteHeader";
import { ErrorState } from "@/components/StateViews";

function SearchContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQueryFromUrl = searchParams.get("q") || "";
  const initialScopeFromUrl = parseInt(searchParams.get("scope") || "0", 10);
  const initialMatchFromUrl = parseInt(searchParams.get("match") || "0", 10);

  const {
    query,
    setQuery,
    page,
    setPage,
    pageSize,
    submittedQuery,
    submitSearch,
    clearSearch,
    advancedRequest,
    setAdvancedRequest,
    data: results,
    isLoading,
    isFetching,
    isError,
    refetch,
  } = useHadithSearch(initialQueryFromUrl, initialScopeFromUrl, initialMatchFromUrl);

  const inputRef = useRef<HTMLInputElement>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const autoTakhreej = useAutoTakhreej();
  const [selectedBook, setSelectedBook] = useState<string | null>(null);
  const [isShamelaModalOpen, setIsShamelaModalOpen] = useState(false);

  // Sync URL when the submitted query updates
  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    if (submittedQuery.trim().length >= MIN_QUERY_LENGTH) {
      url.searchParams.set("q", submittedQuery.trim());
    } else {
      url.searchParams.delete("q");
    }
    window.history.replaceState(null, "", url.toString());
  }, [submittedQuery]);

  // Search runs when the user presses the button or Enter, not on every keystroke.
  const typed = query.trim();
  const canSubmit = typed.length >= MIN_QUERY_LENGTH;
  const hasUnsubmittedChange = canSubmit && typed !== submittedQuery.trim();
  const handleSubmitSearch = () => {
    if (!canSubmit) return;
    if (advancedRequest) setAdvancedRequest(null); // a new simple search replaces the advanced conditions
    submitSearch();
  };

  // Global shortcut "/" or "Ctrl+K" to focus search bar
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const isInput = ["INPUT", "TEXTAREA"].includes((e.target as HTMLElement)?.tagName);
      if ((e.key === "/" && !isInput) || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k")) {
        e.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  // Compute unique books and counts from the current search results
  const bookStats = useMemo(() => {
    if (!results || results.length === 0) return [];
    const map = new Map<string, number>();
    for (const item of results) {
      map.set(item.bookName, (map.get(item.bookName) || 0) + 1);
    }
    return Array.from(map.entries()).map(([name, count]) => ({ name, count }));
  }, [results]);

  // Filter results by selected book
  const filteredResults = useMemo(() => {
    if (!results) return [];
    if (!selectedBook) return results;
    return results.filter((h) => h.bookName === selectedBook);
  }, [results, selectedBook]);

  // ── Optional AI check of the results on this page ───────────────────
  // The labels belong to one page of results: they are used only while the results are the same ones.
  const resultsKey = useMemo(() => (results ?? []).map((h) => h.id).join(","), [results]);
  const judgeQuery = advancedRequest
    ? [...(advancedRequest.andPhrases ?? []), ...(advancedRequest.orPhrases ?? []), ...(advancedRequest.phrases ?? [])].join(" ")
    : submittedQuery;
  const [judge, setJudge] = useState<{ key: string; byId: Record<string, SearchJudgeItemDto>; status: string } | null>(null);
  const [judgeError, setJudgeError] = useState<{ key: string; message: string } | null>(null);
  const [isJudging, setIsJudging] = useState(false);
  const [hideScattered, setHideScattered] = useState(false);

  // The book filter and "hide scattered" belong to one page of results: a new search or page starts unfiltered.
  const resultsSetKey = JSON.stringify([submittedQuery, advancedRequest, page]);
  const [prevResultsSetKey, setPrevResultsSetKey] = useState(resultsSetKey);
  if (resultsSetKey !== prevResultsSetKey) {
    setPrevResultsSetKey(resultsSetKey);
    setSelectedBook(null);
    setHideScattered(false);
  }
  const activeJudge = judge && judge.key === resultsKey ? judge : null;
  const activeJudgeError = judgeError && judgeError.key === resultsKey ? judgeError.message : null;

  // "Hide non-matching" is the user's choice and is always counted; nothing is removed from the results.
  const visibleResults = useMemo(() => {
    if (!hideScattered || !activeJudge) return filteredResults;
    return filteredResults.filter((h) => activeJudge.byId[h.id]?.level !== "scattered");
  }, [filteredResults, hideScattered, activeJudge]);
  const hiddenCount = filteredResults.length - visibleResults.length;

  const runJudge = async () => {
    const key = resultsKey;
    const ids = filteredResults.slice(0, 50).map((h) => h.id);
    if (ids.length === 0 || isJudging) return;
    setIsJudging(true);
    setJudgeError(null);
    try {
      const response = await judgeSearchResults(judgeQuery, ids);
      const byId: Record<string, SearchJudgeItemDto> = { ...(judge && judge.key === key ? judge.byId : {}) };
      for (const item of response.items) byId[item.id] = item;
      setJudge({ key, byId, status: response.status });
      if (response.status === "unavailable") {
        setJudgeError({ key, message: "خدمة الذكاء الاصطناعي غير متاحة الآن، وبقيت درجة الصلة المحسوبة." });
      } else if (response.status === "timeout") {
        setJudgeError({ key, message: "انتهت مهلة الذكاء الاصطناعي، وبقيت درجة الصلة المحسوبة." });
      }
    } catch (err) {
      setJudgeError({
        key,
        message:
          err instanceof RateLimitedError
            ? "تجاوزت الحد المسموح من الطلبات. حاول بعد دقيقة."
            : "تعذر إتمام التحقق بالذكاء الاصطناعي. حاول مرة أخرى.",
      });
    } finally {
      setIsJudging(false);
    }
  };

  // Takhreej selection handlers
  const toggleSelection = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const selectAllFiltered = () => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      visibleResults.forEach((h) => next.add(h.id));
      return next;
    });
  };

  const clearSelection = () => {
    setSelectedIds(new Set());
  };

  const handleOpenSingleTree = (hadithId: string) => {
    router.push(`/tree/${hadithId}`);
  };

  const handleViewTree = () => {
    const ids = Array.from(selectedIds);
    if (ids.length === 1) {
      router.push(`/tree/${ids[0]}`);
    } else if (ids.length >= 2) {
      router.push(`/takhreej?ids=${ids.join(",")}`);
    }
  };

  const isSearchActive = !!advancedRequest || submittedQuery.trim().length >= MIN_QUERY_LENGTH;

  return (
    <div className="min-h-screen bg-surface-muted/60 flex flex-col selection:bg-brand-teal/20 selection:text-brand-dark">
      <SiteHeader>
        {selectedIds.size > 0 && (
          <div className="hidden sm:flex items-center gap-2 text-xs font-bold bg-brand-blue/10 text-brand-blue px-3 py-1.5 rounded-xl animate-in fade-in">
            <CheckSquare className="w-4 h-4" aria-hidden />
            <span>{selectedIds.size} محدد للتخريج</span>
          </div>
        )}
      </SiteHeader>

      {/* Main Container */}
      <main id="main-content" className="flex-1 max-w-4xl w-full mx-auto p-4 sm:p-6 md:pt-10 pb-28">
        {/* Search Hero */}
        <section className="text-center mb-8 space-y-2.5">
          <h1 className="text-2xl sm:text-4xl font-extrabold text-brand-dark tracking-tight">
            ابحث وخرّج الأحاديث النبوية
          </h1>
          <p className="text-xs sm:text-sm text-ink-subtle max-w-xl mx-auto font-arabic leading-relaxed">
            ابحث في المتون والرواة، وحدد الروايات من مختلف كتب السنة لرسم شجرة التخريج المقارنة وبيان مدار الإسناد.
          </p>
        </section>

        {/* Search Bar Input */}
        <form
          role="search"
          className="relative mb-6"
          onSubmit={(e) => {
            e.preventDefault();
            handleSubmitSearch();
          }}
        >
          <div className="flex items-stretch gap-2">
          <div className="relative flex flex-1 items-center">
            {/* Search Icon */}
            <div className="absolute inset-y-0 start-0 flex items-center ps-4 pointer-events-none text-slate-400">
              <Search className="w-5 h-5" />
            </div>

            {/* Input Element */}
            <input
              ref={inputRef}
              type="text"
              dir="rtl"
              className="block w-full py-4 ps-12 pe-28 text-base md:text-lg text-slate-900 placeholder:text-slate-400 bg-surface border-2 border-line/90 rounded-2xl shadow-sm hover:border-slate-300 focus:border-brand-teal focus:ring-4 focus:ring-brand-teal/15 outline-none transition-all duration-200"
              placeholder="ابحث بمتن الحديث، اسم الراوي، أو المصدر..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              enterKeyHint="search"
              autoComplete="off"
              aria-label="نص البحث"
            />

            {/* Right-side Controls (Clear & Shortcut / Loading) */}
            <div className="absolute inset-y-0 end-0 flex items-center pe-3.5 gap-2">
              {isFetching && (
                <Loader2 className="w-5 h-5 text-brand-teal-ink animate-spin" />
              )}

              {query.length > 0 && !isFetching && (
                <button
                  type="button"
                  onClick={() => {
                    clearSearch();
                    inputRef.current?.focus();
                  }}
                  className="p-1 rounded-full text-slate-400 hover:text-ink-muted hover:bg-slate-100 transition-colors cursor-pointer"
                  title="مسح البحث"
                  aria-label="مسح البحث"
                >
                  <X className="w-4 h-4" />
                </button>
              )}

              <button
                type="button"
                onClick={() => setIsShamelaModalOpen(true)}
                className={`p-2 rounded-xl transition-all cursor-pointer border ${
                  advancedRequest
                    ? "bg-brand-blue text-brand-teal border-brand-blue/30 shadow-xs"
                    : "text-slate-400 hover:text-brand-blue hover:bg-slate-100 border-transparent"
                }`}
                title="خيارات البحث المتقدم (نمط المكتبة الشاملة)"
                aria-label="خيارات البحث المتقدم"
              >
                <SlidersHorizontal className="w-4 h-4" />
              </button>

              {query.length === 0 && (
                <kbd className="hidden sm:inline-flex items-center gap-1 text-[11px] font-semibold text-ink-subtle bg-slate-100 border border-line px-2 py-1 rounded-md">
                  /
                </kbd>
              )}
            </div>
          </div>

          <button
            type="submit"
            disabled={!canSubmit}
            className="inline-flex shrink-0 items-center gap-2 rounded-2xl bg-brand-blue px-5 text-sm font-bold text-white shadow-sm transition-colors hover:bg-brand-dark disabled:cursor-not-allowed disabled:opacity-50 cursor-pointer"
            title={canSubmit ? "ابدأ البحث (Enter)" : `اكتب ${MIN_QUERY_LENGTH} أحرف على الأقل`}
          >
            <Search className="h-4 w-4" aria-hidden />
            <span>بحث</span>
          </button>
          </div>

          {/* Under-input helper */}
          {typed.length > 0 && !canSubmit ? (
            <p className="text-xs text-amber-600 mt-2 pr-1 font-medium animate-in fade-in">
              * أدخل {MIN_QUERY_LENGTH} أحرف على الأقل ثم اضغط «بحث» أو Enter.
            </p>
          ) : hasUnsubmittedChange ? (
            <p className="text-xs text-ink-subtle mt-2 pr-1 animate-in fade-in">
              اضغط «بحث» أو Enter لعرض النتائج.
            </p>
          ) : null}
        </form>

        {/* Active Shamela Advanced Query Banner */}
        {advancedRequest && (
          <div className="mb-5 p-3.5 bg-brand-blue/5 border border-brand-blue/20 rounded-2xl flex items-center justify-between gap-3 flex-wrap animate-in fade-in">
            <div className="flex items-center gap-2 flex-wrap text-xs">
              <span className="font-bold text-brand-blue flex items-center gap-1.5">
                <Sliders className="w-4 h-4 text-brand-teal-ink" />
                <span>شروط الشاملة النشطة:</span>
              </span>

              {/* AND Phrases Badges */}
              {advancedRequest.andPhrases && advancedRequest.andPhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-brand-blue/10 border border-brand-blue/25 text-brand-blue px-2 py-0.5 rounded-lg">
                  <span className="font-black">[و]:</span>
                  {advancedRequest.andPhrases.map((p, i) => (
                    <span key={i} className="bg-surface text-ink px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {/* OR Phrases Badges */}
              {advancedRequest.orPhrases && advancedRequest.orPhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-amber-500/10 border border-amber-500/25 text-amber-800 px-2 py-0.5 rounded-lg">
                  <span className="font-black">[أو]:</span>
                  {advancedRequest.orPhrases.map((p, i) => (
                    <span key={i} className="bg-surface text-ink px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {/* Fallback to legacy phrases if andPhrases/orPhrases empty */}
              {(!advancedRequest.andPhrases || advancedRequest.andPhrases.length === 0) &&
                (!advancedRequest.orPhrases || advancedRequest.orPhrases.length === 0) &&
                advancedRequest.phrases?.map((p, i) => (
                  <span key={i} className="bg-surface border border-line text-ink px-2 py-0.5 rounded-md font-semibold shadow-2xs">
                    «{p}»
                  </span>
                ))}

              {/* EXCLUDE Badges */}
              {advancedRequest.excludePhrases && advancedRequest.excludePhrases.length > 0 && (
                <div className="inline-flex items-center gap-1.5 bg-rose-500/10 border border-rose-500/25 text-rose-700 px-2 py-0.5 rounded-lg">
                  <span className="font-black">[ليس]:</span>
                  {advancedRequest.excludePhrases.map((p, i) => (
                    <span key={i} className="bg-surface text-rose-800 px-1.5 py-0.5 rounded font-semibold text-[11px] shadow-2xs">
                      «{p}»
                    </span>
                  ))}
                </div>
              )}

              {advancedRequest.isOrdered && (
                <span className="bg-amber-50 border border-amber-200 text-amber-800 px-2 py-0.5 rounded-md font-semibold">
                  مرتبة
                </span>
              )}
              {advancedRequest.isProximity && (
                <span className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-2 py-0.5 rounded-md font-semibold">
                  متقاربة ({advancedRequest.proximityWords || 15} كلمة)
                </span>
              )}
            </div>

            <div className="flex items-center gap-2 mr-auto">
              <button
                type="button"
                onClick={() => setIsShamelaModalOpen(true)}
                className="text-xs font-bold text-brand-blue hover:underline cursor-pointer"
              >
                تعديل الشروط
              </button>
              <span className="text-slate-300">•</span>
              <button
                type="button"
                onClick={() => setAdvancedRequest(null)}
                className="text-xs font-bold text-rose-600 hover:text-rose-800 hover:underline cursor-pointer"
              >
                إلغاء
              </button>
            </div>
          </div>
        )}

        {/* Filter Pills & Result Summary */}
        {isSearchActive && !isLoading && results && results.length > 0 && (
          <div className="mb-5 space-y-3">
            <div className="flex items-center justify-between flex-wrap gap-2">
              <div className="flex items-center gap-3">
                <h2 className="text-sm font-bold text-slate-700 flex items-center gap-2">
                  <span>{page > 1 || results.length >= pageSize ? `نتائج الصفحة ${page}` : "نتائج البحث"}</span>
                  <span className="px-2 py-0.5 rounded-full bg-slate-200/70 text-slate-700 text-xs font-bold">
                    {results.length}
                  </span>
                </h2>

                <div className="flex items-center gap-2 text-xs border-r border-line pr-3">
                  <button
                    type="button"
                    onClick={selectAllFiltered}
                    className="text-brand-blue hover:underline font-semibold cursor-pointer"
                  >
                    تحديد المعروض ({visibleResults.length})
                  </button>
                  {selectedIds.size > 0 && (
                    <>
                      <span className="text-slate-300">•</span>
                      <button
                        type="button"
                        onClick={clearSelection}
                        className="text-ink-subtle hover:text-ink font-semibold cursor-pointer"
                      >
                        إلغاء التحديد ({selectedIds.size})
                      </button>
                    </>
                  )}
                </div>
              </div>

              {selectedBook && (
                <button
                  type="button"
                  onClick={() => setSelectedBook(null)}
                  className="text-xs text-ink-subtle hover:text-brand-blue flex items-center gap-1 cursor-pointer"
                >
                  <FilterX className="w-3.5 h-3.5" />
                  <span>إلغاء التصفية</span>
                </button>
              )}
            </div>

            {/* Optional AI check of this page of results */}
            <div className="space-y-2">
              <div className="flex items-center gap-3 flex-wrap text-xs">
                <button
                  type="button"
                  onClick={runJudge}
                  disabled={isJudging}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-brand-blue bg-surface px-3 py-1.5 font-bold text-brand-blue transition-colors hover:bg-blue-50 disabled:opacity-60 cursor-pointer"
                  title="يقرأ الذكاء الاصطناعي مقاطع النتائج ويحكم هل تتناول معنى بحثك. خطوة اختيارية."
                >
                  {isJudging ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <Sparkles className="h-3.5 w-3.5" aria-hidden />}
                  <span>{isJudging ? "جارٍ التحقق..." : "تحقق بالذكاء الاصطناعي من نتائج هذه الصفحة"}</span>
                </button>

                {activeJudge && (
                  <label className="inline-flex items-center gap-1.5 font-semibold text-slate-700 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={hideScattered}
                      onChange={(e) => setHideScattered(e.target.checked)}
                      className="h-3.5 w-3.5"
                    />
                    <span>إخفاء ما كلماته متفرقة</span>
                  </label>
                )}

                {activeJudge && (
                  <span className="text-ink-subtle">
                    حُكم على {Object.values(activeJudge.byId).filter((i) => i.level !== "not-judged").length} من{" "}
                    {filteredResults.length}
                    {activeJudge.status === "partial" && " (بعض النتائج لم يُحكم عليها)"}
                  </span>
                )}
              </div>

              {activeJudgeError && (
                <p role="alert" className="text-xs font-semibold text-amber-800">
                  {activeJudgeError}
                </p>
              )}
              {activeJudge && <AiNotice />}
            </div>

            {/* Book Filter Pills */}
            <SearchBookFilters
              books={bookStats}
              selectedBook={selectedBook}
              onSelectBook={setSelectedBook}
              totalCount={results.length}
              isPaged={page > 1 || results.length >= pageSize}
            />
          </div>
        )}

        {/* Results Area */}
        <div>
          {/* Initial / Suggested State */}
          {!isSearchActive && (
            <SearchEmptyState
              type="initial"
              onSelectSuggestion={(sug) => {
                if (advancedRequest) setAdvancedRequest(null);
                submitSearch(sug);
                inputRef.current?.focus();
              }}
            />
          )}

          {/* Loading Skeleton */}
          {isLoading && isSearchActive && <SearchSkeleton count={4} />}

          {/* Error State */}
          {isError && (
            <ErrorState
              title="حدث خطأ أثناء البحث"
              message="تعذر الاتصال بخادم البحث. يرجى التحقق من تشغيل الخادم الخلفي وإعادة المحاولة."
              onRetry={() => refetch()}
            />
          )}

          {/* Zero Results State */}
          {!isLoading && !isError && isSearchActive && results && results.length === 0 && (
            <SearchEmptyState
              type="no_results"
              query={submittedQuery}
              onResetSearch={() => {
                clearSearch();
                inputRef.current?.focus();
              }}
            />
          )}

          {/* Results List */}
          {!isLoading && !isError && isSearchActive && results && results.length > 0 && (
            <>
              {autoTakhreej.error && (
                <div role="alert" className="mb-3 flex items-center justify-between gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-semibold text-rose-800">
                  <span>{autoTakhreej.error.message}</span>
                  <button
                    type="button"
                    onClick={autoTakhreej.clearError}
                    className="rounded-md p-1 hover:bg-rose-100 cursor-pointer"
                    aria-label="إغلاق التنبيه"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </div>
              )}
              {hiddenCount > 0 && (
                <div className="mb-3 flex items-center gap-2 rounded-xl border border-line bg-surface-muted px-3 py-2 text-xs text-slate-700">
                  <span>
                    تم إخفاء {hiddenCount} نتيجة كلماتها متفرقة بحسب حكم الذكاء الاصطناعي، وقد يخطئ.
                  </span>
                  <button
                    type="button"
                    onClick={() => setHideScattered(false)}
                    className="font-bold text-brand-blue hover:underline cursor-pointer"
                  >
                    عرضها
                  </button>
                </div>
              )}
              {filteredResults.length === 0 ? (
                <div className="bg-surface rounded-2xl border border-line/80 p-8 text-center space-y-3 my-4">
                  <p className="text-ink-muted text-sm">
                    لا توجد أحاديث مطابقة في كتاب{" "}
                    <span className="font-bold text-ink">«{selectedBook}»</span>.
                  </p>
                  <button
                    type="button"
                    onClick={() => setSelectedBook(null)}
                    className="text-xs font-bold text-brand-blue hover:underline cursor-pointer"
                  >
                    عرض النتائج من كافة الكتب ({results.length})
                  </button>
                </div>
              ) : (
                <div className="space-y-4">
                  {visibleResults.map((hadith) => (
                    <HadithCard
                      key={hadith.id}
                      hadith={hadith}
                      searchQuery={submittedQuery}
                      highlightPhrases={advancedRequest?.phrases}
                      judgement={activeJudge?.byId[hadith.id]}
                      isSelected={selectedIds.has(hadith.id)}
                      isAutoTakhreejLoading={autoTakhreej.loadingId === hadith.id}
                      onToggleSelect={toggleSelection}
                      onOpenSingleTree={handleOpenSingleTree}
                      onAutoTakhreej={autoTakhreej.start}
                    />
                  ))}
                </div>
              )}

              {/* Pagination Controls */}
              {(page > 1 || results.length >= pageSize) && (
                <div className="mt-8 flex items-center justify-center gap-4">
                  <button
                    type="button"
                    disabled={page <= 1 || isFetching}
                    onClick={() => {
                      setPage((p) => Math.max(1, p - 1));
                      window.scrollTo({ top: 0, behavior: "smooth" });
                    }}
                    className="px-4 py-2 rounded-xl border border-line bg-surface text-xs font-bold text-slate-700 hover:bg-surface-muted disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer shadow-2xs"
                  >
                    الصفحة السابقة
                  </button>
                  <span className="text-xs font-bold text-ink-muted bg-surface border border-line px-3.5 py-2 rounded-xl shadow-2xs">
                    صفحة {page}
                  </span>
                  <button
                    type="button"
                    disabled={results.length < pageSize || isFetching}
                    onClick={() => {
                      setPage((p) => p + 1);
                      window.scrollTo({ top: 0, behavior: "smooth" });
                    }}
                    className="px-4 py-2 rounded-xl border border-line bg-surface text-xs font-bold text-slate-700 hover:bg-surface-muted disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer shadow-2xs"
                  >
                    الصفحة التالية
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </main>

      {/* Floating Action Bar for Takhreej */}
      <TakhreejFloatingBar
        selectedCount={selectedIds.size}
        onViewTree={handleViewTree}
        onClearSelection={clearSelection}
      />

      {/* Shamela Advanced Search Modal */}
      {isShamelaModalOpen && (
        <ShamelaSearchModal
          isOpen={isShamelaModalOpen}
          onClose={() => setIsShamelaModalOpen(false)}
          onSearch={(req) => {
            clearSearch();
            setAdvancedRequest(req);
          }}
          initialRequest={advancedRequest ?? undefined}
          initialQuery={query}
        />
      )}
    </div>
  );
}

export default function SearchPageClient() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center">
          <Loader2 className="w-8 h-8 text-brand-teal-ink animate-spin" />
        </div>
      }
    >
      <SearchContent />
    </Suspense>
  );
}
