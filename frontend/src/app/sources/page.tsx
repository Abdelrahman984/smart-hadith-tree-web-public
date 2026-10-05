import type { Metadata } from "next";
import Link from "next/link";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";
import AiNotice from "@/components/AiNotice";
import SourceList, { BulletList } from "@/features/sources/SourceList";
import {
  GRADE_ORDER,
  ILAL_LIMITS,
  ILAL_RULES,
  ILAL_SOURCES,
  ILAL_WEIGHING,
  JARH_FIGURES,
  JARH_LIMITS,
  JARH_METHOD,
  JARH_SOURCES,
} from "@/features/sources/sourcesData";
import { GRADE_STYLE } from "@/features/narrator-details/utils/gradeStyle";
import { ILLAH_TYPE_LABELS, SEVERITY_STYLES } from "@/features/ilal/utils/ilalLabels";

export const metadata: Metadata = {
  title: "المصادر والمنهج",
  description: "من أين تأتي نصوص الأحاديث وأقوال الجرح والتعديل وقواعد فحص العلل في الأداة، وما حدود كل منها.",
};

const SECTIONS = [
  { id: "books", label: "نصوص الأحاديث" },
  { id: "jarh-tadil", label: "الجرح والتعديل" },
  { id: "ilal", label: "العلل" },
  { id: "limits", label: "حدود الاستخدام" },
];

function SectionHeading({ id, children }: { id: string; children: React.ReactNode }) {
  return (
    <h2 id={id} className="scroll-mt-20 border-b border-line pb-2 text-xl font-bold text-brand-dark sm:text-2xl">
      {children}
    </h2>
  );
}

export default function SourcesPage() {
  return (
    <>
      <SiteHeader />
      <main id="main-content" className="mx-auto w-full max-w-3xl p-4 pt-8 sm:p-6 sm:pt-12">
        <h1 className="mb-2 text-3xl font-bold text-brand-dark">المصادر والمنهج</h1>
        <p className="mb-5 text-sm leading-relaxed text-ink-muted">
          هنا بيان من أين تأتي المعلومات التي تعرضها الأداة، وما الذي نأخذه من كل كتاب، وأين تنتهي حدودها. لا ننسب نصاً إلى
          مصدر لا يوجد فيه، وما لم نستطع إثباته كتبناه «غير موثَّق» ولم نفترضه.
        </p>

        <nav aria-label="أقسام الصفحة" className="mb-10 flex flex-wrap gap-2">
          {SECTIONS.map((s) => (
            <a
              key={s.id}
              href={`#${s.id}`}
              className="rounded-full border border-line bg-surface px-3 py-1 text-xs font-semibold text-ink-muted transition-colors hover:border-brand-teal hover:text-brand-blue"
            >
              {s.label}
            </a>
          ))}
        </nav>

        {/* 1. Hadith texts */}
        <section aria-labelledby="books" className="mb-12 space-y-4">
          <SectionHeading id="books">نصوص الأحاديث</SectionHeading>
          <p className="text-sm leading-relaxed text-ink-muted">
            النصوص من نسخة محلية من <strong className="text-ink">المكتبة الشاملة 4</strong>، وعددها 31 ديواناً مسنداً
            (274,597 حديثاً وأثراً بترقيم كل طبعة). يُحفظ المتن والسند كما في الطبعة ولا يُعاد تحريرهما.{" "}
            <Link href="/books" className="font-semibold text-brand-blue hover:underline">
              فهرس الدواوين
            </Link>
          </p>
          <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs leading-relaxed text-amber-900">
            النصوص تراثية، لكن للطبعات المحققة (الترقيم والتحقيق والحواشي) حقوقاً لمحققيها وناشريها، وللشاملة شروط استخدام؛
            وحال التراخيص «غير موثَّق» في سجل المصادر (<code dir="ltr">docs/challenge/sources-and-licenses.md</code>). قد تدخل
            حواشي المحققين أحياناً في النص المستخرج وليست محتوى مقصوداً.
          </p>
        </section>

        {/* 2. Jarh wa ta'dil */}
        <section aria-labelledby="jarh-tadil" className="mb-12 space-y-5">
          <SectionHeading id="jarh-tadil">الجرح والتعديل</SectionHeading>
          <p className="text-sm leading-relaxed text-ink-muted">
            يعرض درج الراوي رتبته وأقوال النقاد فيه. هذه مصادر كل جزء منه:
          </p>

          <dl className="grid grid-cols-3 gap-3 text-center">
            {JARH_FIGURES.map((f) => (
              <div key={f.label} className="flex flex-col-reverse rounded-xl border border-line bg-surface p-3">
                <dt className="text-xs text-ink-subtle">{f.label}</dt>
                <dd className="font-latin text-lg font-bold text-brand-blue sm:text-2xl">{f.value}</dd>
              </div>
            ))}
          </dl>

          <SourceList items={JARH_SOURCES} />

          <div className="space-y-2">
            <h3 className="font-bold text-ink">سلّم الرتب في الشجرة</h3>
            <ul className="flex flex-wrap gap-2">
              {GRADE_ORDER.map((g) => (
                <li
                  key={g}
                  className={`rounded-full border px-3 py-1 text-xs font-bold ${GRADE_STYLE[g].badgeClass}`}
                >
                  {GRADE_STYLE[g].label}
                </li>
              ))}
            </ul>
          </div>

          <div className="space-y-2">
            <h3 className="font-bold text-ink">المنهج</h3>
            <BulletList items={JARH_METHOD} />
          </div>
          <div className="space-y-2">
            <h3 className="font-bold text-ink">حدود معروفة</h3>
            <BulletList items={JARH_LIMITS} tone="caution" />
          </div>
        </section>

        {/* 3. Ilal */}
        <section aria-labelledby="ilal" className="mb-12 space-y-5">
          <SectionHeading id="ilal">العلل</SectionHeading>
          <p className="text-sm leading-relaxed text-ink-muted">
            فحص العلل يقارن طرق الحديث بقواعد مكتوبة في الشيفرة، ويعتمد على قوائم مأخوذة من الكتب التالية. نتائجه عون
            للباحث وليست حكماً على الحديث.
          </p>

          <SourceList items={ILAL_SOURCES} />

          <div className="space-y-2">
            <h3 className="font-bold text-ink">القواعد الست</h3>
            <div className="space-y-3">
              {ILAL_RULES.map((rule) => (
                <article key={rule.types.join("-")} className="rounded-xl border border-line bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h4 className="font-bold text-ink">{rule.types.map((t) => ILLAH_TYPE_LABELS[t]).join("، ")}</h4>
                    {rule.severities.map((sev) => (
                      <span
                        key={sev}
                        className={`rounded-full border px-2 py-0.5 text-[11px] font-semibold ${SEVERITY_STYLES[sev].badge}`}
                      >
                        {SEVERITY_STYLES[sev].label}
                      </span>
                    ))}
                  </div>
                  <p className="mt-2 text-sm leading-relaxed text-ink-muted">{rule.checks}</p>
                  <p className="mt-1.5 text-xs leading-relaxed text-ink-subtle">{rule.note}</p>
                </article>
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <h3 className="font-bold text-ink">كيف يُرجَّح بين طرفين مختلفين</h3>
            <ol className="list-decimal space-y-1.5 ps-5 text-sm leading-relaxed text-ink-muted marker:font-semibold marker:text-brand-teal-ink">
              {ILAL_WEIGHING.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
          </div>

          <div className="space-y-2">
            <h3 className="font-bold text-ink">حدود معروفة</h3>
            <BulletList items={ILAL_LIMITS} tone="caution" />
          </div>
        </section>

        {/* 4. Limits */}
        <section aria-labelledby="limits" className="mb-4 space-y-4">
          <SectionHeading id="limits">حدود الاستخدام</SectionHeading>
          <AiNotice />
          <p className="text-sm leading-relaxed text-ink-muted">
            الأداة بحثية وليست مصدراً للفتوى. لتعريف المصطلحات الواردة هنا راجع{" "}
            <Link href="/glossary" className="font-semibold text-brand-blue hover:underline">
              قاموس المصطلحات
            </Link>
            . وتعريفات الجداول مبسَّطة وينبغي أن يراجعها مختص قبل الاعتماد عليها.
          </p>
        </section>
      </main>
      <SiteFooter />
    </>
  );
}
