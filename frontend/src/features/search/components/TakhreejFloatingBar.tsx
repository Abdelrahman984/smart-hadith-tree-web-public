import { GitCompareArrows, Network, X } from "lucide-react";

interface TakhreejFloatingBarProps {
  selectedCount: number;
  onViewTree: () => void;
  onClearSelection: () => void;
}

export default function TakhreejFloatingBar({
  selectedCount,
  onViewTree,
  onClearSelection,
}: TakhreejFloatingBarProps) {
  if (selectedCount === 0) return null;

  const isMultiple = selectedCount >= 2;

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 w-[92%] max-w-xl animate-in slide-in-from-bottom-6 duration-200">
      <div className="bg-slate-900/95 backdrop-blur-md text-white px-5 py-3.5 rounded-2xl shadow-2xl border border-slate-700/80 flex items-center justify-between gap-3 flex-wrap sm:flex-nowrap">
        {/* Count & Status */}
        <div className="flex items-center gap-2 text-sm">
          <span className="w-6 h-6 rounded-full bg-brand-teal text-slate-950 font-bold flex items-center justify-center text-xs">
            {selectedCount}
          </span>
          <span className="font-semibold text-slate-200">
            {selectedCount === 1 ? "حديث محدد" : "أحاديث محددة"}
          </span>
          {!isMultiple && (
            <span className="text-[11px] text-amber-300/90 hidden sm:inline">
              (حدد حديثاً آخر للمقارنة أو اعرض سنده)
            </span>
          )}
        </div>

        {/* Action Buttons */}
        <div className="flex items-center gap-2 mr-auto">
          <button
            type="button"
            onClick={onClearSelection}
            className="px-3 py-1.5 text-xs text-slate-300 hover:text-white hover:bg-slate-800 rounded-xl transition-colors cursor-pointer flex items-center gap-1"
            title="إلغاء التحديد"
          >
            <X className="w-3.5 h-3.5" />
            <span>إلغاء</span>
          </button>

          <button
            type="button"
            onClick={onViewTree}
            className="flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs sm:text-sm font-bold bg-brand-teal text-slate-950 hover:bg-teal-300 active:scale-95 transition-all shadow-md cursor-pointer"
          >
            {isMultiple ? (
              <>
                <GitCompareArrows className="w-4 h-4" />
                <span>عرض شجرة التخريج المقارنة</span>
              </>
            ) : (
              <>
                <Network className="w-4 h-4" />
                <span>عرض شجرة السند</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
