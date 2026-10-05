import Link from "next/link";
import { ChevronLeft } from "lucide-react";

export interface Crumb {
  label: string;
  /** Omitted for the current page. */
  href?: string;
}

export default function Breadcrumbs({ items }: { items: Crumb[] }) {
  return (
    <nav aria-label="مسار التصفح" className="mb-4 text-sm">
      <ol className="flex flex-wrap items-center gap-1 text-ink-subtle">
        {items.map((item, i) => (
          <li key={i} className="flex min-w-0 items-center gap-1">
            {i > 0 && <ChevronLeft className="h-3.5 w-3.5 shrink-0 text-slate-400" aria-hidden />}
            {item.href ? (
              <Link href={item.href} className="font-semibold text-brand-blue hover:underline">
                {item.label}
              </Link>
            ) : (
              <span aria-current="page" className="truncate font-semibold text-slate-700">
                {item.label}
              </span>
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
