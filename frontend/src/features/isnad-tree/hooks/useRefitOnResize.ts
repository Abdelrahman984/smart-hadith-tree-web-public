import { RefObject, useEffect } from "react";

const DEBOUNCE_MS = 120;

/**
 * Refits the viewport whenever the canvas container changes size (window resize, sidebar
 * toggle, narrow panes), so the graph never stays off-screen after the layout settles.
 * Does nothing until the graph is ready, to avoid fighting the initial layout passes.
 */
export function useRefitOnResize(
  containerRef: RefObject<HTMLElement | null>,
  refit: () => void,
  enabled: boolean
) {
  useEffect(() => {
    const el = containerRef.current;
    if (!enabled || !el || typeof ResizeObserver === "undefined") return;

    let timer: ReturnType<typeof setTimeout> | undefined;
    const observer = new ResizeObserver(() => {
      clearTimeout(timer);
      timer = setTimeout(refit, DEBOUNCE_MS);
    });
    observer.observe(el);
    return () => {
      clearTimeout(timer);
      observer.disconnect();
    };
  }, [containerRef, refit, enabled]);
}
