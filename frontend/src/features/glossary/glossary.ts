import type { IllahType, IllahSeverity } from "@/types/api";

export interface GlossaryEntry {
  id: string;
  /** Arabic term as shown in the UI. */
  term: string;
  /** One-sentence plain-language definition. */
  definition: string;
  /** Transliteration used in English texts. */
  english: string;
  /** Short English gloss. */
  englishGloss: string;
}

/**
 * Simplified definitions for readers new to hadith sciences. They are meant to orient, not to settle
 * technical disagreements between scholars; they should be reviewed by a qualified specialist before release.
 */
export const GLOSSARY: GlossaryEntry[] = [
  { id: "isnad", term: "الإسناد", definition: "سلسلة الرواة التي أوصلت الحديث من مصنِّف الكتاب إلى النبي ﷺ أو الصحابي.", english: "Isnad", englishGloss: "chain of narrators" },
  { id: "matn", term: "المتن", definition: "نص الحديث نفسه، أي ألفاظه التي يُروى بها، دون سلسلة الرواة.", english: "Matn", englishGloss: "text of the hadith" },
  { id: "takhrij", term: "التخريج", definition: "عزو الحديث إلى كتب السنة التي رُوي فيها، وجمع طرقه للنظر في حاله.", english: "Takhrij", englishGloss: "tracing a hadith to its sources and gathering its routes" },
  { id: "madar", term: "مدار الإسناد", definition: "الراوي الذي تدور عليه طرق الحديث، فتتفرع منه الأسانيد.", english: "Madar", englishGloss: "the pivot narrator from whom the routes branch" },
  { id: "mutaba-shahid", term: "المتابعة والشاهد", definition: "المتابعة: رواية أخرى للحديث عن الشيخ نفسه أو من فوقه. الشاهد: حديث آخر بمعناه من رواية صحابي آخر.", english: "Mutaba‘ah / Shahid", englishGloss: "corroborating routes of the same hadith / of a similar hadith" },
  { id: "tabaqah", term: "الطبقة", definition: "الجيل الذي يجمع الرواة المتقاربين في زمنهم وفي الأخذ عن الشيوخ.", english: "Tabaqah", englishGloss: "generation or rank of narrators" },
  { id: "jarh-tadil", term: "الجرح والتعديل", definition: "العلم الذي يبحث في أحوال الرواة، أي عدالتهم وضبطهم، للحكم على قبول روايتهم.", english: "Jarh wa Ta‘dil", englishGloss: "evaluation of narrators' reliability" },
  { id: "tadlis", term: "التدليس", definition: "أن يخفي الراوي في الإسناد ما يوهم الاتصال، كأن يروي عمّن عاصره بصيغة توهم أنه سمعه منه ولم يسمعه.", english: "Tadlis", englishGloss: "concealing a gap or a source in the chain" },
  { id: "anana", term: "العنعنة", definition: "الرواية بلفظ «عن فلان» دون التصريح بالسماع أو التحديث.", english: "‘An‘anah", englishGloss: "narrating with “from so-and-so” without stating direct hearing" },
  { id: "ikhtilat", term: "الاختلاط", definition: "تغيّر ضبط الراوي وحفظه في آخر عمره لسبب كالكِبَر أو ضياع الكتب، فيُنظر هل سمع منه التلميذ قبل الاختلاط أو بعده.", english: "Ikhtilat", englishGloss: "deterioration of a narrator's memory later in life" },
  { id: "inqita", term: "الانقطاع", definition: "سقوط راوٍ أو أكثر من الإسناد، أو عدم ثبوت لقاء الراوي بشيخه.", english: "Inqita‘", englishGloss: "a break in the chain" },
  { id: "hidden-inqita", term: "الانقطاع الخفي", definition: "انقطاع لا يظهر من ظاهر السند، ويُعرف بالنظر في تواريخ الرواة وهل التقوا.", english: "Hidden Inqita‘", englishGloss: "a break that is not visible on the surface of the chain" },
  { id: "wasl-irsal", term: "الوصل والإرسال", definition: "الوصل: ذكر السند متصلاً إلى الصحابي. الإرسال: أن يرويه التابعي عن النبي ﷺ مباشرة دون ذكر الصحابي.", english: "Wasl / Irsal", englishGloss: "connected chain / a successor narrating directly from the Prophet ﷺ" },
  { id: "raf-waqf", term: "الرفع والوقف", definition: "الرفع: نسبة القول أو الفعل إلى النبي ﷺ. الوقف: نسبته إلى الصحابي.", english: "Raf‘ / Waqf", englishGloss: "attributed to the Prophet ﷺ / stopping at a Companion" },
  { id: "idtirab", term: "الاضطراب", definition: "اختلاف الرواة في الحديث على وجوه متساوية يتعذر معها الترجيح.", english: "Idtirab", englishGloss: "irreconcilable inconsistency between reports" },
  { id: "shudhudh", term: "الشذوذ", definition: "مخالفة الراوي المقبول لمن هو أولى منه بالقبول.", english: "Shudhudh", englishGloss: "an acceptable narrator contradicting a more reliable one" },
  { id: "nakarah", term: "النكارة", definition: "تفرد الراوي بما لا يُحتمل منه، أو مخالفة الضعيف لمن هو أقوى منه، على اصطلاح كثير من المحدثين.", english: "Nakarah", englishGloss: "an unacceptable singular or contradicting report" },
  { id: "ziyadat-thiqah", term: "زيادة الثقة", definition: "انفراد الراوي الثقة بزيادة في السند أو المتن لم يذكرها غيره؛ وتُقبل أو تُرد بحسب القرائن.", english: "Ziyadat al-Thiqah", englishGloss: "an addition reported only by a reliable narrator" },
  { id: "illah", term: "العلة", definition: "سبب خفي في الإسناد أو المتن قد يؤثر في صحة الحديث رغم أن ظاهره السلامة.", english: "‘Illah", englishGloss: "a hidden defect in a hadith" },
  { id: "qadihah", term: "علة قادحة", definition: "تصنيف في هذه الأداة لعلة يُرجَّح أنها تؤثر في صحة الحديث؛ وهي تنبيه آلي وليست حكماً نهائياً.", english: "Qadihah", englishGloss: "a defect likely to affect authenticity (tool classification)" },
  { id: "ghayr-qadihah", term: "غير قادحة", definition: "تصنيف في هذه الأداة لعلة يُرجَّح أنها لا تؤثر في صحة الحديث بمفردها.", english: "Ghayr Qadihah", englishGloss: "a defect unlikely to affect authenticity by itself (tool classification)" },
  { id: "tanbih", term: "تنبيه", definition: "إشارة تستدعي التحقق ولا تُعد علة مثبتة.", english: "Tanbih", englishGloss: "a flag that needs checking, not an established defect" },
  { id: "evidence-supported", term: "مؤيَّد بنص مصدر", definition: "المعلومة منسوبة إلى أقوال مسجَّلة لعلماء في مصادر ورد ذكرها في بيانات الأداة، ويمكنك قراءة النص الأصلي بنفسك.", english: "Supported by a source text", englishGloss: "attributed to recorded scholarly statements you can read" },
  { id: "evidence-verify", term: "يتطلب تحققاً", definition: "استنتاج آلي، أو خلاف بين الأقوال، أو قول واحد فقط؛ لا يكفي وحده، فراجع المصادر الأصلية أو أهل الاختصاص.", english: "Needs verification", englishGloss: "a machine inference, a disagreement, or a single statement" },
  { id: "evidence-insufficient", term: "لا يوجد مرجع كافٍ", definition: "لا تتوفر في بيانات الأداة أقوال أو حكم معتمد في هذه المسألة، فلا تُصدر الأداة حكماً وتحيلك إلى المصادر.", english: "Insufficient evidence", englishGloss: "no recorded statements or authoritative ruling in the tool's data" },
];

const BY_ID = new Map(GLOSSARY.map((e) => [e.id, e]));

export function getGlossaryEntry(id: string): GlossaryEntry | undefined {
  return BY_ID.get(id);
}

export const ILLAH_TYPE_GLOSSARY_ID: Record<IllahType, string> = {
  Tadlis: "tadlis",
  Ikhtilat: "ikhtilat",
  HiddenInqita: "hidden-inqita",
  Ziyadah: "ziyadat-thiqah",
  Shudhudh: "shudhudh",
  Nakarah: "nakarah",
  Idtirab: "idtirab",
  RafWaqf: "raf-waqf",
  WaslIrsal: "wasl-irsal",
};

export const SEVERITY_GLOSSARY_ID: Record<IllahSeverity, string> = {
  Qadihah: "qadihah",
  GhayrQadihah: "ghayr-qadihah",
  Tanbih: "tanbih",
};
