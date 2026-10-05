import Link from "next/link";
import { getBookMeta } from "@/lib/bookTheme";
import { CORPUS_CATEGORIES } from "@/lib/corpus";

/** The 31 books grouped by category, each linking to its chapters. Used on the home page and the books index. */
export default function CorpusCatalog({ headingLevel = 3 }: { headingLevel?: 2 | 3 }) {
  const HeadingTag = headingLevel === 2 ? "h2" : "h3";
  return (
    <div className="space-y-8">
      {CORPUS_CATEGORIES.map((category) => (
        <div
          key={category.title}
          className="bg-surface-muted/70 rounded-2xl p-4 sm:p-6 border border-line/80"
        >
          <div className="mb-4">
            <HeadingTag className="text-lg font-bold text-brand-dark">{category.title}</HeadingTag>
            <p className="text-xs sm:text-sm text-ink-subtle">{category.subtitle}</p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
            {category.books.map((book) => {
              const meta = getBookMeta(book.name);
              return (
                <Link
                  key={book.name}
                  href={`/books/${encodeURIComponent(book.name)}`}
                  className="group bg-surface p-4 rounded-xl border border-line/80 hover:border-brand-teal hover:shadow-md transition-all flex items-center justify-between gap-3"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <span
                      className={`w-10 h-10 shrink-0 rounded-xl flex items-center justify-center text-xs font-bold border ${meta.badgeClass}`}
                    >
                      {meta.code}
                    </span>
                    <div className="min-w-0">
                      <div className="font-bold text-ink group-hover:text-brand-blue transition-colors truncate">
                        {book.name}
                      </div>
                      <div className="text-xs text-ink-subtle truncate">{book.author}</div>
                    </div>
                  </div>

                  <span className="shrink-0 text-xs font-semibold font-latin px-2.5 py-1 rounded-lg bg-slate-100 text-ink-muted group-hover:bg-brand-teal/10 group-hover:text-brand-dark transition-colors">
                    {book.hadithsCount}
                  </span>
                </Link>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
