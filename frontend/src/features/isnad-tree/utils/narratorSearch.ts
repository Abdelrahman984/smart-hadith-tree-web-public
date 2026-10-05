/** A narrator on the graph, as the find box lists it. */
export interface NarratorSearchItem {
  id: string;
  /** Full name, as the card's tooltip shows it. */
  name: string;
  /** The shorter name drawn on the card, when different. */
  shortName?: string;
  tier?: string | null;
  /** Arabic label of the grade, if any. */
  grade?: string | null;
}

/**
 * Arabic text reduced for matching: no diacritics or tatweel, one form of alef, yaa and taa marbuta, single spaces.
 * «أَبُو هُرَيْرَة» and «ابو هريره» then compare equal.
 */
export function normalizeArabic(text: string): string {
  return text
    .replace(/[ً-ٰٟـ]/g, "")
    .replace(/[أإآٱ]/g, "ا")
    .replace(/ى/g, "ي")
    .replace(/ة/g, "ه")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();
}

/**
 * Narrators whose name contains every word of the query, best first: the whole query as a phrase at the start of the
 * name, the phrase anywhere, a word starting with the first query word, then the rest; ties keep the graph's order.
 */
export function searchNarrators(items: NarratorSearchItem[], query: string, limit = 8): NarratorSearchItem[] {
  const phrase = normalizeArabic(query);
  const words = phrase.split(" ").filter(Boolean);
  if (words.length === 0) return [];

  const scored: { item: NarratorSearchItem; score: number; order: number }[] = [];
  items.forEach((item, order) => {
    const haystack = normalizeArabic(`${item.name} ${item.shortName ?? ""}`);
    if (!words.every((w) => haystack.includes(w))) return;
    const name = normalizeArabic(item.name);
    const score = name.startsWith(phrase)
      ? 0
      : name.includes(phrase)
        ? 1
        : name.split(" ").some((part) => part.startsWith(words[0]))
          ? 2
          : 3;
    scored.push({ item, score, order });
  });
  return scored.sort((a, b) => a.score - b.score || a.order - b.order).slice(0, limit).map((s) => s.item);
}
