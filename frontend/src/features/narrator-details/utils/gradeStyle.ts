/**
 * One look per narrator grade (`gradeEn` from the API), shared by the tree nodes, the legend and the narrator drawer,
 * so a grade never shows in one colour on the graph and another in the drawer.
 */
export interface GradeStyle {
  /** Arabic label shown as text, so the grade is never conveyed by colour alone. */
  label: string;
  /** Node border colour; the node background is the same colour at low opacity. */
  color: string;
  /** Tailwind classes for a badge with readable contrast. */
  badgeClass: string;
  /** Dimmed by "إبراز الضعفاء". */
  isReliable: boolean;
}

export const GRADE_STYLE: Record<string, GradeStyle> = {
  companion: { label: "صحابي", color: "#9b59b6", badgeClass: "bg-purple-50 text-purple-800 border-purple-200", isReliable: true },
  reliable: { label: "ثقة", color: "#2ecc71", badgeClass: "bg-emerald-50 text-emerald-800 border-emerald-200", isReliable: true },
  mostly_reliable: { label: "صدوق", color: "#f39c12", badgeClass: "bg-amber-50 text-amber-900 border-amber-200", isReliable: true },
  weak: { label: "ضعيف", color: "#e74c3c", badgeClass: "bg-rose-50 text-rose-800 border-rose-200", isReliable: false },
  unknown: { label: "مجهول", color: "#95a5a6", badgeClass: "bg-slate-100 text-slate-700 border-slate-300", isReliable: false },
  abandoned: { label: "متروك", color: "#c0392b", badgeClass: "bg-red-100 text-red-800 border-red-300", isReliable: false },
  fabricator: { label: "كذاب", color: "#8b0000", badgeClass: "bg-red-900 text-white border-red-900", isReliable: false },
};

/**
 * A narrator with no grade at all (Ibn Hajar's Taqrib covers about a third of the registry). This is "no verdict",
 * not "weak" and not "majhul": it is shown neutrally and never counted as weak.
 */
export const UNRATED_STYLE: GradeStyle = {
  label: "غير مُقيَّم",
  color: "#94a3b8",
  badgeClass: "bg-surface text-ink-muted border-slate-300 border-dashed",
  isReliable: false,
};

export const UNRATED_HINT = "لا حكم على هذا الراوي في الكتب المعتمدة في الأداة؛ وليس ذلك حكماً بضعفه.";

/** True when there is no grade to show (missing, or a code the app does not know). */
export function isUnrated(gradeEn?: string | null): boolean {
  return getGradeStyle(gradeEn) === null;
}

const DEFAULT_NODE_BORDER = "#cbd5e1"; // slate-300

export function getGradeStyle(gradeEn?: string | null): GradeStyle | null {
  return gradeEn ? GRADE_STYLE[gradeEn.toLowerCase()] ?? null : null;
}

/** Arabic label for a grade or a critic's verdict code; unknown codes are shown as they are. */
export function getVerdictAr(en: string): string {
  return getGradeStyle(en)?.label ?? en;
}

/** Border and background colours of a narrator node. */
export function getNodeColors(gradeEn?: string | null): { borderColor: string; bgColor: string } {
  const style = getGradeStyle(gradeEn);
  return style
    ? { borderColor: style.color, bgColor: `${style.color}15` }
    : { borderColor: DEFAULT_NODE_BORDER, bgColor: "#ffffff" };
}
