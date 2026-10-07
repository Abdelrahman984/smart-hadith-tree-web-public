/**
 * Mapping of canonical Hadith books and compilers to their universally recognized famous names.
 */
const CANONICAL_COMPILERS: Array<{
  matchKey: string;
  famousName: string;
  shortName: string;
}> = [
  // Specific multi-word or book-title matches first
  { matchKey: "الطيالسي", famousName: "أبو داود الطيالسي", shortName: "الطيالسي" },
  { matchKey: "سليمان بن داود بن الجارود", famousName: "أبو داود الطيالسي", shortName: "الطيالسي" },
  { matchKey: "ابن أبي شيبة", famousName: "ابن أبي شيبة", shortName: "ابن أبي شيبة" },
  { matchKey: "عبد الله بن محمد بن إبراهيم بن عثمان", famousName: "ابن أبي شيبة", shortName: "ابن أبي شيبة" },
  { matchKey: "عبد الرزاق", famousName: "عبد الرزاق الصنعاني", shortName: "عبد الرزاق" },
  { matchKey: "سعيد بن منصور", famousName: "سعيد بن منصور", shortName: "سعيد بن منصور" },
  { matchKey: "إسحاق بن راهويه", famousName: "إسحاق بن راهويه", shortName: "ابن راهويه" },
  { matchKey: "إسحاق بن إبراهيم بن مخلد", famousName: "إسحاق بن راهويه", shortName: "ابن راهويه" },
  { matchKey: "أبي يعلى", famousName: "أبو يعلى الموصلي", shortName: "أبو يعلى" },
  { matchKey: "أبو يعلى", famousName: "أبو يعلى الموصلي", shortName: "أبو يعلى" },
  { matchKey: "أحمد بن علي بن المثنى", famousName: "أبو يعلى الموصلي", shortName: "أبو يعلى" },
  { matchKey: "ابن خزيمة", famousName: "ابن خزيمة", shortName: "ابن خزيمة" },
  { matchKey: "أبي عوانة", famousName: "أبو عوانة الإسفراييني", shortName: "أبو عوانة" },
  { matchKey: "أبو عوانة", famousName: "أبو عوانة الإسفراييني", shortName: "أبو عوانة" },
  { matchKey: "يعقوب بن إسحاق بن إبراهيم", famousName: "أبو عوانة الإسفراييني", shortName: "أبو عوانة" },
  { matchKey: "ابن حبان", famousName: "ابن حبان", shortName: "ابن حبان" },
  { matchKey: "الطبراني", famousName: "الطبراني", shortName: "الطبراني" },
  { matchKey: "سليمان بن أحمد بن أيوب", famousName: "الطبراني", shortName: "الطبراني" },
  { matchKey: "الدارقطني", famousName: "الدارقطني", shortName: "الدارقطني" },
  { matchKey: "علي بن عمر بن أحمد بن مهدي", famousName: "الدارقطني", shortName: "الدارقطني" },
  { matchKey: "المستدرك", famousName: "الحاكم النيسابوري", shortName: "الحاكم" },
  { matchKey: "الحاكم", famousName: "الحاكم النيسابوري", shortName: "الحاكم" },
  { matchKey: "محمد بن عبد الله بن محمد بن حمدويه", famousName: "الحاكم النيسابوري", shortName: "الحاكم" },
  { matchKey: "البيهقي", famousName: "البيهقي", shortName: "البيهقي" },
  { matchKey: "أحمد بن الحسين بن علي بن موسى", famousName: "البيهقي", shortName: "البيهقي" },
  { matchKey: "البزار", famousName: "البزار", shortName: "البزار" },
  { matchKey: "أحمد بن عمرو بن عبد الخالق", famousName: "البزار", shortName: "البزار" },
  { matchKey: "الشافعي", famousName: "الإمام الشافعي", shortName: "الشافعي" },
  { matchKey: "محمد بن إدريس بن العباس", famousName: "الإمام الشافعي", shortName: "الشافعي" },
  { matchKey: "الحميدي", famousName: "الحميدي", shortName: "الحميدي" },
  { matchKey: "عبد الله بن الزبير بن عيسى", famousName: "الحميدي", shortName: "الحميدي" },
  { matchKey: "الأدب المفرد", famousName: "البخاري", shortName: "البخاري" },
  { matchKey: "الشمائل المحمدية", famousName: "الترمذي", shortName: "الترمذي" },
  { matchKey: "البخاري", famousName: "البخاري", shortName: "البخاري" },
  { matchKey: "محمد بن إسماعيل بن إبراهيم", famousName: "البخاري", shortName: "البخاري" },
  { matchKey: "مسلم", famousName: "مسلم", shortName: "مسلم" },
  { matchKey: "مسلم بن الحجاج", famousName: "مسلم", shortName: "مسلم" },
  { matchKey: "أبي داود", famousName: "أبو داود", shortName: "أبو داود" },
  { matchKey: "ابو داود", famousName: "أبو داود", shortName: "أبو داود" },
  { matchKey: "سليمان بن الأشعث", famousName: "أبو داود", shortName: "أبو داود" },
  { matchKey: "الترمذي", famousName: "الترمذي", shortName: "الترمذي" },
  { matchKey: "محمد بن عيسى بن سورة", famousName: "الترمذي", shortName: "الترمذي" },
  { matchKey: "النسائي", famousName: "النسائي", shortName: "النسائي" },
  { matchKey: "أحمد بن شعيب", famousName: "النسائي", shortName: "النسائي" },
  { matchKey: "ابن ماجه", famousName: "ابن ماجه", shortName: "ابن ماجه" },
  { matchKey: "ابن ماجة", famousName: "ابن ماجه", shortName: "ابن ماجه" },
  { matchKey: "محمد بن يزيد بن ماجه", famousName: "ابن ماجه", shortName: "ابن ماجه" },
  { matchKey: "أحمد", famousName: "أحمد بن حنبل", shortName: "أحمد" },
  { matchKey: "احمد", famousName: "أحمد بن حنبل", shortName: "أحمد" },
  { matchKey: "مالك", famousName: "مالك بن أنس", shortName: "مالك" },
  { matchKey: "الدارمي", famousName: "الدارمي", shortName: "الدارمي" },
  { matchKey: "عبد الله بن عبد الرحمن بن الفضل", famousName: "الدارمي", shortName: "الدارمي" },
];

/** "للطبراني" is "ل" + "الطبراني" with the alef dropped; restore it so the book title can match a key. */
function expandLamLam(bookName: string): string {
  return bookName.replace(/(^|\s)لل/g, "$1ل ال");
}

/**
 * The compiler a book is known by (e.g. 'البخاري' for «صحيح البخاري»), taken from the book title alone: the first
 * narrator of a chain is a narrator, not the compiler. A title with no known compiler is shown as it is.
 */
export function getFamousReferenceOwnerName(bookName: string): string {
  const book = expandLamLam(bookName);
  return CANONICAL_COMPILERS.find((item) => book.includes(item.matchKey))?.famousName ?? bookName;
}
