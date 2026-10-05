import type { ComparativeHadithSourceDto } from "@/types/api";

/**
 * Browser title of a comparison: the first narration and how many are compared, e.g.
 * «تخريج صحيح البخاري 1 و2 رواية أخرى». The book and number are used rather than the text, because a stored
 * `matnArabic` can still begin with the isnad.
 */
export function takhreejTitle(sources: ComparativeHadithSourceDto[]): string {
  const first = sources[0];
  if (!first) return "شجرة التخريج المقارنة";
  const others = sources.length - 1;
  const rest = others === 0 ? "" : others === 1 ? " ورواية أخرى" : ` و${others} روايات أخرى`;
  return `تخريج ${first.bookName} ${first.hadithNumber}${rest}`;
}
