import type { ComparativeHadithSourceDto, ComparativeIsnadNodeDto, ComparativeTreeResponseDto, IlalTariqDto } from "@/types/api";

export interface CompanionRef {
  narratorId: string;
  name: string;
}

export interface SourceGroups {
  /** The Companion most of the narrations come through (the first narration's, on a tie). */
  main: CompanionRef | null;
  /** Narrations of the same hadith: through the main Companion, or one that could not be placed. */
  turuq: ComparativeHadithSourceDto[];
  /** Narrations through another Companion: a hadith with the same wording, not another route of this one. */
  shawahid: { source: ComparativeHadithSourceDto; companion: CompanionRef }[];
}

/** The Companion of a narration: the top of its chain, i.e. the narrator with the highest step among its transmissions. */
function companionOf(hadithId: string, nodes: ComparativeIsnadNodeDto[]): CompanionRef | null {
  const own = nodes.filter((n) => n.sourceHadithIds?.includes(hadithId));
  if (own.length === 0) return null;

  const top = Math.max(...own.map((n) => n.stepOrder));
  const tips = own.filter((n) => n.stepOrder === top);
  // Several chains (tahwil) can end at different narrators: take the one most chains share.
  const counts = new Map<string, number>();
  for (const n of tips) counts.set(n.narratorId, (counts.get(n.narratorId) ?? 0) + 1);
  const best = [...counts.entries()].sort((a, b) => b[1] - a[1])[0][0];
  const node = tips.find((n) => n.narratorId === best)!;
  return { narratorId: node.narratorId, name: node.knownAs || node.narratorName };
}

/** The split the server already made (`IlalTariqDto.isShahid`): the same one its findings and madars were computed on. */
function groupByServer(sources: ComparativeHadithSourceDto[], turuq: IlalTariqDto[]): SourceGroups {
  const turuqOf = new Map<string, IlalTariqDto[]>();
  for (const t of turuq) turuqOf.set(t.hadithId, [...(turuqOf.get(t.hadithId) ?? []), t]);

  const companionOfSource = (hadithId: string): CompanionRef | null => {
    const withCompanion = turuqOf.get(hadithId)?.find((t) => t.companionId);
    return withCompanion?.companionId ? { narratorId: withCompanion.companionId, name: withCompanion.companionName ?? "" } : null;
  };

  const routes: ComparativeHadithSourceDto[] = [];
  const shawahid: SourceGroups["shawahid"] = [];
  for (const source of sources) {
    const entries = turuqOf.get(source.hadithId) ?? [];
    const companion = companionOfSource(source.hadithId);
    if (entries.length > 0 && entries.every((t) => t.isShahid) && companion) shawahid.push({ source, companion });
    else routes.push(source);
  }
  // The main Companion is the one the first route that reaches a Companion ends at.
  const main = routes.map((s) => companionOfSource(s.hadithId)).find((c) => c !== null) ?? null;
  return { main, turuq: routes, shawahid };
}

/**
 * Splits the compared narrations into routes of one hadith (turuq) and witnesses (shawahid, through another Companion).
 * The server decides (a chain that stops short of a Companion is still a route); without its answer, e.g. an older
 * API, the Companion is read from the top of each chain in the tree.
 */
export function groupSourcesByCompanion(
  tree: Pick<ComparativeTreeResponseDto, "sources" | "nodes"> & { ilalReport?: ComparativeTreeResponseDto["ilalReport"] }
): SourceGroups {
  const serverTuruq = tree.ilalReport?.turuq;
  if (serverTuruq?.some((t) => t.isShahid !== undefined)) return groupByServer(tree.sources, serverTuruq);

  const companions = new Map(tree.sources.map((s) => [s.hadithId, companionOf(s.hadithId, tree.nodes)] as const));

  const counts = new Map<string, number>();
  for (const c of companions.values()) if (c) counts.set(c.narratorId, (counts.get(c.narratorId) ?? 0) + 1);
  // Most common Companion; on a tie the first narration's wins (Map keeps insertion order and sort is stable).
  const mainId = [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? null;
  const main = [...companions.values()].find((c) => c?.narratorId === mainId) ?? null;

  const turuq: ComparativeHadithSourceDto[] = [];
  const shawahid: SourceGroups["shawahid"] = [];
  for (const source of tree.sources) {
    const companion = companions.get(source.hadithId);
    if (companion && main && companion.narratorId !== main.narratorId) shawahid.push({ source, companion });
    else turuq.push(source);
  }
  return { main, turuq, shawahid };
}
