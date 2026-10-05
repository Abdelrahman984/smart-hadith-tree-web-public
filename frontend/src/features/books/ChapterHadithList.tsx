"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Network } from "lucide-react";
import { getBookHadiths } from "@/lib/api";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";

export default function ChapterHadithList({ bookName, chapter }: { bookName: string; chapter: string }) {
  const { data: hadiths, isLoading, isError, refetch } = useQuery({
    queryKey: ["hadiths", bookName, chapter],
    queryFn: () => getBookHadiths(bookName, chapter),
  });

  if (isLoading) return <LoadingState message="جارٍ تحميل الأحاديث..." />;
  if (isError || !hadiths) {
    return <ErrorState title="تعذر تحميل الأحاديث" message="تعذر الاتصال بالخادم. تأكد من تشغيله ثم أعد المحاولة." onRetry={() => refetch()} />;
  }
  if (hadiths.length === 0) return <EmptyState title="لا توجد أحاديث في هذا الباب." />;

  return (
    <>
      <h2 className="mb-4 text-sm font-semibold text-ink-muted">الأحاديث ({hadiths.length})</h2>
      <ul className="space-y-4">
        {hadiths.map((hadith) => (
          <li key={hadith.id}>
            <Link
              href={`/tree/${hadith.id}`}
              className="group block rounded-xl border border-line/80 bg-surface p-4 shadow-xs transition-all hover:border-brand-teal hover:shadow-md sm:p-5"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-semibold text-brand-blue">حديث رقم {hadith.hadithNumber}</span>
                <span className="inline-flex items-center gap-1 text-xs font-semibold text-ink-subtle group-hover:text-brand-blue">
                  <Network className="h-3.5 w-3.5" aria-hidden />
                  شجرة الإسناد
                </span>
              </div>
              <p className="mt-3 whitespace-pre-wrap text-justify font-arabic text-base leading-loose text-ink md:text-lg">
                {hadith.matnArabic || hadith.matnSnippet}
              </p>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
