import { useMutation, useQuery } from '@tanstack/react-query';
import { explainIlal, getIlalForHadith } from '@/lib/api';
import { IlalReportDto } from '@/types/api';

/** Gathers the turuq of a hadith automatically and analyzes their ilal. */
export function useIlalForHadith(hadithId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['ilal', hadithId],
    queryFn: () => getIlalForHadith(hadithId),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}

/** Asks the AI to explain a report; only runs on demand. */
export function useIlalExplanation() {
  return useMutation({
    mutationFn: (report: IlalReportDto) => explainIlal(report),
  });
}
