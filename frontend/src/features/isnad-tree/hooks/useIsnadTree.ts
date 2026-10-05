import { useQuery } from '@tanstack/react-query';
import { getIsnadTree } from '@/lib/api';

export function useIsnadTree(hadithId: string) {
  return useQuery({
    queryKey: ['isnadTree', hadithId],
    queryFn: () => getIsnadTree(hadithId),
    enabled: !!hadithId,
  });
}
