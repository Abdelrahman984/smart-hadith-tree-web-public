import Link from "next/link";
import Logo from "./Logo";

export default function SiteFooter() {
  return (
    <footer className="mt-auto py-8 px-6 bg-brand-dark text-slate-300 text-sm border-t border-slate-800">
      <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center gap-2.5">
          <Logo variant="lockup" size={32} onDark />
          <span className="font-latin text-xs text-slate-400" dir="ltr">Smart Hadith Tree</span>
        </div>
        <div className="text-xs text-slate-400 text-center sm:text-left">
          موسوعة رقمية لدراسة الأسانيد والتخريج وكشف العلل
        </div>
      </div>
      <nav aria-label="روابط التذييل" className="max-w-7xl mx-auto mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 text-xs font-semibold text-slate-300">
        <Link href="/sources" className="hover:text-white hover:underline">المصادر والمنهج</Link>
        <Link href="/sources#jarh-tadil" className="hover:text-white hover:underline">مصادر الجرح والتعديل</Link>
        <Link href="/sources#ilal" className="hover:text-white hover:underline">مصادر العلل</Link>
        <Link href="/glossary" className="hover:text-white hover:underline">قاموس المصطلحات</Link>
      </nav>
      <p className="max-w-7xl mx-auto mt-4 text-[11px] text-slate-400 text-center">
        أداة بحثية مدعومة بالذكاء الاصطناعي، وليست مصدراً للفتوى أو الحكم النهائي على الأحاديث؛ يُرجع في المسائل الدقيقة إلى أهل الاختصاص.
      </p>
    </footer>
  );
}
