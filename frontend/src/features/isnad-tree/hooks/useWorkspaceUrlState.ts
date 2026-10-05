import { useEffect } from "react";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { useGraphViewStore } from "../store/useGraphViewStore";
import { NARRATOR_TAB, useWorkspaceStore } from "../store/useWorkspaceStore";

/** Query parameters that make a view shareable: `?tab=ilal&narrator=<id>&book=<name>`. */
const PARAMS = { tab: "tab", narrator: "narrator", book: "book" } as const;

interface Options {
  /** Ids of the tabs the page defines (the narrator tab is implied by `narrator`). */
  tabIds: string[];
  defaultTab: string;
  /** Only pages that compare books use the `book` parameter. */
  bookFocus: boolean;
}

/**
 * Keeps the open tab, the selected narrator and the focused book in the address (replacing the URL, never adding
 * history entries) and restores them on load, so a link reopens the same view.
 */
export function useWorkspaceUrlState({ tabIds, defaultTab, bookFocus }: Options) {
  // Restore from the address once, when the workspace mounts.
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const tab = params.get(PARAMS.tab);
    const narrator = params.get(PARAMS.narrator);
    const book = params.get(PARAMS.book);

    if (tab && tabIds.includes(tab)) useWorkspaceStore.getState().openTab(tab);
    if (bookFocus && book) useGraphViewStore.getState().setFocusBook(book);
    if (narrator) {
      useNarratorDrawerStore.getState().openDrawer(narrator);
      useGraphViewStore.getState().revealNodes([narrator]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- restore once
  }, []);

  // Write the state back whenever it changes.
  useEffect(() => {
    const write = () => {
      const { panelOpen, activeTab, previousTab } = useWorkspaceStore.getState();
      const { selectedNarratorId, isOpen } = useNarratorDrawerStore.getState();
      const { focusBook } = useGraphViewStore.getState();

      // The narrator tab is temporary: the address keeps the tab to come back to.
      const shownTab = activeTab === NARRATOR_TAB ? previousTab : activeTab;
      let search = window.location.search;
      search = withParam(search, PARAMS.tab, panelOpen && shownTab && shownTab !== defaultTab ? shownTab : null);
      search = withParam(search, PARAMS.narrator, isOpen ? selectedNarratorId : null);
      search = withParam(search, PARAMS.book, bookFocus ? focusBook : null);

      if (search !== window.location.search) window.history.replaceState(null, "", `${window.location.pathname}${search}`);
    };

    const unsubscribers = [
      useWorkspaceStore.subscribe(write),
      useNarratorDrawerStore.subscribe(write),
      useGraphViewStore.subscribe(write),
    ];
    return () => unsubscribers.forEach((u) => u());
  }, [defaultTab, bookFocus]);
}

/**
 * Sets or removes one query parameter without touching the others: `URLSearchParams` would re-encode them all
 * (`ids=a,b` becomes `ids=a%2Cb`), changing links the app built itself.
 */
function withParam(search: string, key: string, value: string | null | undefined): string {
  const kept = search.replace(/^\?/, "").split("&").filter((part) => part && part.split("=")[0] !== key);
  if (value) kept.push(`${key}=${encodeURIComponent(value)}`);
  return kept.length > 0 ? `?${kept.join("&")}` : "";
}
