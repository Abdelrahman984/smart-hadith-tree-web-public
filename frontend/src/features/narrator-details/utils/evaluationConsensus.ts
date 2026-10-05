import type { ScholarEvaluationDto } from "@/types/api";
import type { EvidenceStatus } from "@/types/evidence";

/** Which side of the jarh/ta'dil divide a recorded verdict falls on. */
export type VerdictCamp = "tadil" | "jarh" | "unclassified";

const TADIL = new Set(["reliable", "mostly_reliable", "companion"]);
// "unknown" (majhul) is a form of criticism: it is not an endorsement, so it counts against ta'dil.
const JARH = new Set(["weak", "abandoned", "fabricator", "unknown"]);

export function campOf(verdictRating: string | null | undefined): VerdictCamp {
  const v = verdictRating?.toLowerCase();
  if (!v) return "unclassified";
  if (TADIL.has(v)) return "tadil";
  if (JARH.has(v)) return "jarh";
  return "unclassified";
}

export type ConsensusStatus =
  /** Both endorsing and criticising verdicts are recorded. */
  | "dispute"
  /** Two or more classified verdicts, all on the same side. */
  | "agree"
  /** Only one classified verdict: not enough to speak of agreement or disagreement. */
  | "single"
  /** No classified verdict. */
  | "none";

export interface EvaluationGroup {
  camp: VerdictCamp;
  items: { evaluation: ScholarEvaluationDto; index: number }[];
}

export interface ConsensusResult {
  status: ConsensusStatus;
  tadilCount: number;
  jarhCount: number;
  /** Camp of the verdicts when status is "agree". */
  agreedCamp: Exclude<VerdictCamp, "unclassified"> | null;
  /** Evaluations grouped by camp, in the order ta'dil, jarh, unclassified; empty groups omitted. */
  groups: EvaluationGroup[];
}

/**
 * Compares the recorded verdicts of different scholars on a narrator. This relies only on the `verdictRating`
 * assigned in the dataset, not on reading the quotes, so it is a prompt to read the quotes rather than a ruling.
 */
export function analyzeEvaluations(evaluations: ScholarEvaluationDto[]): ConsensusResult {
  const buckets: Record<VerdictCamp, EvaluationGroup> = {
    tadil: { camp: "tadil", items: [] },
    jarh: { camp: "jarh", items: [] },
    unclassified: { camp: "unclassified", items: [] },
  };
  evaluations.forEach((evaluation, index) => {
    buckets[campOf(evaluation.verdictRating)].items.push({ evaluation, index });
  });

  const tadilCount = buckets.tadil.items.length;
  const jarhCount = buckets.jarh.items.length;
  const classified = tadilCount + jarhCount;

  let status: ConsensusStatus;
  if (tadilCount > 0 && jarhCount > 0) status = "dispute";
  else if (classified >= 2) status = "agree";
  else if (classified === 1) status = "single";
  else status = "none";

  return {
    status,
    tadilCount,
    jarhCount,
    agreedCamp: status === "agree" ? (tadilCount > 0 ? "tadil" : "jarh") : null,
    groups: [buckets.tadil, buckets.jarh, buckets.unclassified].filter((g) => g.items.length > 0),
  };
}

export interface NarratorEvidence {
  status: EvidenceStatus;
  /** Short plain-language reason, shown beside the badge. */
  reason: string;
}

/** Evidence status of what the tool can say about a narrator, from the recorded evaluations. */
export function narratorEvidence(consensus: ConsensusResult, evaluationCount: number): NarratorEvidence {
  if (evaluationCount === 0) {
    return { status: "insufficient", reason: "لا توجد أقوال مسجلة لهذا الراوي في البيانات." };
  }
  switch (consensus.status) {
    case "agree":
      return { status: "supported", reason: "أقوال منسوبة لأصحابها ومتفقة في الاتجاه؛ اقرأ نصوصها أدناه." };
    case "dispute":
      return { status: "verify", reason: "الأقوال المسجلة مختلفة؛ لا يكفي أحدها وحده." };
    case "single":
      return { status: "verify", reason: "قول مصنَّف واحد فقط؛ لا يكفي للجزم." };
    default:
      return { status: "verify", reason: "أقوال غير مصنَّفة؛ اقرأ نصوصها بنفسك." };
  }
}
