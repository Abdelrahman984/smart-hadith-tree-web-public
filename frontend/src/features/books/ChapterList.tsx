"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ListFilter } from "lucide-react";
import { getChapters } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { bookHref } from "./bookParams";

export default function ChapterList({ bookName }: { bookName: string }) {
  const { data: chapters, isLoading, isError, refetch } = useQuery({
    queryKey: ["chapters", bookName],
    queryFn: () => getChapters(bookName),
  });

  if (isLoading) return <LoadingState message="جارٍ تحميل الأبواب..." />;
  if (isError || !chapters) {
    return <ErrorState title="تعذر تحميل الأبواب" message="تعذر الاتصال بالخادم. تأكد من تشغيله ثم أعد المحاولة." onRetry={() => refetch()} />;
  }
  if (chapters.length === 0) return <EmptyState title="لا توجد أبواب متاحة لهذا الكتاب." />;

  return (
    <>
      <p className="mb-4 text-sm font-semibold text-ink-muted">عدد الأبواب: {chapters.length}</p>
      <ul className="grid grid-cols-1 gap-3 md:grid-cols-2">
        {chapters.map((chapter) => (
          <li key={chapter}>
            <Link
              href={bookHref(bookName, chapter)}
              className="flex h-full items-center gap-3 rounded-xl border border-line/80 bg-surface p-4 shadow-xs transition-all hover:border-brand-teal hover:shadow-md"
            >
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand-teal/10 text-brand-teal-ink">
                <ListFilter className="h-4 w-4" aria-hidden />
              </span>
              <span className="font-semibold text-ink">{chapter}</span>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
