"use client";

import { useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type KeyboardEvent, type PointerEvent, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, UserCheck, X, type LucideIcon } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { useMediaQuery } from "@/components/useMediaQuery";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import NarratorDetails from "@/features/narrator-details/components/NarratorDetails";
import NarratorDrawer from "@/features/narrator-details/components/NarratorDrawer";
import { useNarratorDrawerStore } from "@/features/narrator-details/store/useNarratorDrawerStore";
import { isPhone } from "@/lib/viewport";
import { useWorkspaceUrlState } from "../hooks/useWorkspaceUrlState";
import { NARRATOR_TAB, PANEL_MAX_WIDTH, PANEL_MIN_WIDTH, useWorkspaceStore } from "../store/useWorkspaceStore";

const STORAGE_WIDTH = "hadith-workspace:panel-width";
const STORAGE_TAB = "hadith-workspace:tab";

/** localStorage can throw (private windows, blocked storage): the workspace works without it. */
const readStored = (key: string) => {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
};
const writeStored = (key: string, value: string) => {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    /* ignore */
  }
};

export interface WorkspaceTab {
  id: string;
  label: string;
  Icon: LucideIcon;
  badge?: ReactNode;
  /** `active` is false while another tab is showing; the content stays mounted so it keeps its state. */
  render: (active: boolean) => ReactNode;
}

interface HadithWorkspaceProps {
  /** The hadith (or comparison) title block, under the site header. */
  header: ReactNode;
  /** An optional strip between the header and the graph. */
  summary?: ReactNode;
  canvas: ReactNode;
  tabs: WorkspaceTab[];
  defaultTab: string;
  /** "desktop": the panel starts open beside the graph (not on phones). "never": it starts closed. */
  panelStartsOpen: "desktop" | "never";
  /** Word for the panel's toggle button: «إظهار <label>». */
  panelLabel: string;
  /** The page compares books, so `?book=` is kept in the address. */
  bookFocus?: boolean;
}

/**
 * The page shared by the single tree and the takhreej tree: site header, hadith header, graph, and a side panel of
 * tabs. On wide screens (1024px up) the selected narrator opens as a tab of the panel, so the graph stays visible
 * and the card stays selected; below that it opens as a drawer over the graph.
 */
export default function HadithWorkspace({
  header,
  summary,
  canvas,
  tabs,
  defaultTab,
  panelStartsOpen,
  panelLabel,
  bookFocus = false,
}: HadithWorkspaceProps) {
  // Set the page's defaults before the first paint, so the panel does not slide open on load. Not during render:
  // the store has subscribers (the workspace of the page being left), and React forbids updating them while rendering.
  useLayoutEffect(() => {
    useWorkspaceStore.getState().init({ tab: defaultTab, open: panelStartsOpen === "desktop" && !isPhone() });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once, on mount
  }, []);

  const panelOpen = useWorkspaceStore((s) => s.panelOpen);
  const activeTab = useWorkspaceStore((s) => s.activeTab);
  const previousTab = useWorkspaceStore((s) => s.previousTab);
  const panelWidth = useWorkspaceStore((s) => s.panelWidth);
  const setPanelWidth = useWorkspaceStore((s) => s.setPanelWidth);
  const setPanelOpen = useWorkspaceStore((s) => s.setPanelOpen);
  const setTab = useWorkspaceStore((s) => s.setTab);
  const openTab = useWorkspaceStore((s) => s.openTab);

  const isWide = useMediaQuery("(min-width: 1024px)", true);
  const narratorOpen = useNarratorDrawerStore((s) => s.isOpen);
  const selectedNarratorId = useNarratorDrawerStore((s) => s.selectedNarratorId);
  const closeNarrator = useNarratorDrawerStore((s) => s.closeDrawer);
  const narratorInPanel = isWide && narratorOpen;

  useWorkspaceUrlState({ tabIds: tabs.map((t) => t.id), defaultTab, bookFocus });

  // Remembered between visits: the panel's width and the tab last used (unless the address names one).
  // Read after mount, so a server-rendered page and the browser start from the same markup.
  const tabIds = tabs.map((t) => t.id).join(",");
  useEffect(() => {
    const width = Number(readStored(STORAGE_WIDTH));
    if (width) setPanelWidth(width);
    const tab = readStored(STORAGE_TAB);
    const named = new URLSearchParams(window.location.search).has("tab");
    if (!named && tab && tabIds.split(",").includes(tab)) setTab(tab);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once, on mount
  }, []);
  useEffect(
    () =>
      useWorkspaceStore.subscribe((state, prev) => {
        if (state.activeTab !== prev.activeTab && state.activeTab !== NARRATOR_TAB && state.activeTab) {
          writeStored(STORAGE_TAB, state.activeTab);
        }
      }),
    []
  );

  // Dragging the panel's edge (or the arrow keys on it) resizes it.
  const bodyRef = useRef<HTMLDivElement>(null);
  const [resizing, setResizing] = useState(false);
  const maxWidth = () => Math.min(PANEL_MAX_WIDTH, (bodyRef.current?.getBoundingClientRect().width ?? 1200) * 0.6);
  const resizeTo = (width: number) => setPanelWidth(Math.min(maxWidth(), width));
  const onResizeMove = (e: PointerEvent<HTMLDivElement>) => {
    if (resizing) resizeTo(e.clientX - (bodyRef.current?.getBoundingClientRect().left ?? 0));
  };
  const endResize = () => {
    setResizing(false);
    writeStored(STORAGE_WIDTH, String(useWorkspaceStore.getState().panelWidth));
  };
  const onResizeKey = (e: KeyboardEvent<HTMLDivElement>) => {
    const step = e.shiftKey ? 96 : 24;
    // The panel is on the left: moving its edge right widens it.
    const next = { ArrowRight: panelWidth + step, ArrowLeft: panelWidth - step, Home: PANEL_MIN_WIDTH, End: PANEL_MAX_WIDTH }[e.key];
    if (next === undefined) return;
    e.preventDefault();
    resizeTo(next);
    writeStored(STORAGE_WIDTH, String(useWorkspaceStore.getState().panelWidth));
  };

  // A selected narrator opens the narrator tab on wide screens, and closing it returns to the previous tab.
  useEffect(() => {
    if (narratorInPanel) openTab(NARRATOR_TAB);
    else if (useWorkspaceStore.getState().activeTab === NARRATOR_TAB) setTab(useWorkspaceStore.getState().previousTab);
  }, [narratorInPanel, openTab, setTab]);

  // On a phone the panel covers the graph: picking a finding closes it, so the narrators it names can be seen.
  useEffect(
    () =>
      useIlalStore.subscribe((state, prev) => {
        if (state.activeFindingIndex !== null && state.activeFindingIndex !== prev.activeFindingIndex && isPhone()) {
          setPanelOpen(false);
        }
      }),
    [setPanelOpen]
  );

  const allTabs: WorkspaceTab[] = narratorInPanel
    ? [...tabs, { id: NARRATOR_TAB, label: "الراوي", Icon: UserCheck, render: () => null }]
    : tabs;
  const shownTab = allTabs.some((t) => t.id === activeTab) ? activeTab : previousTab || defaultTab;

  return (
    <div className="h-screen w-full flex flex-col overflow-hidden bg-surface-muted relative" dir="rtl">
      <SiteHeader variant="compact" />
      {header}
      {summary}

      <div ref={bodyRef} className="flex-1 flex overflow-hidden relative" style={{ "--panel-w": `${panelWidth}px` } as CSSProperties}>
        <main id="main-content" className="flex-1 relative h-full">
          {canvas}
          {!isWide && <NarratorDrawer />}
        </main>

        <aside
          id="workspace-panel"
          aria-label={`لوحة ${panelLabel}`}
          className={`absolute inset-y-0 left-0 z-20 md:relative md:z-10 ${
            resizing ? "" : "transition-all duration-300 ease-in-out"
          } border-r border-line bg-surface flex flex-col overflow-hidden shadow-xl md:shadow-none ${
            panelOpen ? "w-full sm:w-[var(--panel-w)]" : "w-0 border-r-0"
          }`}
        >
          <div className="flex min-h-0 flex-1 flex-col min-w-[100vw] sm:min-w-[var(--panel-w)]">
            <div className="flex items-center justify-between border-b border-line px-4 py-2 md:hidden">
              <span className="text-sm font-bold text-ink">{panelLabel}</span>
              <button
                type="button"
                onClick={() => setPanelOpen(false)}
                className="flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-ink-muted hover:bg-slate-100 cursor-pointer"
              >
                <X className="w-4 h-4" aria-hidden />
                <span>إغلاق والعودة للشجرة</span>
              </button>
            </div>

            <div role="tablist" aria-label="أقسام اللوحة" className="mx-4 mt-4 flex shrink-0 rounded-lg bg-slate-100 p-1 text-sm font-semibold">
              {allTabs.map(({ id, label, Icon, badge }) => (
                <button
                  key={id}
                  type="button"
                  role="tab"
                  id={`workspace-tab-${id}`}
                  aria-selected={shownTab === id}
                  aria-controls={`workspace-tabpanel-${id}`}
                  onClick={() => setTab(id)}
                  className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 transition-colors cursor-pointer ${
                    shownTab === id ? "bg-surface text-brand-blue shadow-sm" : "text-ink-subtle hover:text-ink"
                  }`}
                >
                  <Icon className="w-4 h-4" aria-hidden /> {label}
                  {badge}
                </button>
              ))}
            </div>

            <div className="min-h-0 flex-1 overflow-y-auto">
              {tabs.map((tab) => (
                <div
                  key={tab.id}
                  role="tabpanel"
                  id={`workspace-tabpanel-${tab.id}`}
                  aria-labelledby={`workspace-tab-${tab.id}`}
                  hidden={shownTab !== tab.id}
                  className="p-4 space-y-5"
                >
                  {tab.render(shownTab === tab.id)}
                </div>
              ))}
              {narratorInPanel && (
                <div
                  role="tabpanel"
                  id={`workspace-tabpanel-${NARRATOR_TAB}`}
                  aria-labelledby={`workspace-tab-${NARRATOR_TAB}`}
                  hidden={shownTab !== NARRATOR_TAB}
                >
                  <div className="flex items-center justify-between border-b border-line px-4 py-2">
                    <h2 className="text-base font-bold text-ink">تفاصيل الراوي</h2>
                    <button
                      type="button"
                      onClick={closeNarrator}
                      aria-label="إغلاق تفاصيل الراوي"
                      className="rounded-full p-1 text-ink-subtle hover:bg-slate-100 cursor-pointer"
                    >
                      <X size={18} />
                    </button>
                  </div>
                  <NarratorDetails key={selectedNarratorId} />
                </div>
              )}
            </div>
          </div>
        </aside>

        {panelOpen && (
          <div
            role="separator"
            aria-orientation="vertical"
            aria-label="تغيير عرض اللوحة"
            aria-valuemin={PANEL_MIN_WIDTH}
            aria-valuemax={PANEL_MAX_WIDTH}
            aria-valuenow={panelWidth}
            tabIndex={0}
            onPointerDown={(e) => {
              e.currentTarget.setPointerCapture(e.pointerId);
              setResizing(true);
            }}
            onPointerMove={onResizeMove}
            onPointerUp={endResize}
            onPointerCancel={endResize}
            onKeyDown={onResizeKey}
            className={`hidden md:block absolute inset-y-0 z-10 w-1.5 -translate-x-1/2 cursor-col-resize touch-none outline-none transition-colors hover:bg-brand-teal/40 focus-visible:bg-brand-teal/60 ${
              resizing ? "bg-brand-teal/60" : ""
            }`}
            style={{ left: "var(--panel-w)" }}
          />
        )}

        <button
          type="button"
          onClick={() => setPanelOpen(!panelOpen)}
          title={panelOpen ? `إخفاء ${panelLabel}` : `إظهار ${panelLabel}`}
          aria-label={panelOpen ? `إخفاء ${panelLabel}` : `إظهار ${panelLabel}`}
          aria-controls="workspace-panel"
          aria-expanded={panelOpen}
          className={`absolute top-1/2 -translate-y-1/2 bg-surface border border-l-0 border-line p-2 rounded-r-lg shadow-md z-20 text-ink-subtle hover:text-brand-blue transition-all duration-300 ease-in-out items-center justify-center gap-1 cursor-pointer ${
            panelOpen ? "hidden md:flex md:left-[var(--panel-w)]" : "flex left-0"
          }`}
        >
          {panelOpen ? <ChevronLeft className="w-5 h-5" /> : <ChevronRight className="w-5 h-5" />}
          {!panelOpen && <span className="text-xs font-semibold md:hidden">{panelLabel}</span>}
        </button>
      </div>
    </div>
  );
}
