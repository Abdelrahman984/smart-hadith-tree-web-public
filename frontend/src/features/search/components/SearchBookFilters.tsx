import { getBookMeta } from "@/lib/bookTheme";

interface SearchBookFiltersProps {
  books: { name: string; count: number }[];
  selectedBook: string | null;
  onSelectBook: (book: string | null) => void;
  totalCount: number;
  /** True when the results span several pages: the counts then cover only the page shown. */
  isPaged?: boolean;
}

export default function SearchBookFilters({
  books,
  selectedBook,
  onSelectBook,
  totalCount,
  isPaged = false,
}: SearchBookFiltersProps) {
  if (books.length <= 1) return null;

  return (
    <div className="flex items-center gap-2 overflow-x-auto py-1 text-xs">
      <span className="text-ink-subtle shrink-0 font-medium ml-1">
        {isPaged ? "تصفية نتائج هذه الصفحة بالمصدر:" : "تصفية بالمصدر:"}
      </span>
      
      {/* "All" button */}
      <button
        type="button"
        onClick={() => onSelectBook(null)}
        className={`px-3 py-1.5 rounded-full font-semibold shrink-0 transition-all cursor-pointer flex items-center gap-1.5 border ${
          selectedBook === null
            ? "bg-brand-blue text-white border-brand-blue shadow-xs"
            : "bg-surface text-ink-muted border-line hover:bg-slate-100"
        }`}
      >
        <span>الكل</span>
        <span
          className={`px-1.5 py-0.2 rounded-full text-[10px] ${
            selectedBook === null ? "bg-surface/20 text-white" : "bg-slate-100 text-ink-subtle"
          }`}
        >
          {totalCount}
        </span>
      </button>

      {/* Per book button */}
      {books.map((b) => {
        const meta = getBookMeta(b.name);
        const isSelected = selectedBook === b.name;
        return (
          <button
            key={b.name}
            type="button"
            onClick={() => onSelectBook(isSelected ? null : b.name)}
            className={`px-3 py-1.5 rounded-full font-semibold shrink-0 transition-all cursor-pointer flex items-center gap-1.5 border ${
              isSelected
                ? "bg-slate-900 text-white border-slate-900 shadow-xs"
                : "bg-surface text-slate-700 border-line hover:bg-slate-100"
            }`}
          >
            <span
              className="w-2 h-2 rounded-full shrink-0"
              style={{ backgroundColor: meta.color }}
            />
            <span>{b.name}</span>
            <span
              className={`px-1.5 py-0.2 rounded-full text-[10px] ${
                isSelected ? "bg-surface/20 text-white" : "bg-slate-100 text-ink-subtle"
              }`}
            >
              {b.count}
            </span>
          </button>
        );
      })}
    </div>
  );
}
