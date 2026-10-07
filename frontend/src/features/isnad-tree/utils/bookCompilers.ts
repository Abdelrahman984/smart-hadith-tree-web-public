/**
 * The compiler of each book, as the start of his name in the registry (the resolver's `compiler` of the book's chains).
 * A chain whose first narrator is the compiler himself must not be drawn twice, once as him and once as his book's source
 * card; a first narrator who is only the compiler's sheikh (the compiler's own name is often unresolved) stays a narrator.
 */
const COMPILER_NAME_START: Record<string, string> = {
  "سنن أبي داود": "سليمان بن الأشعث بن شداد",
  "مسند أحمد": "أحمد بن محمد بن حنبل",
  "الأدب المفرد": "محمد بن إسماعيل بن إبراهيم",
  "صحيح البخاري": "محمد بن إسماعيل بن إبراهيم",
  "سنن الدارمي": "عبد الله بن عبد الرحمن",
  "سنن ابن ماجه": "محمد بن يزيد الربعي",
  "موطأ مالك": "مالك بن أنس بن مالك",
  "المعجم الأوسط للطبراني": "سليمان بن أحمد بن أيوب",
  "المعجم الكبير للطبراني": "سليمان بن أحمد بن أيوب",
  "المعجم الصغير للطبراني": "سليمان بن أحمد بن أيوب",
  "مصنف عبد الرزاق": "عبد الرزاق بن همام بن",
  "مصنف ابن أبي شيبة": "عبد الله بن محمد بن",
  "صحيح مسلم": "مسلم بن الحجاج بن مسلم",
  "مسند أبي يعلى الموصلي": "أحمد بن علي بن المثنى",
  "مسند البزار": "أحمد بن عمرو بن عبد",
  "مسند الحميدي": "عبد الله بن الزبير بن",
  "مسند إسحاق بن راهويه": "إسحاق بن إبراهيم بن مخلد",
  "مسند الشافعي": "محمد بن إدريس بن العباس",
  "مسند أبي داود الطيالسي": "سليمان بن داود بن الجارود",
  "المستدرك على الصحيحين": "محمد بن عبد الله بن",
  "مستخرج أبي عوانة": "يعقوب بن إسحاق بن إبراهيم",
  "سنن النسائي": "أحمد بن شعيب بن علي",
  "صحيح ابن حبان": "محمد بن حبان بن أحمد",
  "صحيح ابن خزيمة": "محمد بن إسحاق بن خزيمة",
  "الشمائل المحمدية": "محمد بن عيسى بن سورة",
  "شعب الإيمان للبيهقي": "أحمد بن الحسين بن علي",
  "سنن الدارقطني": "علي بن عمر بن أحمد",
  "السنن الكبرى للبيهقي": "أحمد بن الحسين بن علي",
  "السنن الكبرى للنسائي": "أحمد بن شعيب بن علي",
  "سنن سعيد بن منصور": "سعيد بن منصور بن شعبة",
  "جامع الترمذي": "محمد بن عيسى بن سورة",
};

/** Is this narrator the compiler of the book? Compared by the start of the registry name, which is unique per compiler. */
export function isCompilerOf(narratorName: string | null | undefined, bookName: string): boolean {
  const start = COMPILER_NAME_START[bookName];
  return Boolean(start && narratorName && narratorName.trim().startsWith(start));
}
