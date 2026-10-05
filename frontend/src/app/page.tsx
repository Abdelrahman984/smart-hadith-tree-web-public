import Link from "next/link";
import {
  BookOpen,
  Network,
  ShieldCheck,
  Search,
  GitCompareArrows,
  ShieldAlert,
  Users,
  Sparkles,
  ArrowLeft,
  CheckCircle2,
} from "lucide-react";
import CorpusCatalog from "@/features/books/CorpusCatalog";
import SiteHeader from "@/components/SiteHeader";
import SiteFooter from "@/components/SiteFooter";

const QUICK_SEARCH_EXAMPLES = [
  "إنما الأعمال بالنيات",
  "الزهري عن سالم عن أبيه",
  "لا ضرر ولا ضرار",
  "سفيان الثوري",
  "معمر عن الزهري",
];

export default function Home() {
  return (
    <div className="min-h-screen bg-surface-muted flex flex-col">
      <SiteHeader />

      <main id="main-content">
      {/* Hero Section */}
      <section className="relative overflow-hidden pt-14 pb-16 px-6 bg-gradient-to-b from-white via-slate-50 to-slate-100/70 border-b border-line/70">
        <div className="max-w-5xl mx-auto text-center space-y-7">
          <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-brand-blue/5 border border-brand-blue/15 text-brand-blue text-sm font-semibold">
            <Sparkles className="w-4 h-4 text-brand-teal-ink" />
            <span>الجيل الجديد من رقمنة علوم الحديث والتخريج ودراسة الأسانيد والعلل</span>
          </div>

          <h1 className="text-4xl sm:text-6xl md:text-7xl font-bold text-brand-dark tracking-tight leading-tight">
            شجرة الأسانيد <span className="text-brand-teal-ink">الذكية</span>
          </h1>

          <p className="text-lg sm:text-xl text-ink-muted max-w-3xl mx-auto leading-relaxed">
            منصة علمية متكاملة تجمع <strong className="text-ink">31 ديواناً من أصول السنة المسندة</strong> (الصحاح، السنن، المصنفات، المسانيد، المعاجم، والمستدركات) لرسم أشجار الأسانيد تفاعلياً، والتخريج المقارن، ورصد علل الانقطاع والتدليس والاختلاط، وتلخيص الجرح والتعديل بالذكاء الاصطناعي.
          </p>

          {/* Direct Search Bar on Home */}
          <form
            action="/search"
            method="GET"
            className="max-w-2xl mx-auto pt-2"
          >
            <div className="relative flex items-center bg-surface rounded-2xl shadow-lg border border-line/90 focus-within:border-brand-teal focus-within:ring-4 focus-within:ring-brand-teal/15 transition-all p-2">
              <div className="pr-3 pl-2 text-slate-400">
                <Search className="w-6 h-6" />
              </div>
              <input
                type="text"
                name="q"
                aria-label="ابحث في الأحاديث والأسانيد"
                placeholder="ابحث بطرف الحديث، أو اسم الراوي، أو سلسلة الإسناد (مثال: الزهري عن سالم)..."
                className="w-full py-3 px-2 text-base sm:text-lg text-ink placeholder:text-slate-400 focus:outline-none bg-transparent"
              />
              <button
                type="submit"
                className="shrink-0 px-6 py-3 bg-brand-blue hover:bg-brand-dark text-white font-bold rounded-xl transition-colors flex items-center gap-2 cursor-pointer"
              >
                <span>بحث</span>
                <ArrowLeft className="w-4 h-4" />
              </button>
            </div>
          </form>

          {/* Quick Search Chips */}
          <div className="flex items-center justify-center flex-wrap gap-2 pt-1 text-sm">
            <span className="text-ink-subtle font-medium">نماذج بحث سريعة:</span>
            {QUICK_SEARCH_EXAMPLES.map((example) => (
              <Link
                key={example}
                href={`/search?q=${encodeURIComponent(example)}`}
                className="px-3 py-1 rounded-full bg-surface border border-line text-slate-700 hover:border-brand-teal hover:text-brand-blue hover:bg-teal-50/30 transition-all text-xs font-semibold shadow-2xs"
              >
                {example}
              </Link>
            ))}
          </div>

        </div>

        {/* Live Database Stats Bar */}
        <div className="max-w-5xl mx-auto mt-12 grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-surface/90 backdrop-blur-xs p-5 rounded-2xl border border-line/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-brand-blue font-latin">31</div>
            <div className="text-sm font-bold text-ink mt-1">ديواناً حديثياً مسنداً</div>
            <div className="text-xs text-ink-subtle mt-0.5">الصحاح والسنن والمصنفات والمسانيد والمعاجم</div>
          </div>

          <div className="bg-surface/90 backdrop-blur-xs p-5 rounded-2xl border border-line/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-brand-teal-ink font-latin">274,597</div>
            <div className="text-sm font-bold text-ink mt-1">حديث وأثر مسند</div>
            <div className="text-xs text-ink-subtle mt-0.5">مفهرسة ومطبّعة للبحث الفوري</div>
          </div>

          <div className="bg-surface/90 backdrop-blur-xs p-5 rounded-2xl border border-line/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-emerald-600 font-latin">1,217,978</div>
            <div className="text-sm font-bold text-ink mt-1">حلقة إسناد متصلة</div>
            <div className="text-xs text-ink-subtle mt-0.5">روابط شيخ–تلميذ مستخرجة آلياً من الأسانيد</div>
          </div>

          <div className="bg-surface/90 backdrop-blur-xs p-5 rounded-2xl border border-line/80 shadow-xs text-center">
            <div className="text-3xl sm:text-4xl font-bold text-purple-600 font-latin">23,502</div>
            <div className="text-sm font-bold text-ink mt-1">ترجمة راوٍ في القاعدة</div>
            <div className="text-xs text-ink-subtle mt-0.5">مع أقوال أئمة الجرح والتعديل والطبقة</div>
          </div>
        </div>
      </section>

      {/* Core Capabilities Section */}
      <section className="py-16 px-6 max-w-7xl mx-auto w-full">
        <div className="text-center max-w-3xl mx-auto mb-12 space-y-3">
          <h2 className="text-2xl sm:text-3xl font-bold text-brand-dark">
            إمكانات هندسية وعلمية متقدمة لدراسة الأسانيد
          </h2>
          <p className="text-ink-muted text-base">
            تجمع المنصة بين قواعد صناعة الحديث التراثية وأحدث خوارزميات الرسوم البيانية (Graph Layout) والذكاء الاصطناعي التوليدي.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-brand-teal/10 rounded-xl flex items-center justify-center mb-4 text-brand-teal-ink">
              <Network className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">تصور شبكي تفاعلي للأسانيد</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              رسم شجري تفاعلي (ELK Graph) يوضح طبقات الرواة من الصحابي إلى المصنف، ويكشف مدارات الحديث والتفردات والمتابعات بوضوح بصري فائق.
            </p>
          </div>

          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-rose-500/10 rounded-xl flex items-center justify-center mb-4 text-rose-600">
              <ShieldAlert className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">محرك كشف العلل والانقطاع</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              فحص آلي لاتصال السند، ورصد الانقطاع، والتنبيه على عنعنة المدلسين (وفق مراتب ابن حجر الخمس)، وتمييز الرواة المختلطين (من سمع منهم قبل الاختلاط أو بعده).
            </p>
          </div>

          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-brand-blue/10 rounded-xl flex items-center justify-center mb-4 text-brand-blue">
              <GitCompareArrows className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">التخريج المقارن عبر الدواوين</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              دمج عدة روايات للحديث من مختلف الصحاح والسنن والمسانيد والمعاجم في شجرة مقارنة واحدة لتحديد المدار المشترك ومواطن الزيادة والشذوذ.
            </p>
          </div>

          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-amber-500/10 rounded-xl flex items-center justify-center mb-4 text-amber-600">
              <Users className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">فك الاشتباك السياقي للرواة</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              خوارزمية ذكية لتمييز الرواة المتشابهين أو المهملين في السند (مثل سفيان، معمر، ابن جريج، يحيى بن سعيد) بالاعتماد على شبكة الشيوخ والتلاميذ والطبقة.
            </p>
          </div>

          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-purple-500/10 rounded-xl flex items-center justify-center mb-4 text-purple-600">
              <ShieldCheck className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">تلخيص الجرح والتعديل بالذكاء الاصطناعي</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              تحليل وتلخيص آلي لأقوال أئمة النقد (كالذهبي وابن حجر والمزي وابن معين) عبر تقنية RAG لتقديم خلاصة موثقة لحال الراوي ورتبته.
            </p>
          </div>

          <div className="bg-surface p-6 rounded-2xl shadow-xs border border-line/80 hover:shadow-md transition-shadow">
            <div className="w-12 h-12 bg-emerald-500/10 rounded-xl flex items-center justify-center mb-4 text-emerald-600">
              <BookOpen className="w-6 h-6" />
            </div>
            <h3 className="text-lg font-bold text-ink mb-2">بحث متقدم في المتون والأسانيد</h3>
            <p className="text-ink-muted text-sm leading-relaxed">
              بحث فوري يتجاوز التشكيل والهمزات مع إمكانية التصفية، والبحث بشرط وجود راوٍ معين في السند، واستكشاف المتابعات والشواهد آلياً.
            </p>
          </div>
        </div>
      </section>

      {/* 31 Canonical Books Showcase */}
      <section className="py-16 px-6 bg-surface border-t border-line/80">
        <div className="max-w-7xl mx-auto">
          <div className="flex flex-col md:flex-row md:items-end justify-between mb-10 gap-4">
            <div>
              <div className="inline-flex items-center gap-2 text-brand-teal-ink font-bold text-sm mb-2">
                <CheckCircle2 className="w-4 h-4" />
                <span>المكتبة الحديثية الكاملة المدمجة</span>
              </div>
              <h2 className="text-2xl sm:text-3xl font-bold text-brand-dark">
                خزانة دواوين السنة المسندة (31 مصدراً أصلياً)
              </h2>
              <p className="text-ink-muted text-sm sm:text-base mt-1">
                تغطي الدواوين التسعة، والمصنفات المبكرة، والمسانيد، والصحاح والمستخرجات، والمعاجم الثلاثة، والسنن الكبرى والمستدركات.
              </p>
            </div>
            <Link
              href="/books"
              className="self-start md:self-auto px-5 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-ink font-bold text-sm transition-colors flex items-center gap-2"
            >
              <span>فتح فهرس الأبواب الكامل</span>
              <ArrowLeft className="w-4 h-4" />
            </Link>
          </div>

          <CorpusCatalog />
        </div>
      </section>

      </main>

      <SiteFooter />
    </div>
  );
}
