import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getComparativeTree } from '@/lib/api';

export function useTakhreej(hadithIds: string[]) {
  return useQuery({
    queryKey: ['takhreej', ...[...hadithIds].sort()],
    queryFn: () => getComparativeTree(hadithIds),
    enabled: hadithIds.length >= 2,
    // Removing a source changes the ids: keep showing the current graph while the next one loads.
    placeholderData: keepPreviousData,
  });
}
