import { useSyncExternalStore } from "react";

/**
 * Whether a CSS media query matches. The server (and hydration) uses `serverValue`, then the real value applies,
 * so server-rendered markup never mismatches.
 */
export function useMediaQuery(query: string, serverValue: boolean): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const list = window.matchMedia(query);
      list.addEventListener("change", onChange);
      return () => list.removeEventListener("change", onChange);
    },
    () => window.matchMedia(query).matches,
    () => serverValue
  );
}
