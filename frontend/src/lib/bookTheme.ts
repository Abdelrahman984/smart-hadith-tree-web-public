export interface BookMeta {
  code: string;
  name: string;
  color: string;
  badgeClass: string;
  borderClass: string;
  bgLightClass: string;
}

export const KNOWN_BOOKS: Record<string, BookMeta> = {
  // ── الصحاح والمستخرجات والمستدركات ───────────────────────────────
  "صحيح البخاري": {
    code: "خ",
    name: "صحيح البخاري",
    color: "#2563eb",
    badgeClass: "bg-blue-50 text-blue-700 border-blue-200 hover:bg-blue-100",
    borderClass: "border-blue-500",
    bgLightClass: "bg-blue-50/40",
  },
  "صحيح مسلم": {
    code: "م",
    name: "صحيح مسلم",
    color: "#16a34a",
    badgeClass: "bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100",
    borderClass: "border-emerald-500",
    bgLightClass: "bg-emerald-50/40",
  },
  "صحيح ابن خزيمة": {
    code: "خز",
    name: "صحيح ابن خزيمة",
    color: "#0284c7",
    badgeClass: "bg-sky-50 text-sky-700 border-sky-200 hover:bg-sky-100",
    borderClass: "border-sky-500",
    bgLightClass: "bg-sky-50/40",
  },
  "صحيح ابن حبان": {
    code: "حب",
    name: "صحيح ابن حبان",
    color: "#059669",
    badgeClass: "bg-emerald-50 text-emerald-800 border-emerald-200 hover:bg-emerald-100",
    borderClass: "border-emerald-600",
    bgLightClass: "bg-emerald-50/40",
  },
  "مستخرج أبي عوانة": {
    code: "عو",
    name: "مستخرج أبي عوانة",
    color: "#0d9488",
    badgeClass: "bg-teal-50 text-teal-700 border-teal-200 hover:bg-teal-100",
    borderClass: "border-teal-500",
    bgLightClass: "bg-teal-50/40",
  },
  "المستدرك على الصحيحين": {
    code: "كم",
    name: "المستدرك على الصحيحين",
    color: "#4f46e5",
    badgeClass: "bg-indigo-50 text-indigo-700 border-indigo-200 hover:bg-indigo-100",
    borderClass: "border-indigo-500",
    bgLightClass: "bg-indigo-50/40",
  },

  // ── السنن والجوامع ──────────────────────────────────────────────
  "سنن أبي داود": {
    code: "د",
    name: "سنن أبي داود",
    color: "#d97706",
    badgeClass: "bg-amber-50 text-amber-700 border-amber-200 hover:bg-amber-100",
    borderClass: "border-amber-500",
    bgLightClass: "bg-amber-50/40",
  },
  "جامع الترمذي": {
    code: "ت",
    name: "جامع الترمذي",
    color: "#9333ea",
    badgeClass: "bg-purple-50 text-purple-700 border-purple-200 hover:bg-purple-100",
    borderClass: "border-purple-500",
    bgLightClass: "bg-purple-50/40",
  },
  "سنن النسائي": {
    code: "س",
    name: "سنن النسائي",
    color: "#0284c7",
    badgeClass: "bg-sky-50 text-sky-700 border-sky-200 hover:bg-sky-100",
    borderClass: "border-sky-500",
    bgLightClass: "bg-sky-50/40",
  },
  "السنن الكبرى للنسائي": {
    code: "كب",
    name: "السنن الكبرى للنسائي",
    color: "#0369a1",
    badgeClass: "bg-sky-50 text-sky-800 border-sky-200 hover:bg-sky-100",
    borderClass: "border-sky-600",
    bgLightClass: "bg-sky-50/40",
  },
  "سنن ابن ماجه": {
    code: "ق",
    name: "سنن ابن ماجه",
    color: "#e11d48",
    badgeClass: "bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100",
    borderClass: "border-rose-500",
    bgLightClass: "bg-rose-50/40",
  },
  "سنن الدارمي": {
    code: "دي",
    name: "سنن الدارمي",
    color: "#ca8a04",
    badgeClass: "bg-yellow-50 text-yellow-800 border-yellow-200 hover:bg-yellow-100",
    borderClass: "border-yellow-600",
    bgLightClass: "bg-yellow-50/40",
  },
  "سنن سعيد بن منصور": {
    code: "سع",
    name: "سنن سعيد بن منصور",
    color: "#b45309",
    badgeClass: "bg-amber-50 text-amber-800 border-amber-200 hover:bg-amber-100",
    borderClass: "border-amber-600",
    bgLightClass: "bg-amber-50/40",
  },
  "سنن الدارقطني": {
    code: "قط",
    name: "سنن الدارقطني",
    color: "#be123c",
    badgeClass: "bg-rose-50 text-rose-800 border-rose-200 hover:bg-rose-100",
    borderClass: "border-rose-600",
    bgLightClass: "bg-rose-50/40",
  },
  "السنن الكبرى للبيهقي": {
    code: "هق",
    name: "السنن الكبرى للبيهقي",
    color: "#7e22ce",
    badgeClass: "bg-purple-50 text-purple-800 border-purple-200 hover:bg-purple-100",
    borderClass: "border-purple-600",
    bgLightClass: "bg-purple-50/40",
  },

  // ── الموطآت والمصنفات ───────────────────────────────────────────
  "موطأ مالك": {
    code: "ط",
    name: "موطأ مالك",
    color: "#0d9488",
    badgeClass: "bg-teal-50 text-teal-700 border-teal-200 hover:bg-teal-100",
    borderClass: "border-teal-500",
    bgLightClass: "bg-teal-50/40",
  },
  "مصنف عبد الرزاق": {
    code: "عب",
    name: "مصنف عبد الرزاق",
    color: "#0f766e",
    badgeClass: "bg-teal-50 text-teal-800 border-teal-200 hover:bg-teal-100",
    borderClass: "border-teal-600",
    bgLightClass: "bg-teal-50/40",
  },
  "مصنف ابن أبي شيبة": {
    code: "ش",
    name: "مصنف ابن أبي شيبة",
    color: "#0891b2",
    badgeClass: "bg-cyan-50 text-cyan-800 border-cyan-200 hover:bg-cyan-100",
    borderClass: "border-cyan-600",
    bgLightClass: "bg-cyan-50/40",
  },

  // ── المسانيد ────────────────────────────────────────────────────
  "مسند أحمد": {
    code: "حم",
    name: "مسند أحمد",
    color: "#b45309",
    badgeClass: "bg-orange-50 text-orange-800 border-orange-200 hover:bg-orange-100",
    borderClass: "border-orange-500",
    bgLightClass: "bg-orange-50/40",
  },
  "مسند أبي داود الطيالسي": {
    code: "طي",
    name: "مسند أبي داود الطيالسي",
    color: "#c2410c",
    badgeClass: "bg-orange-50 text-orange-700 border-orange-200 hover:bg-orange-100",
    borderClass: "border-orange-600",
    bgLightClass: "bg-orange-50/40",
  },
  "مسند الشافعي": {
    code: "شاف",
    name: "مسند الشافعي",
    color: "#1d4ed8",
    badgeClass: "bg-blue-50 text-blue-800 border-blue-200 hover:bg-blue-100",
    borderClass: "border-blue-600",
    bgLightClass: "bg-blue-50/40",
  },
  "مسند الحميدي": {
    code: "حميد",
    name: "مسند الحميدي",
    color: "#a16207",
    badgeClass: "bg-amber-50 text-amber-800 border-amber-200 hover:bg-amber-100",
    borderClass: "border-amber-600",
    bgLightClass: "bg-amber-50/40",
  },
  "مسند إسحاق بن راهويه": {
    code: "راه",
    name: "مسند إسحاق بن راهويه",
    color: "#9a3412",
    badgeClass: "bg-orange-50 text-orange-900 border-orange-200 hover:bg-orange-100",
    borderClass: "border-orange-700",
    bgLightClass: "bg-orange-50/40",
  },
  "مسند البزار": {
    code: "بز",
    name: "مسند البزار",
    color: "#6d28d9",
    badgeClass: "bg-violet-50 text-violet-700 border-violet-200 hover:bg-violet-100",
    borderClass: "border-violet-500",
    bgLightClass: "bg-violet-50/40",
  },
  "مسند أبي يعلى الموصلي": {
    code: "يع",
    name: "مسند أبي يعلى الموصلي",
    color: "#4338ca",
    badgeClass: "bg-indigo-50 text-indigo-800 border-indigo-200 hover:bg-indigo-100",
    borderClass: "border-indigo-600",
    bgLightClass: "bg-indigo-50/40",
  },

  // ── المعاجم والأجزاء والآداب ────────────────────────────────────
  "المعجم الكبير للطبراني": {
    code: "طب",
    name: "المعجم الكبير للطبراني",
    color: "#0e7490",
    badgeClass: "bg-cyan-50 text-cyan-700 border-cyan-200 hover:bg-cyan-100",
    borderClass: "border-cyan-500",
    bgLightClass: "bg-cyan-50/40",
  },
  "المعجم الأوسط للطبراني": {
    code: "طس",
    name: "المعجم الأوسط للطبراني",
    color: "#155e75",
    badgeClass: "bg-cyan-50 text-cyan-800 border-cyan-200 hover:bg-cyan-100",
    borderClass: "border-cyan-600",
    bgLightClass: "bg-cyan-50/40",
  },
  "المعجم الصغير للطبراني": {
    code: "طص",
    name: "المعجم الصغير للطبراني",
    color: "#0369a1",
    badgeClass: "bg-sky-50 text-sky-700 border-sky-200 hover:bg-sky-100",
    borderClass: "border-sky-500",
    bgLightClass: "bg-sky-50/40",
  },
  "شعب الإيمان للبيهقي": {
    code: "شعب",
    name: "شعب الإيمان للبيهقي",
    color: "#7c3aed",
    badgeClass: "bg-violet-50 text-violet-800 border-violet-200 hover:bg-violet-100",
    borderClass: "border-violet-600",
    bgLightClass: "bg-violet-50/40",
  },
  "الأدب المفرد": {
    code: "خد",
    name: "الأدب المفرد",
    color: "#2563eb",
    badgeClass: "bg-blue-50 text-blue-700 border-blue-200 hover:bg-blue-100",
    borderClass: "border-blue-500",
    bgLightClass: "bg-blue-50/40",
  },
  "الشمائل المحمدية": {
    code: "شم",
    name: "الشمائل المحمدية",
    color: "#16a34a",
    badgeClass: "bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100",
    borderClass: "border-emerald-500",
    bgLightClass: "bg-emerald-50/40",
  },
};

export const CANONICAL_31_BOOKS: BookMeta[] = Object.values(KNOWN_BOOKS);

const EXTRA_BOOKS: Record<string, BookMeta> = {
  "مشكاة المصابيح": {
    code: "مشكاة",
    name: "مشكاة المصابيح",
    color: "#475569",
    badgeClass: "bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100",
    borderClass: "border-slate-500",
    bgLightClass: "bg-slate-50/40",
  },
  "بلوغ المرام": {
    code: "بلوغ",
    name: "بلوغ المرام",
    color: "#475569",
    badgeClass: "bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100",
    borderClass: "border-slate-500",
    bgLightClass: "bg-slate-50/40",
  },
  "رياض الصالحين": {
    code: "رياض",
    name: "رياض الصالحين",
    color: "#475569",
    badgeClass: "bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100",
    borderClass: "border-slate-500",
    bgLightClass: "bg-slate-50/40",
  },
};

const DEFAULT_BOOK_META: BookMeta = {
  code: "ك",
  name: "كتاب حديث",
  color: "#64748b",
  badgeClass: "bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100",
  borderClass: "border-slate-400",
  bgLightClass: "bg-slate-50/40",
};

export function getBookMeta(bookName?: string | null): BookMeta {
  if (!bookName) return DEFAULT_BOOK_META;
  const trimmed = bookName.trim();

  if (KNOWN_BOOKS[trimmed]) return KNOWN_BOOKS[trimmed];
  if (EXTRA_BOOKS[trimmed]) return EXTRA_BOOKS[trimmed];

  // Handle combined book names in comparative reference nodes (e.g. "صحيح البخاري / الأدب المفرد")
  if (trimmed.includes("/")) {
    const firstPart = trimmed.split("/")[0]?.trim();
    if (firstPart && KNOWN_BOOKS[firstPart]) {
      return { ...KNOWN_BOOKS[firstPart], name: trimmed };
    }
  }

  for (const [key, meta] of Object.entries(KNOWN_BOOKS)) {
    if (trimmed.includes(key)) {
      return { ...meta, name: trimmed };
    }
  }

  return {
    ...DEFAULT_BOOK_META,
    name: trimmed,
  };
}

