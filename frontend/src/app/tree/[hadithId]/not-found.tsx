import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import { EmptyState } from "@/components/StateViews";

export default function TreeNotFound() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="flex flex-1 items-center justify-center p-4">
        <EmptyState title="لم يتم العثور على الحديث" message="قد يكون الرابط غير صحيح أو أن الحديث غير موجود في القاعدة.">
          <Link href="/search" className="rounded-xl bg-brand-blue px-4 py-2 text-sm font-bold text-white hover:bg-brand-dark">
            العودة للبحث
          </Link>
        </EmptyState>
      </main>
    </>
  );
}
