import { formatTwoPartNarratorName } from "./formatNarratorName";

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

/**
 * Resolves the famous reference compiler name (e.g. 'البخاري' instead of 'محمد بن إسماعيل').
 */
export function getFamousReferenceOwnerName(
  narratorName?: string | null,
  knownAs?: string | null,
  bookName?: string | null
): string {
  // 1. Check against canonical mappings using bookName first
  if (bookName) {
    for (const item of CANONICAL_COMPILERS) {
      if (bookName.includes(item.matchKey)) {
        return item.famousName;
      }
    }
  }

  // 2. Check knownAs
  if (knownAs) {
    for (const item of CANONICAL_COMPILERS) {
      if (knownAs.includes(item.matchKey)) {
        return item.famousName;
      }
    }
    // Clean knownAs if it contains titles like "الإمام البخاري"
    const cleanedKnown = knownAs.replace(/^(?:الإمام|الشيخ|الحافظ|العلامة)\s+/, "").trim();
    if (cleanedKnown && !cleanedKnown.includes("بن")) {
      return cleanedKnown;
    }
  }

  // 3. Check narratorName / fullName
  if (narratorName) {
    for (const item of CANONICAL_COMPILERS) {
      if (narratorName.includes(item.matchKey)) {
        return item.famousName;
      }
    }
  }

  // 4. Fallback: if knownAs exists, use it; otherwise use formatTwoPartNarratorName
  if (knownAs && knownAs.trim()) {
    return knownAs.trim();
  }

  return formatTwoPartNarratorName(narratorName);
}
