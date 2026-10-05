export default function SearchSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-4 animate-pulse" aria-label="جاري تحميل النتائج...">
      {Array.from({ length: count }).map((_, idx) => (
        <div
          key={idx}
          className="bg-surface p-5 rounded-2xl border border-line/80 shadow-xs space-y-4"
        >
          {/* Header row */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="h-6 w-28 bg-slate-200 rounded-lg"></div>
              <div className="h-4 w-2 bg-slate-200 rounded"></div>
              <div className="h-6 w-20 bg-slate-200 rounded-lg"></div>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-7 w-16 bg-slate-200 rounded-lg"></div>
              <div className="h-7 w-20 bg-slate-200 rounded-lg"></div>
            </div>
          </div>

          {/* Matn Lines */}
          <div className="space-y-2.5 pt-1">
            <div className="h-4 bg-slate-200 rounded w-full"></div>
            <div className="h-4 bg-slate-200 rounded w-11/12"></div>
            <div className="h-4 bg-slate-200 rounded w-4/5"></div>
            <div className="h-4 bg-slate-200 rounded w-3/5"></div>
          </div>

          {/* Footer action row */}
          <div className="pt-2 border-t border-slate-100 flex items-center justify-between">
            <div className="h-4 w-32 bg-slate-200 rounded"></div>
            <div className="h-8 w-36 bg-slate-200 rounded-xl"></div>
          </div>
        </div>
      ))}
    </div>
  );
}
