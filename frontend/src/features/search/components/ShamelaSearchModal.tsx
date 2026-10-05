"use client";

import { useRef, useState } from "react";
import { useDialogFocus } from "@/components/useDialogFocus";
import { SearchRequestDto, SearchScope } from "@/types/api";
import {
  X,
  Plus,
  Trash2,
  Sliders,
  RotateCcw,
  Search,
  Layers,
} from "lucide-react";

interface ShamelaSearchModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSearch: (request: SearchRequestDto) => void;
  initialRequest?: SearchRequestDto;
  initialQuery?: string;
}

type TabType = "and" | "or" | "exclude";

export default function ShamelaSearchModal({
  isOpen,
  onClose,
  onSearch,
  initialRequest,
  initialQuery,
}: ShamelaSearchModalProps) {
  const [activeTab, setActiveTab] = useState<TabType>("and");

  // AND phrases (یلزم وجود کل العبارات)
  const [andPhrases, setAndPhrases] = useState<string[]>(() => {
    if (initialRequest?.andPhrases && initialRequest.andPhrases.length > 0) {
      return initialRequest.andPhrases;
    }
    // Backward compat check
    if (initialRequest?.operator === 0 && initialRequest.phrases && initialRequest.phrases.length > 0) {
      return initialRequest.phrases;
    }
    if (initialQuery && initialQuery.trim().length > 0) {
      return [initialQuery.trim(), ""];
    }
    return ["", ""];
  });

  // OR phrases (یکفی وجود أي من العبارات)
  const [orPhrases, setOrPhrases] = useState<string[]>(() => {
    if (initialRequest?.orPhrases && initialRequest.orPhrases.length > 0) {
      return initialRequest.orPhrases;
    }
    if (initialRequest?.operator === 1 && initialRequest.phrases && initialRequest.phrases.length > 0) {
      return initialRequest.phrases;
    }
    return [""];
  });

  // Exclude phrases (لیس)
  const [excludePhrases, setExcludePhrases] = useState<string[]>(() => {
    if (initialRequest?.excludePhrases && initialRequest.excludePhrases.length > 0) {
      return initialRequest.excludePhrases;
    }
    return [""];
  });

  const [isOrdered, setIsOrdered] = useState<boolean>(
    initialRequest?.isOrdered ?? false
  );
  const [isProximity, setIsProximity] = useState<boolean>(
    initialRequest?.isProximity ?? false
  );
  const [proximityWords, setProximityWords] = useState<number>(
    initialRequest?.proximityWords ?? 15
  );
  const [scope, setScope] = useState<SearchScope>(
    initialRequest?.scope ?? 1 // Default to Matn
  );

  const dialogRef = useRef<HTMLDivElement>(null);
  useDialogFocus(dialogRef, isOpen, onClose);

  if (!isOpen) return null;

  // Handlers for AND phrases
  const handleAddAnd = () => {
    if (andPhrases.length < 6) setAndPhrases([...andPhrases, ""]);
  };
  const handleRemoveAnd = (idx: number) => {
    if (andPhrases.length > 1) setAndPhrases(andPhrases.filter((_, i) => i !== idx));
  };
  const handleAndChange = (idx: number, val: string) => {
    const next = [...andPhrases];
    next[idx] = val;
    setAndPhrases(next);
  };

  // Handlers for OR phrases
  const handleAddOr = () => {
    if (orPhrases.length < 6) setOrPhrases([...orPhrases, ""]);
  };
  const handleRemoveOr = (idx: number) => {
    if (orPhrases.length > 1) setOrPhrases(orPhrases.filter((_, i) => i !== idx));
  };
  const handleOrChange = (idx: number, val: string) => {
    const next = [...orPhrases];
    next[idx] = val;
    setOrPhrases(next);
  };

  // Handlers for Exclude phrases
  const handleAddExclude = () => {
    if (excludePhrases.length < 6) setExcludePhrases([...excludePhrases, ""]);
  };
  const handleRemoveExclude = (idx: number) => {
    if (excludePhrases.length > 1) setExcludePhrases(excludePhrases.filter((_, i) => i !== idx));
  };
  const handleExcludeChange = (idx: number, val: string) => {
    const next = [...excludePhrases];
    next[idx] = val;
    setExcludePhrases(next);
  };

  const handleReset = () => {
    setAndPhrases(["", ""]);
    setOrPhrases([""]);
    setExcludePhrases([""]);
    setIsOrdered(false);
    setIsProximity(false);
    setProximityWords(15);
    setScope(1);
    setActiveTab("and");
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanAnd = andPhrases.map((p) => p.trim()).filter((p) => p.length > 0);
    const cleanOr = orPhrases.map((p) => p.trim()).filter((p) => p.length > 0);
    const cleanExclude = excludePhrases.map((p) => p.trim()).filter((p) => p.length > 0);

    if (cleanAnd.length === 0 && cleanOr.length === 0) return;

    const request: SearchRequestDto = {
      andPhrases: cleanAnd,
      orPhrases: cleanOr,
      excludePhrases: cleanExclude,
      // Provide combined phrases for backward compatibility & highlighting
      phrases: [...cleanAnd, ...cleanOr],
      operator: cleanAnd.length > 0 ? 0 : 1,
      isOrdered,
      isProximity,
      proximityWords,
      scope,
    };

    onSearch(request);
    onClose();
  };

  const arabicNumbers = ["١", "٢", "٣", "٤", "٥", "٦"];

  const activeAndCount = andPhrases.filter((p) => p.trim().length > 0).length;
  const activeOrCount = orPhrases.filter((p) => p.trim().length > 0).length;
  const activeExcludeCount = excludePhrases.filter((p) => p.trim().length > 0).length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div
        dir="rtl"
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby="shamela-modal-title"
        className="outline-none relative w-full max-w-2xl bg-surface rounded-3xl shadow-2xl border border-line overflow-hidden flex flex-col max-h-[90vh] animate-in zoom-in-95 duration-200"
      >
        {/* Modal Header */}
        <div className="px-6 py-5 bg-gradient-to-r from-slate-900 via-brand-blue to-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-white/10 flex items-center justify-center border border-white/10 text-brand-teal">
              <Sliders className="w-5 h-5" />
            </div>
            <div>
              <h2 id="shamela-modal-title" className="text-lg font-bold flex items-center gap-2">
                <span>البحث المتقدم</span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-brand-teal/20 text-brand-teal border border-brand-teal/30 font-normal">
                  نمط المكتبة الشاملة
                </span>
              </h2>
              <p className="text-xs text-slate-300">
                يمكنك دمج شروط الإلزام [و] والبدائل [أو] والاستبعاد [ليس] في نفس عملية البحث
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-2 rounded-xl text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
            aria-label="إغلاق"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Form Content */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Shamela Tabs: [ و ] | [ أو ] | [ ليس ] */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <label className="text-xs font-bold text-slate-700 block">
                أقسام وشروط البحث المنطقية:
              </label>
              <span className="text-[11px] text-ink-subtle">
                يمكن استخدام كافة الألسنة معاً
              </span>
            </div>

            <div className="grid grid-cols-3 gap-2 bg-slate-100 p-1.5 rounded-2xl border border-line/80">
              {/* Tab AND */}
              <button
                type="button"
                onClick={() => setActiveTab("and")}
                className={`flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                  activeTab === "and"
                    ? "bg-surface text-brand-blue shadow-sm border border-line"
                    : "text-ink-subtle hover:text-ink"
                }`}
              >
                <span className="text-base font-black text-brand-blue">[ و ]</span>
                <span className="hidden sm:inline">يلزم وجودها</span>
                {activeAndCount > 0 && (
                  <span className="w-5 h-5 rounded-full bg-brand-blue text-white text-[11px] flex items-center justify-center font-bold">
                    {activeAndCount}
                  </span>
                )}
              </button>

              {/* Tab OR */}
              <button
                type="button"
                onClick={() => setActiveTab("or")}
                className={`flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                  activeTab === "or"
                    ? "bg-surface text-brand-blue shadow-sm border border-line"
                    : "text-ink-subtle hover:text-ink"
                }`}
              >
                <span className="text-base font-black text-amber-600">[ أو ]</span>
                <span className="hidden sm:inline">يكفي أحدها</span>
                {activeOrCount > 0 && (
                  <span className="w-5 h-5 rounded-full bg-amber-600 text-white text-[11px] flex items-center justify-center font-bold">
                    {activeOrCount}
                  </span>
                )}
              </button>

              {/* Tab EXCLUDE */}
              <button
                type="button"
                onClick={() => setActiveTab("exclude")}
                className={`flex items-center justify-center gap-2 py-2.5 px-3 rounded-xl text-xs sm:text-sm font-bold transition-all cursor-pointer ${
                  activeTab === "exclude"
                    ? "bg-surface text-rose-700 shadow-sm border border-line"
                    : "text-ink-subtle hover:text-ink"
                }`}
              >
                <span className="text-base font-black text-rose-600">[ ليس ]</span>
                <span className="hidden sm:inline">استبعاد</span>
                {activeExcludeCount > 0 && (
                  <span className="w-5 h-5 rounded-full bg-rose-600 text-white text-[11px] flex items-center justify-center font-bold">
                    {activeExcludeCount}
                  </span>
                )}
              </button>
            </div>
          </div>

          {/* Active Tab Panel */}
          {activeTab === "and" && (
            <div className="space-y-3 p-4 bg-brand-blue/5 border border-brand-blue/15 rounded-2xl animate-in fade-in duration-150">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-xs font-bold text-brand-blue flex items-center gap-1.5">
                    <span className="text-base font-black">[ و ]</span>
                    <span>يلزم وجود كل هذه العبارات معاً (AND)</span>
                  </h3>
                  <p className="text-[11px] text-ink-subtle mt-0.5">
                    لن تظهر النتيجة إلا إذا كانت جميع هذه العبارات واردة في الحديث
                  </p>
                </div>
              </div>

              <div className="space-y-2.5">
                {andPhrases.map((phrase, idx) => (
                  <div key={idx} className="flex items-center gap-2">
                    <span className="w-7 h-9 rounded-xl bg-surface border border-brand-blue/20 text-brand-blue font-bold text-sm flex items-center justify-center shrink-0 shadow-2xs">
                      {arabicNumbers[idx] || idx + 1}
                    </span>
                    <input
                      type="text"
                      dir="rtl"
                      value={phrase}
                      onChange={(e) => handleAndChange(idx, e.target.value)}
                      placeholder={
                        idx === 0
                          ? "العبارة الإلزامية الأولى (مثال: نهى رسول الله)..."
                          : idx === 1
                          ? "العبارة الإلزامية الثانية (مثال: عن بيع)..."
                          : `عبارة إلزامية ${idx + 1}...`
                      }
                      className="flex-1 py-2.5 px-3.5 text-sm bg-surface border border-line rounded-xl focus:border-brand-teal focus:ring-3 focus:ring-brand-teal/15 outline-none transition-all"
                    />
                    {andPhrases.length > 1 && (
                      <button
                        type="button"
                        onClick={() => handleRemoveAnd(idx)}
                        className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-xl transition-colors cursor-pointer"
                        title="حذف هذا السطر"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                ))}
              </div>

              {andPhrases.length < 6 && (
                <button
                  type="button"
                  onClick={handleAddAnd}
                  className="inline-flex items-center gap-1.5 text-xs font-bold text-brand-blue hover:text-brand-dark transition-colors cursor-pointer pt-1"
                >
                  <Plus className="w-4 h-4" />
                  <span>إضافة عبارة إلزامية أخرى ({arabicNumbers[andPhrases.length] || andPhrases.length + 1})</span>
                </button>
              )}

              {/* Modifiers (مرتبة / متقاربة) under AND tab */}
              <div className="mt-4 pt-3 border-t border-brand-blue/15 space-y-3">
                <h4 className="text-xs font-bold text-slate-700">خيارات الترتيب والسياق لعبارات [ و ]:</h4>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
                  <label className="flex items-start gap-2.5 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={isOrdered}
                      onChange={(e) => setIsOrdered(e.target.checked)}
                      className="mt-0.5 w-4 h-4 text-brand-blue rounded-md border-slate-300 focus:ring-brand-teal"
                    />
                    <div>
                      <span className="font-bold text-ink block">مرتبة (In-Order)</span>
                      <span className="text-[11px] text-ink-subtle">
                        تشترط ورود العبارة (٢) بعد (١) في سياق الحديث
                      </span>
                    </div>
                  </label>

                  <label className="flex items-start gap-2.5 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={isProximity}
                      onChange={(e) => setIsProximity(e.target.checked)}
                      className="mt-0.5 w-4 h-4 text-brand-blue rounded-md border-slate-300 focus:ring-brand-teal"
                    />
                    <div>
                      <span className="font-bold text-ink block">متقاربة (Proximity)</span>
                      <span className="text-[11px] text-ink-subtle">
                        تشترط ورود العبارات في نفس الجملة/السياق
                      </span>
                    </div>
                  </label>
                </div>

                {isProximity && (
                  <div className="pt-2 border-t border-line/60 flex items-center gap-3 animate-in fade-in">
                    <span className="text-xs text-ink-muted">أقصى مسافة فاصلة بين العبارات:</span>
                    <input
                      type="number"
                      min={3}
                      max={50}
                      value={proximityWords}
                      onChange={(e) => setProximityWords(Number(e.target.value))}
                      className="w-16 py-1 px-2 text-xs text-center font-bold border border-slate-300 rounded-lg bg-surface"
                    />
                    <span className="text-xs text-ink-subtle">كلمة</span>
                  </div>
                )}
              </div>
            </div>
          )}

          {activeTab === "or" && (
            <div className="space-y-3 p-4 bg-amber-500/5 border border-amber-500/20 rounded-2xl animate-in fade-in duration-150">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-xs font-bold text-amber-700 flex items-center gap-1.5">
                    <span className="text-base font-black">[ أو ]</span>
                    <span>يكفي وجود أي من هذه العبارات (OR)</span>
                  </h3>
                  <p className="text-[11px] text-ink-subtle mt-0.5">
                    ستظهر النتيجة إذا وردت عبارة واحدة على الأقل من هذه القائمة
                  </p>
                </div>
              </div>

              <div className="space-y-2.5">
                {orPhrases.map((phrase, idx) => (
                  <div key={idx} className="flex items-center gap-2">
                    <span className="w-7 h-9 rounded-xl bg-surface border border-amber-500/25 text-amber-700 font-bold text-sm flex items-center justify-center shrink-0 shadow-2xs">
                      {arabicNumbers[idx] || idx + 1}
                    </span>
                    <input
                      type="text"
                      dir="rtl"
                      value={phrase}
                      onChange={(e) => handleOrChange(idx, e.target.value)}
                      placeholder={
                        idx === 0
                          ? "عبارة بديلة أولى (مثال: الغرر)..."
                          : idx === 1
                          ? "عبارة بديلة ثانية (مثال: الملامسة)..."
                          : `عبارة بديلة ${idx + 1}...`
                      }
                      className="flex-1 py-2.5 px-3.5 text-sm bg-surface border border-line rounded-xl focus:border-amber-500 focus:ring-3 focus:ring-amber-500/15 outline-none transition-all"
                    />
                    {orPhrases.length > 1 && (
                      <button
                        type="button"
                        onClick={() => handleRemoveOr(idx)}
                        className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-xl transition-colors cursor-pointer"
                        title="حذف هذا السطر"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                ))}
              </div>

              {orPhrases.length < 6 && (
                <button
                  type="button"
                  onClick={handleAddOr}
                  className="inline-flex items-center gap-1.5 text-xs font-bold text-amber-700 hover:text-amber-900 transition-colors cursor-pointer pt-1"
                >
                  <Plus className="w-4 h-4" />
                  <span>إضافة عبارة بديلة أخرى ({arabicNumbers[orPhrases.length] || orPhrases.length + 1})</span>
                </button>
              )}
            </div>
          )}

          {activeTab === "exclude" && (
            <div className="space-y-3 p-4 bg-rose-500/5 border border-rose-500/20 rounded-2xl animate-in fade-in duration-150">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-xs font-bold text-rose-700 flex items-center gap-1.5">
                    <span className="text-base font-black">[ ليس ]</span>
                    <span>استبعاد نصوص وكلمات (NOT)</span>
                  </h3>
                  <p className="text-[11px] text-ink-subtle mt-0.5">
                    أي حديث يحتوي على أي من هذه العبارات سيتم استبعاده تماماً من النتائج
                  </p>
                </div>
              </div>

              <div className="space-y-2.5">
                {excludePhrases.map((phrase, idx) => (
                  <div key={idx} className="flex items-center gap-2">
                    <span className="w-7 h-9 rounded-xl bg-surface border border-rose-500/25 text-rose-700 font-bold text-sm flex items-center justify-center shrink-0 shadow-2xs">
                      {arabicNumbers[idx] || idx + 1}
                    </span>
                    <input
                      type="text"
                      dir="rtl"
                      value={phrase}
                      onChange={(e) => handleExcludeChange(idx, e.target.value)}
                      placeholder={
                        idx === 0
                          ? "عبارة مستبعدة أولى (مثال: رمضان)..."
                          : `عبارة مستبعدة ${idx + 1}...`
                      }
                      className="flex-1 py-2.5 px-3.5 text-sm bg-surface border border-line rounded-xl focus:border-rose-500 focus:ring-3 focus:ring-rose-500/15 outline-none transition-all"
                    />
                    {excludePhrases.length > 1 && (
                      <button
                        type="button"
                        onClick={() => handleRemoveExclude(idx)}
                        className="p-2 text-slate-400 hover:text-rose-600 hover:bg-rose-50 rounded-xl transition-colors cursor-pointer"
                        title="حذف هذا السطر"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                ))}
              </div>

              {excludePhrases.length < 6 && (
                <button
                  type="button"
                  onClick={handleAddExclude}
                  className="inline-flex items-center gap-1.5 text-xs font-bold text-rose-700 hover:text-rose-900 transition-colors cursor-pointer pt-1"
                >
                  <Plus className="w-4 h-4" />
                  <span>إضافة عبارة مستبعدة أخرى ({arabicNumbers[excludePhrases.length] || excludePhrases.length + 1})</span>
                </button>
              )}
            </div>
          )}

          {/* Combined Summary Preview Bar */}
          <div className="p-3 bg-surface-muted border border-line/80 rounded-2xl flex items-center gap-2 text-xs text-ink-muted flex-wrap">
            <Layers className="w-4 h-4 text-brand-blue shrink-0" />
            <span className="font-bold text-slate-700">المعادلة المنطقية الحالية:</span>
            {activeAndCount > 0 ? (
              <span className="bg-brand-blue/10 text-brand-blue px-2 py-0.5 rounded-md font-semibold">
                [و]: {activeAndCount} عبارة إلزامية
              </span>
            ) : (
              <span className="text-ink-subtle">لا يوجد إلزام</span>
            )}
            <span className="text-slate-300">•</span>
            {activeOrCount > 0 ? (
              <span className="bg-amber-500/10 text-amber-800 px-2 py-0.5 rounded-md font-semibold">
                [أو]: {activeOrCount} بدائل
              </span>
            ) : (
              <span className="text-ink-subtle">لا توجد بدائل</span>
            )}
            <span className="text-slate-300">•</span>
            {activeExcludeCount > 0 ? (
              <span className="bg-rose-500/10 text-rose-700 px-2 py-0.5 rounded-md font-semibold">
                [ليس]: {activeExcludeCount} مستبعد
              </span>
            ) : (
              <span className="text-ink-subtle">لا يوجد استبعاد</span>
            )}
          </div>

          {/* Search Scope */}
          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-700 block">مجال البحث:</label>
            <div className="flex bg-slate-100 p-1 rounded-xl border border-line/60 text-xs">
              {[
                { label: "في المتن فقط (موصى به)", val: 1 },
                { label: "في السند والرواة", val: 2 },
                { label: "بحث شامل (الكل)", val: 0 },
              ].map((item) => (
                <button
                  key={item.val}
                  type="button"
                  onClick={() => setScope(item.val as SearchScope)}
                  className={`flex-1 py-1.5 px-2 rounded-lg font-medium transition-all cursor-pointer ${
                    scope === item.val
                      ? "bg-surface text-brand-blue shadow-xs font-bold"
                      : "text-ink-subtle hover:text-ink"
                  }`}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>
        </form>

        {/* Modal Footer Controls */}
        <div className="p-4 px-6 bg-surface-muted border-t border-line flex items-center justify-between gap-3">
          <button
            type="button"
            onClick={handleReset}
            className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-semibold text-ink-muted hover:text-slate-900 hover:bg-slate-200/70 rounded-xl transition-colors cursor-pointer"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>إعادة تعيين</span>
          </button>

          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold text-ink-muted hover:text-ink rounded-xl transition-colors cursor-pointer"
            >
              إلغاء
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              className="inline-flex items-center gap-2 px-5 py-2.5 bg-brand-blue text-white hover:bg-slate-900 text-xs sm:text-sm font-bold rounded-xl shadow-sm hover:shadow-md transition-all cursor-pointer"
            >
              <Search className="w-4 h-4 text-brand-teal" />
              <span>تنفيذ البحث المتقدم</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
