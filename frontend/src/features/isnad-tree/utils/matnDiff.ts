import type { MatnComparisonDto, MatnSegmentDto } from "@/types/api";

/**
 * A TypeScript port of the server's `MatnText` (extract the matn body of a text that stores isnad and matn together) and
 * `MatnAligner` (word-level longest-common-subsequence alignment), so a comparison drawn in the browser cuts and aligns
 * texts the same way the Ilal findings do. Keep the regular expressions in step with
 * `src/SmartHadithTree.Domain/Utilities/MatnText.cs`; the test cases in `tests/matnDiff.spec.ts` come from its tests.
 */

const HONORIFICS =
  /صل[يى]\s+الله\s+عليه\s+(?:و?اله\s+)?وسلم|ﷺ|رضي\s+الله\s+عنهما|رضي\s+الله\s+عنهم|رضي\s+الله\s+عنها|رضي\s+الله\s+عنه|عليه\s+(?:السلام|الصلا[ةه]\s+والسلام)/gu;
const NON_LETTER = /[^\p{L}\s]/gu;
const DIACRITICS = /\p{Mn}|ـ/gu;
const WHITESPACE = /\s+/g;

/** The start of the matn: a transmission/speech verb directly followed by a reference to the Prophet ﷺ. */
const MATN_START = /(?<!\p{L})[وف]?(?:قال|ان|عن|سمعت|سمع|رايت|كان|يقول|كنت\s+مع)\s+(?:رسول\s+الله|النبي|نبي\s+الله)/u;
const TRANSMISSION_TERM = /(?:^|\s)(?:حدثنا|حدثني|اخبرنا|اخبرني|انبانا|سمعت|عن)\s/gu;
const SPEECH_START = /\s(?:قال|يقول|انه|انها|ان)\s/gu;
/** Compiler commentary, editorial grading notes, or a following isnad appended after the matn. */
const TRAILING_COMMENTARY =
  /(?:^|\s)(?:قال\s+ابو\s+عيسي|قال\s+ابو\s+داود|قال\s+ابو\s+عبد\s+الرحمن|قال\s+ابو\s+عبد\s+الله|قال\s+ابو\s+الحسن|قال\s+الشيخ|قال\s+النسايي|قال\s+الاعظمي|قال\s+الالباني|قال\s+شعيب|قال\s+حسين\s+سليم|قال\s+المحقق|اسناده\s+صحيح|اسناده\s+حسن|اسناده\s+ضعيف|هذا\s+حديث|وفي\s+الباب\s+عن|وحدثنا|وحدثني|بهذا\s+الاسناد|فذكر\s+الحديث|فذكر\s+نحوه)(?:\s|$)/u;

/** Normalizes for comparison: no diacritics, tatweel, honorifics or punctuation; one form of alef, taa marbuta, yaa. */
export function normalizeForComparison(text: string | null | undefined): string {
  if (!text || !text.trim()) return "";
  return text
    .replace(DIACRITICS, "")
    .replace(/[أإآ]/g, "ا")
    .replace(/ة/g, "ه")
    .replace(/ى/g, "ي")
    .replace(/ؤ/g, "و")
    .replace(/ئ/g, "ي")
    .replace(HONORIFICS, " ")
    .replace(NON_LETTER, " ")
    .replace(WHITESPACE, " ")
    .trim();
}

export const tokenize = (text: string | null | undefined): string[] => normalizeForComparison(text).split(" ").filter(Boolean);

/** The last match of a global regex within `text`, or null. */
function lastMatch(re: RegExp, text: string): RegExpExecArray | null {
  re.lastIndex = 0;
  let last: RegExpExecArray | null = null;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text)) !== null) {
    last = m;
    if (m[0].length === 0) re.lastIndex++;
  }
  return last;
}

/**
 * The normalized matn body: the text after the isnad, without trailing commentary. Without a Prophet marker the text
 * after the last transmission formula is used (a mawquf text), and failing that the whole text.
 */
export function extractBody(fullText: string | null | undefined): string {
  const normalized = normalizeForComparison(fullText);
  if (normalized.length === 0) return normalized;

  let start = 0;
  const found = MATN_START.exec(normalized);
  if (found) {
    start = found.index;
  } else {
    // The last transmission formula before 60% of the text, then past the narrator's name to the speech.
    const lastTerm = lastMatch(TRANSMISSION_TERM, normalized.slice(0, Math.floor(normalized.length * 0.6)));
    if (lastTerm) {
      const afterTerm = lastTerm.index + lastTerm[0].length;
      SPEECH_START.lastIndex = afterTerm;
      const speech = SPEECH_START.exec(normalized);
      start = speech ? speech.index + 1 : afterTerm;
    }
  }

  let body = normalized.slice(start);
  const tail = TRAILING_COMMENTARY.exec(body);
  if (tail && tail.index > 0) body = body.slice(0, tail.index);
  return body.trim();
}

/** Upper bound on tokens per text, keeping the table small. */
export const MAX_TOKENS = 800;

/** Aligns two token lists with the longest common subsequence: runs only in `compared` are additions, only in `reference` omissions. */
export function alignTokens(reference: string[], compared: string[]): { segments: MatnSegmentDto[]; similarity: number } {
  const a = reference.slice(0, MAX_TOKENS);
  const b = compared.slice(0, MAX_TOKENS);
  const n = a.length;
  const m = b.length;
  if (n === 0 && m === 0) return { segments: [], similarity: 1 };

  // lcs[i][j] = length of the LCS of a[i..] and b[j..]
  const width = m + 1;
  const lcs = new Int32Array((n + 1) * width);
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i * width + j] = a[i] === b[j] ? lcs[(i + 1) * width + j + 1] + 1 : Math.max(lcs[(i + 1) * width + j], lcs[i * width + j + 1]);
    }
  }

  const segments: MatnSegmentDto[] = [];
  let kind: MatnSegmentDto["kind"] = "equal";
  let run: string[] = [];
  const emit = (next: MatnSegmentDto["kind"], token: string) => {
    if (run.length > 0 && next !== kind) {
      segments.push({ kind, text: run.join(" ") });
      run = [];
    }
    kind = next;
    run.push(token);
  };

  let x = 0;
  let y = 0;
  while (x < n && y < m) {
    if (a[x] === b[y]) {
      emit("equal", a[x]);
      x++;
      y++;
    } else if (lcs[(x + 1) * width + y] >= lcs[x * width + y + 1]) {
      emit("removed", a[x++]);
    } else {
      emit("added", b[y++]);
    }
  }
  while (x < n) emit("removed", a[x++]);
  while (y < m) emit("added", b[y++]);
  if (run.length > 0) segments.push({ kind, text: run.join(" ") });

  return { segments, similarity: (2 * lcs[0]) / (n + m) };
}

/** Compares the matn of one narration against a reference narration, as the Ilal findings do. */
export function compareMatns(
  reference: { hadithId: string; text: string },
  compared: { hadithId: string; text: string }
): MatnComparisonDto {
  const { segments, similarity } = alignTokens(tokenize(extractBody(reference.text)), tokenize(extractBody(compared.text)));
  return { referenceHadithId: reference.hadithId, comparedHadithId: compared.hadithId, similarity, segments };
}
