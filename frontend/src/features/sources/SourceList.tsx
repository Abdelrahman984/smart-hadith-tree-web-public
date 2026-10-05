import type { SourceItem } from "./sourcesData";

/** A list of books with author, what the project takes from each, and an optional count. */
export default function SourceList({ items }: { items: SourceItem[] }) {
  return (
    <ul className="space-y-3">
      {items.map((item) => (
        <li key={item.name} className="rounded-xl border border-line bg-surface p-4">
          <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
            <div className="min-w-0">
              <h3 className="font-bold text-ink">{item.name}</h3>
              {item.author && <p className="text-xs text-ink-subtle">{item.author}</p>}
            </div>
            {item.figure && (
              <span className="shrink-0 rounded-lg bg-surface-muted px-2.5 py-1 text-xs font-semibold text-ink-muted">
                {item.figure}
              </span>
            )}
          </div>
          <p className="mt-2 text-sm leading-relaxed text-ink-muted">{item.use}</p>
        </li>
      ))}
    </ul>
  );
}

export function BulletList({ items, tone = "neutral" }: { items: string[]; tone?: "neutral" | "caution" }) {
  const dot = tone === "caution" ? "bg-amber-500" : "bg-brand-teal";
  return (
    <ul className="space-y-2">
      {items.map((text) => (
        <li key={text} className="flex items-start gap-2.5 text-sm leading-relaxed text-ink-muted">
          <span className={`mt-2 h-1.5 w-1.5 shrink-0 rounded-full ${dot}`} aria-hidden />
          <span>{text}</span>
        </li>
      ))}
    </ul>
  );
}
