import { getBookMeta } from "@/lib/bookTheme";
import type { NarratorNodeData } from "../utils/graphTypes";

const MAX_VISIBLE_BOOKS = 4;

/** First residence (or the place of death) as the narrator's city. */
const getPrimaryCity = (residence?: string | null, death?: string | null) => {
  if (residence) {
    const first = residence.split(/[،,-]/)[0]?.trim();
    if (first) return first;
  }
  return death?.trim() || null;
};

/** Small chips under a narrator's name: city, how many hadiths he has, تدليس, اختلاط. */
export function NarratorBadges({ data }: { data: NarratorNodeData }) {
  const city = getPrimaryCity(data.residencePlaces, data.deathPlace);
  const count = data.uniqueHadithCount;
  if (!city && count == null && !data.isMudallis && !data.hasMukhtalit) return null;

  return (
    <div className="flex gap-1 justify-center mt-2 flex-wrap">
      {city && (
        <span
          className="px-2 py-0.5 text-[10px] font-semibold text-slate-700 bg-slate-100 border border-line rounded-full"
          title={`بلدان الإقامة: ${data.residencePlaces || "غير محدد"}${data.deathPlace ? ` | الوفاة: ${data.deathPlace}` : ""}`}
        >
          📍 {city}
        </span>
      )}
      {count != null && count > 0 && (
        <span
          className={`px-2 py-0.5 text-[10px] font-semibold rounded-full ${
            count <= 5
              ? "bg-amber-100 text-amber-800 border border-amber-300"
              : count >= 500
                ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
                : "bg-blue-50 text-blue-700 border border-blue-200"
          }`}
          title={`أطراف الأحاديث: ${count}${data.totalNarrationsCount ? ` | إجمالي الأسانيد: ${data.totalNarrationsCount}` : ""}`}
        >
          {count <= 5 ? `مقل (${count})` : count >= 500 ? `مكثر (${count})` : `${count} حديث`}
        </span>
      )}
      {data.isMudallis && (
        <span className="px-2 py-0.5 text-[10px] font-bold text-orange-900 bg-orange-100 border border-orange-300 rounded-full" title="موصوف بالتدليس">
          مدلس
        </span>
      )}
      {data.hasMukhtalit && (
        <span className="px-2 py-0.5 text-[10px] font-bold text-yellow-900 bg-yellow-100 border border-yellow-300 rounded-full" title="اختلط في آخر عمره">
          اختلط
        </span>
      )}
    </div>
  );
}

/** Pill of the books whose chains pass through the narrator (comparative tree). */
export function SourceBookBadges({ books }: { books: string[] }) {
  if (books.length === 0) return null;
  const visible = books.slice(0, MAX_VISIBLE_BOOKS);
  const hidden = books.slice(MAX_VISIBLE_BOOKS);

  return (
    <div className="absolute -top-3 left-2 flex items-center gap-0.5 bg-surface rounded-full px-1.5 py-0.5 shadow-xs border border-line z-10 whitespace-nowrap">
      {visible.map((book) => {
        const meta = getBookMeta(book);
        return (
          <span
            key={book}
            className="min-w-[18px] h-[18px] px-1 flex items-center justify-center text-[9px] text-white rounded-full font-bold shrink-0"
            style={{ backgroundColor: meta.color }}
            title={meta.name}
          >
            {meta.code}
          </span>
        );
      })}
      {hidden.length > 0 && (
        <span
          className="min-w-[20px] h-[18px] px-1 flex items-center justify-center text-[9px] text-slate-700 bg-slate-100 border border-slate-300 rounded-full font-bold shrink-0"
          title={hidden.map((b) => getBookMeta(b).name).join("، ")}
        >
          +{hidden.length}
        </span>
      )}
    </div>
  );
}
