"use client"; // Error boundaries must be Client Components

import { useEffect } from "react";
import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import { ErrorState } from "@/components/StateViews";

export default function Error({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <>
      <SiteHeader />
      <main id="main-content" className="flex flex-1 items-center justify-center p-4">
        <ErrorState
          title="تعذر عرض هذه الصفحة"
          message="تعذر الاتصال بالخادم أو حدث خطأ غير متوقع. تأكد من تشغيل الخادم ثم أعد المحاولة."
          onRetry={() => retry()}
        >
          <Link href="/" className="rounded-xl border border-rose-200 bg-surface px-4 py-2 text-xs font-bold text-rose-800 hover:bg-rose-100">
            الصفحة الرئيسية
          </Link>
        </ErrorState>
      </main>
    </>
  );
}
