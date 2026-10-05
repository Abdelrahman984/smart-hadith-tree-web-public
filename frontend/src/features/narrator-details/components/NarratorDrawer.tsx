"use client";

import { X } from 'lucide-react';
import { useRef } from 'react';
import { useDialogFocus } from '@/components/useDialogFocus';
import { useNarratorDrawerStore } from '../store/useNarratorDrawerStore';
import NarratorDetails from './NarratorDetails';

/** Narrator details as a modal drawer over the graph, for screens too narrow for the workspace side panel. */
export default function NarratorDrawer() {
  const { isOpen, selectedNarratorId, closeDrawer } = useNarratorDrawerStore();
  const dialogRef = useRef<HTMLDivElement>(null);
  useDialogFocus(dialogRef, isOpen, closeDrawer);

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div className="absolute inset-0 bg-slate-900/20 z-40 transition-opacity" onClick={closeDrawer} />

      {/* Drawer Panel */}
      <div
        ref={dialogRef}
        tabIndex={-1}
        className="absolute top-0 bottom-0 start-0 w-full sm:w-96 bg-surface shadow-2xl z-50 flex flex-col outline-none animate-in slide-in-from-right duration-200"
        dir="rtl"
        role="dialog"
        aria-modal="true"
        aria-label="تفاصيل الراوي"
      >
        <div className="flex items-center justify-between p-4 border-b border-line">
          <h2 className="text-xl font-bold text-ink">تفاصيل الراوي</h2>
          <button
            onClick={closeDrawer}
            aria-label="إغلاق تفاصيل الراوي"
            className="p-1 rounded-full hover:bg-slate-100 text-ink-subtle cursor-pointer"
          >
            <X size={20} />
          </button>
        </div>
        <NarratorDetails key={selectedNarratorId} />
      </div>
    </>
  );
}
