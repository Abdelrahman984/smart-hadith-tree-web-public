import type { Metadata } from "next";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import CorpusCatalog from "@/features/books/CorpusCatalog";
import Breadcrumbs from "@/features/books/Breadcrumbs";

export const metadata: Metadata = {
  title: "فهرس الدواوين",
  description: "تصفح الدواوين الحديثية المسندة الـ 31 بأبوابها وأحاديثها.",
};

export default function BooksPage() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-7xl p-4 pt-6 sm:p-6 sm:pt-8">
        <Breadcrumbs items={[{ label: "الرئيسية", href: "/" }, { label: "فهرس الدواوين" }]} />
        <h1 className="text-2xl font-bold text-brand-dark sm:text-3xl">فهرس الدواوين</h1>
        <p className="mb-8 mt-1 text-sm text-ink-muted sm:text-base">
          اختر ديواناً لتصفح أبوابه، ثم افتح أي حديث لرسم شجرة إسناده.
        </p>
        <CorpusCatalog headingLevel={2} />
      </main>
      <SiteFooter />
    </>
  );
}
