"use client";

import { useMemo } from "react";

interface HighlightedTextProps {
  text: string;
  query?: string;
  phrases?: string[];
  className?: string;
  highlightClassName?: string;
}

const TASHKEEL_REGEX = "[\u064B-\u065F\u0670\u0640]*";

/**
 * Strips Arabic diacritics and tatweel from a string
 */
export function stripArabicDiacritics(str: string): string {
  return str.replace(/[\u064B-\u065F\u0670\u0640]/g, "");
}

/**
 * Escapes regex special characters
 */
function escapeRegExp(str: string): string {
  return str.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Converts an Arabic character to a diacritic-tolerant & variant-tolerant regex pattern
 */
function buildArabicCharPattern(char: string): string {
  // Alef variations: ا, أ, إ, آ, ٱ
  if (/[اأإآٱ\u0671]/.test(char)) {
    return `[اأإآٱ\\u0671]${TASHKEEL_REGEX}`;
  }
  // Taa Marbuta & Haa: ة, ه
  if (/[ةه]/.test(char)) {
    return `[ةه]${TASHKEEL_REGEX}`;
  }
  // Yaa & Alef Maqsura: ي, ى, ئ
  if (/[يىئ]/.test(char)) {
    return `[يىئ]${TASHKEEL_REGEX}`;
  }
  // Waw & Waw with Hamza: و, ؤ
  if (/[وؤ]/.test(char)) {
    return `[وؤ]${TASHKEEL_REGEX}`;
  }
  
  const escaped = escapeRegExp(char);
  return `${escaped}${TASHKEEL_REGEX}`;
}

/**
 * Converts a search word into a regex pattern that matches the word in Arabic text
 * regardless of diacritics (tashkeel) or character variants.
 */
function buildArabicWordPattern(word: string): string {
  const cleanWord = stripArabicDiacritics(word);
  if (!cleanWord) return "";
  
  const charPatterns = Array.from(cleanWord).map(buildArabicCharPattern);
  return `${TASHKEEL_REGEX}${charPatterns.join("")}`;
}

/**
 * Slices text into matched and non-matched segments
 */
export function getHighlightedParts(
  text: string,
  query?: string,
  phrases?: string[]
): { text: string; match: boolean }[] {
  if (!text) return [];

  // Gather all input phrases
  const allInputs: string[] = [];
  if (query && query.trim().length >= 2) {
    allInputs.push(query.trim());
  }
  if (phrases && phrases.length > 0) {
    for (const p of phrases) {
      if (p && p.trim().length >= 2) {
        allInputs.push(p.trim());
      }
    }
  }

  if (allInputs.length === 0) {
    return [{ text, match: false }];
  }

  const patterns: string[] = [];
  const individualWords: string[] = [];

  for (const input of allInputs) {
    const rawWords = input.split(/\s+/);
    if (rawWords.length > 1) {
      const phrasePattern = rawWords
        .map(buildArabicWordPattern)
        .filter(Boolean)
        .join("\\s+");
      if (phrasePattern) {
        patterns.push(phrasePattern);
      }
    }
    for (const w of rawWords) {
      const cleanW = stripArabicDiacritics(w);
      if (cleanW.length >= 2) {
        individualWords.push(cleanW);
      }
    }
  }

  const uniqueWords = Array.from(new Set(individualWords));
  uniqueWords.sort((a, b) => b.length - a.length);
  for (const w of uniqueWords) {
    const pat = buildArabicWordPattern(w);
    if (pat) {
      patterns.push(pat);
    }
  }

  if (patterns.length === 0) {
    return [{ text, match: false }];
  }

  const combinedRegex = new RegExp(`(?:(${patterns.join(")|(")}))`, "gi");
  const parts: { text: string; match: boolean }[] = [];
  let lastIndex = 0;
  let m: RegExpExecArray | null;

  while ((m = combinedRegex.exec(text)) !== null) {
    if (m[0].length === 0) {
      combinedRegex.lastIndex++;
      continue;
    }

    if (m.index > lastIndex) {
      parts.push({ text: text.slice(lastIndex, m.index), match: false });
    }

    parts.push({ text: m[0], match: true });
    lastIndex = m.index + m[0].length;
  }

  if (lastIndex < text.length) {
    parts.push({ text: text.slice(lastIndex), match: false });
  }

  return parts;
}

/**
 * Renders Arabic text with search terms highlighted
 */
export default function HighlightedText({
  text,
  query,
  phrases,
  className,
  highlightClassName = "bg-amber-200/90 text-amber-950 font-bold px-1 py-0.5 rounded-sm shadow-2xs",
}: HighlightedTextProps) {
  const parts = useMemo(() => getHighlightedParts(text, query, phrases), [text, query, phrases]);

  if (!text) return null;

  const content = parts.map((part, index) =>
    part.match ? (
      <mark key={index} className={highlightClassName}>
        {part.text}
      </mark>
    ) : (
      <span key={index}>{part.text}</span>
    )
  );

  if (className) {
    return <span className={className}>{content}</span>;
  }

  return <>{content}</>;
}
