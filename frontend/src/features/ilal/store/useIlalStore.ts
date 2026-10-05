import { create } from 'zustand';
import { IlalReportDto } from '@/types/api';

interface IlalState {
  /** The report currently shown on the canvas (used to decorate nodes and edges). */
  report: IlalReportDto | null;
  /** Narrators highlighted by the selected finding. */
  highlightedNarratorIds: string[];
  /** Index of the selected finding in report.findings. */
  activeFindingIndex: number | null;
  /** The panel's folded low-confidence group is open: its findings are drawn on the graph too. */
  showLowConfidence: boolean;
  setShowLowConfidence: (show: boolean) => void;
  setReport: (report: IlalReportDto | null) => void;
  selectFinding: (index: number | null) => void;
  reset: () => void;
}

export const useIlalStore = create<IlalState>((set, get) => ({
  report: null,
  highlightedNarratorIds: [],
  activeFindingIndex: null,
  showLowConfidence: false,
  setShowLowConfidence: (showLowConfidence) => set({ showLowConfidence }),
  setReport: (report) => set({ report, highlightedNarratorIds: [], activeFindingIndex: null, showLowConfidence: false }),
  selectFinding: (index) => {
    const finding = index === null ? undefined : get().report?.findings[index];
    set({
      activeFindingIndex: finding ? index : null,
      highlightedNarratorIds: finding?.narratorIds ?? [],
    });
  },
  reset: () => set({ report: null, highlightedNarratorIds: [], activeFindingIndex: null, showLowConfidence: false }),
}));
