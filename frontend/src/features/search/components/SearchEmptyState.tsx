import { Search, Sparkles, BookOpen, Network, HelpCircle, RotateCcw } from "lucide-react";

interface SearchEmptyStateProps {
  type: "initial" | "no_results";
  query?: string;
  onSelectSuggestion?: (text: string) => void;
  onResetSearch?: () => void;
}

const SUGGESTIONS = [
  "إنما الأعمال بالنيات",
  "طلب العلم فريضة",
  "بني الإسلام على خمس",
  "المسلم من سلم المسلمون",
  "كلمتان خفيفتان على اللسان",
  "لا يؤمن أحدكم حتى يحب لأخيه",
  "الطهور شطر الإيمان",
  "الحياء شعبة من الإيمان",
];

export default function SearchEmptyState({
  type,
  query = "",
  onSelectSuggestion,
  onResetSearch,
}: SearchEmptyStateProps) {
  if (type === "initial") {
    return (
      <div className="space-y-8 pt-4">
        {/* Suggested Searches */}
        <div className="bg-surface/80 backdrop-blur-xs border border-line/80 rounded-2xl p-5 shadow-xs">
          <div className="flex items-center gap-2 mb-3.5 text-ink font-bold text-sm">
            <Sparkles className="w-4 h-4 text-brand-teal-ink" />
            <span>عمليات بحث شائعة مقترحة</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {SUGGESTIONS.map((sug) => (
              <button
                key={sug}
                type="button"
                onClick={() => onSelectSuggestion?.(sug)}
                className="px-3.5 py-1.5 text-xs md:text-sm bg-slate-100/80 hover:bg-brand-blue hover:text-white text-slate-700 rounded-full transition-all duration-200 cursor-pointer border border-line/60 font-arabic hover:shadow-xs"
              >
                {sug}
              </button>
            ))}
          </div>
        </div>

        {/* Feature Cards / Search Guide */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-surface p-5 rounded-2xl border border-line/80 shadow-xs space-y-2">
            <div className="w-9 h-9 rounded-xl bg-brand-blue/10 text-brand-blue flex items-center justify-center">
              <Search className="w-5 h-5" />
            </div>
            <h2 className="font-bold text-ink text-sm">البحث بالمتن والألفاظ</h2>
            <p className="text-ink-subtle text-xs leading-relaxed">
              ابحث بأي جملة أو عبارة من متن الحديث مع معالجة ذكية للتطبيع الإملائي والهمزات.
            </p>
          </div>

          <div className="bg-surface p-5 rounded-2xl border border-line/80 shadow-xs space-y-2">
            <div className="w-9 h-9 rounded-xl bg-emerald-500/10 text-emerald-600 flex items-center justify-center">
              <BookOpen className="w-5 h-5" />
            </div>
            <h2 className="font-bold text-ink text-sm">البحث في كتب السنة</h2>
            <p className="text-ink-subtle text-xs leading-relaxed">
              تصفح نتائج دقيقة تشمل صحيح البخاري، صحيح مسلم، السنن الأربعة، والمسانيد المعتمدة.
            </p>
          </div>

          <div className="bg-surface p-5 rounded-2xl border border-line/80 shadow-xs space-y-2">
            <div className="w-9 h-9 rounded-xl bg-purple-500/10 text-purple-600 flex items-center justify-center">
              <Network className="w-5 h-5" />
            </div>
            <h2 className="font-bold text-ink text-sm">شجرة الأسانيد والتخريج</h2>
            <p className="text-ink-subtle text-xs leading-relaxed">
              انقر على أي حديث لعرض شجرة سنده كاملة، أو فعل وضع التخريج للمقارنة بين عدة أسانيد.
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-surface rounded-2xl border border-line/80 p-8 md:p-12 text-center shadow-xs max-w-xl mx-auto space-y-5 my-6">
      <div className="w-16 h-16 bg-slate-100 rounded-2xl flex items-center justify-center mx-auto text-slate-400">
        <Search className="w-8 h-8" />
      </div>

      <div className="space-y-2">
        <h2 className="text-lg font-bold text-ink">
          لم يتم العثور على نتائج مطابقة
        </h2>
        <p className="text-sm text-ink-subtle max-w-md mx-auto leading-relaxed">
          لم نجد أي أحاديث أو رواة يطابقون عبارة البحث: <span className="font-semibold text-slate-700">«{query}»</span>
        </p>
      </div>

      <div className="bg-surface-muted rounded-xl p-4 text-xs text-ink-muted text-right space-y-2 border border-line/60 max-w-md mx-auto">
        <div className="flex items-center gap-1.5 font-bold text-slate-700">
          <HelpCircle className="w-4 h-4 text-brand-blue" />
          <span>نصائح لتحسين نتائج البحث:</span>
        </div>
        <ul className="list-disc list-inside space-y-1 text-ink-subtle pr-1">
          <li>تأكد من كتابة الكلمات بشكل صحيح دون أخطاء إملائية.</li>
          <li>جرب استخدام كلمات أقل أو عبارة أقصر من متن الحديث.</li>
          <li>يمكنك البحث باسم أحد رواة السند (مثل: نافع، مالك، الزهري).</li>
        </ul>
      </div>

      {onResetSearch && (
        <button
          type="button"
          onClick={onResetSearch}
          className="inline-flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-semibold rounded-xl transition-colors cursor-pointer"
        >
          <RotateCcw className="w-4 h-4" />
          <span>إعادة ضبط البحث</span>
        </button>
      )}
    </div>
  );
}
