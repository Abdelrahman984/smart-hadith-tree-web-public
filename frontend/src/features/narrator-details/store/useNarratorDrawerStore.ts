import { create } from 'zustand';

/** What the clicked node knows about this link in the chain, shown in the drawer (hover tooltips do not work on touch screens). */
export interface NarratorNodeContext {
  anomalyReason?: string | null;
  travelNote?: string | null;
}

interface DrawerState {
  isOpen: boolean;
  selectedNarratorId: string | null;
  nodeContext: NarratorNodeContext | null;
  openDrawer: (id: string, context?: NarratorNodeContext) => void;
  closeDrawer: () => void;
}

export const useNarratorDrawerStore = create<DrawerState>((set) => ({
  isOpen: false,
  selectedNarratorId: null,
  nodeContext: null,
  openDrawer: (id, context) => set({ isOpen: true, selectedNarratorId: id, nodeContext: context ?? null }),
  closeDrawer: () => set({ isOpen: false, selectedNarratorId: null, nodeContext: null }),
}));
