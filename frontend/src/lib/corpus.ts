/**
 * The 31 books of the corpus, grouped as on the home page and the books index.
 * Hadith counts are the editions' own numbering, shown as display text.
 */
export interface CorpusBookItem {
  name: string;
  author: string;
  hadithsCount: string;
}

export interface CorpusCategory {
  title: string;
  subtitle: string;
  books: CorpusBookItem[];
}

export const CORPUS_CATEGORIES: CorpusCategory[] = [
  {
    title: "الصحاح والمستخرجات والمستدركات",
    subtitle: "أعلى دواوين السنة شرطاً وتوثيقاً واستدراكاً",
    books: [
      { name: "صحيح البخاري", author: "الإمام البخاري (256 هـ)", hadithsCount: "7,563" },
      { name: "صحيح مسلم", author: "الإمام مسلم (261 هـ)", hadithsCount: "7,500+" },
      { name: "صحيح ابن خزيمة", author: "ابن خزيمة (311 هـ)", hadithsCount: "2,851" },
      { name: "مستخرج أبي عوانة", author: "أبو عوانة الإسفراييني (316 هـ)", hadithsCount: "13,036" },
      { name: "صحيح ابن حبان", author: "ابن حبان البستي (354 هـ)", hadithsCount: "7,447" },
      { name: "المستدرك على الصحيحين", author: "الحاكم النيسابوري (405 هـ)", hadithsCount: "5,799" },
    ],
  },
  {
    title: "السنن والجوامع",
    subtitle: "موسوعات أحاديث الأحكام والسنن الصغرى والكبرى",
    books: [
      { name: "سنن سعيد بن منصور", author: "سعيد بن منصور (227 هـ)", hadithsCount: "2,712" },
      { name: "سنن الدارمي", author: "الإمام الدارمي (255 هـ)", hadithsCount: "3,500+" },
      { name: "سنن أبي داود", author: "أبو داود السجستاني (275 هـ)", hadithsCount: "5,274" },
      { name: "سنن ابن ماجه", author: "ابن ماجه القزويني (273 هـ)", hadithsCount: "4,341" },
      { name: "جامع الترمذي", author: "أبو عيسى الترمذي (279 هـ)", hadithsCount: "3,956" },
      { name: "سنن النسائي", author: "الإمام النسائي (303 هـ)", hadithsCount: "5,758" },
      { name: "السنن الكبرى للنسائي", author: "الإمام النسائي (303 هـ)", hadithsCount: "11,444" },
      { name: "سنن الدارقطني", author: "الإمام الدارقطني (385 هـ)", hadithsCount: "4,231" },
      { name: "السنن الكبرى للبيهقي", author: "الإمام البيهقي (458 هـ)", hadithsCount: "11,655" },
    ],
  },
  {
    title: "الموطآت والمصنفات المبكرة",
    subtitle: "أقدم الجوامع الحديثية والفقهية المسندة لعصر التابعين وأتباعهم",
    books: [
      { name: "موطأ مالك", author: "الإمام مالك بن أنس (179 هـ)", hadithsCount: "1,800+" },
      { name: "مصنف عبد الرزاق", author: "عبد الرزاق الصنعاني (211 هـ)", hadithsCount: "18,422" },
      { name: "مصنف ابن أبي شيبة", author: "ابن أبي شيبة (235 هـ)", hadithsCount: "37,000+" },
    ],
  },
  {
    title: "المسانيد",
    subtitle: "الدواوين المرتبة على مسانيد الصحابة لتتبع الطرق والمتابعات",
    books: [
      { name: "مسند أبي داود الطيالسي", author: "أبو داود الطيالسي (204 هـ)", hadithsCount: "2,891" },
      { name: "مسند الشافعي", author: "الإمام الشافعي (204 هـ)", hadithsCount: "1,678" },
      { name: "مسند الحميدي", author: "الإمام الحميدي (219 هـ)", hadithsCount: "1,214" },
      { name: "مسند إسحاق بن راهويه", author: "إسحاق بن راهويه (238 هـ)", hadithsCount: "2,083" },
      { name: "مسند أحمد", author: "الإمام أحمد بن حنبل (241 هـ)", hadithsCount: "27,647" },
      { name: "مسند البزار", author: "أبو بكر البزار (292 هـ)", hadithsCount: "4,470" },
      { name: "مسند أبي يعلى الموصلي", author: "أبو يعلى الموصلي (307 هـ)", hadithsCount: "7,333" },
    ],
  },
  {
    title: "المعاجم والأجزاء والآداب",
    subtitle: "معاجم الشيوخ ومصنفات الأخلاق والشمائل النبوية",
    books: [
      { name: "الأدب المفرد", author: "الإمام البخاري (256 هـ)", hadithsCount: "1,322" },
      { name: "الشمائل المحمدية", author: "أبو عيسى الترمذي (279 هـ)", hadithsCount: "415" },
      { name: "المعجم الكبير للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "14,549" },
      { name: "المعجم الأوسط للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "9,444" },
      { name: "المعجم الصغير للطبراني", author: "الإمام الطبراني (360 هـ)", hadithsCount: "1,187" },
      { name: "شعب الإيمان للبيهقي", author: "الإمام البيهقي (458 هـ)", hadithsCount: "6,215" },
    ],
  },
];

/** All corpus books, in category order. */
export const CORPUS_BOOKS: CorpusBookItem[] = CORPUS_CATEGORIES.flatMap((c) => c.books);
