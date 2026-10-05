import { useMediaQuery } from "@/components/useMediaQuery";
import { useGraphViewStore } from "../store/useGraphViewStore";

/** The legend is open: as the viewer chose, else from 768px up (where it sits beside the graph; on a phone it would cover it). */
export function useLegendOpen(): boolean {
  const isWide = useMediaQuery("(min-width: 768px)", true);
  const setting = useGraphViewStore((s) => s.legendOpen);
  return setting ?? isWide;
}
