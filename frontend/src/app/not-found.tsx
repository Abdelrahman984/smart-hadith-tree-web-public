import type { Metadata } from "next";
import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import { EmptyState } from "@/components/StateViews";

export const metadata: Metadata = {
  title: "الصفحة غير موجودة",
};

export default function NotFound() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="flex flex-1 items-center justify-center p-4">
        <EmptyState title="الصفحة غير موجودة" message="تحقق من الرابط، أو ابدأ من البحث أو فهرس الدواوين.">
          <Link href="/search" className="rounded-xl bg-brand-blue px-4 py-2 text-sm font-bold text-white hover:bg-brand-dark">
            البحث
          </Link>
          <Link href="/books" className="rounded-xl border border-line bg-surface px-4 py-2 text-sm font-bold text-slate-700 hover:bg-surface-muted">
            فهرس الدواوين
          </Link>
        </EmptyState>
      </main>
      <SiteFooter />
    </>
  );
}
