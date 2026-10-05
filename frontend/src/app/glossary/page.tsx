import type { Metadata } from "next";
import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import { GLOSSARY } from "@/features/glossary/glossary";

export const metadata: Metadata = {
  title: "قاموس المصطلحات",
  description: "تعريفات مبسطة لمصطلحات علم الحديث المستخدمة في الأداة، مع مقابلها الإنجليزي.",
};

export default function GlossaryPage() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-3xl p-4 pt-8 sm:p-6 sm:pt-12">
        <h1 className="mb-2 text-3xl font-bold text-brand-dark">قاموس المصطلحات</h1>
        <p className="mb-8 text-sm leading-relaxed text-ink-muted">
          تعريفات مبسّطة لتقريب المصطلحات لغير المتخصصين، وليست فصلاً في خلافات أهل العلم في حدودها. للتحقيق الدقيق ارجع إلى كتب
          مصطلح الحديث وأهل الاختصاص. ومصادر الجرح والتعديل والعلل في{" "}
          <Link href="/sources" className="font-semibold text-brand-blue hover:underline">
            صفحة المصادر
          </Link>
          .
        </p>

        <dl className="space-y-4">
          {GLOSSARY.map((e) => (
            <div key={e.id} id={e.id} className="scroll-mt-20 rounded-xl border border-line bg-surface p-4 target:ring-2 target:ring-brand-blue">
              <dt className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <span className="text-lg font-bold text-slate-900">{e.term}</span>
                <span className="text-xs text-ink-subtle" dir="ltr" lang="en">
                  {e.english} — {e.englishGloss}
                </span>
              </dt>
              <dd className="mt-1.5 text-sm leading-relaxed text-slate-700">{e.definition}</dd>
            </div>
          ))}
        </dl>
      </main>
      <SiteFooter />
    </>
  );
}
