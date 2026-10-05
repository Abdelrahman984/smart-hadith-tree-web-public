import { useState } from "react";
import { useRouter } from "next/navigation";
import { getRelatedHadiths } from "@/lib/api";

/**
 * "Takhreej" for one hadith: gathers its related narrations and opens the comparative tree.
 * The takhreej page needs at least two ids, so a hadith without related narrations opens its own tree.
 */
export function useAutoTakhreej() {
  const router = useRouter();
  const [loadingId, setLoadingId] = useState<string | null>(null);
  const [error, setError] = useState<{ id: string; message: string } | null>(null);

  const start = async (hadithId: string) => {
    setLoadingId(hadithId);
    setError(null);
    try {
      const related = await getRelatedHadiths(hadithId);
      const ids = [hadithId, ...related.map((r) => r.id).filter((id) => id !== hadithId)];
      router.push(ids.length >= 2 ? `/takhreej?ids=${ids.join(",")}` : `/tree/${hadithId}`);
    } catch {
      setError({ id: hadithId, message: "تعذر جلب الروايات المتعلقة بهذا الحديث. حاول مرة أخرى." });
    } finally {
      setLoadingId(null);
    }
  };

  return { start, loadingId, error, clearError: () => setError(null) };
}
