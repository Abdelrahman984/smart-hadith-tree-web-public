"use client";

import Link from "next/link";
import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState } from "react";
import { getGlossaryEntry } from "./glossary";

const EDGE_GAP = 8;

/**
 * Wraps a term with a short plain-language explanation. The term is a button, so it works by tap,
 * click and keyboard (Enter/Space); a mouse hover also previews it. Escape or an outside tap closes it,
 * and the popover is nudged to stay inside the viewport. Falls back to plain text for unknown ids.
 */
export default function GlossaryTerm({ id, children }: { id: string; children: React.ReactNode }) {
  const entry = getGlossaryEntry(id);
  const [open, setOpen] = useState(false);
  const [shift, setShift] = useState(0);
  const rootRef = useRef<HTMLSpanElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const popRef = useRef<HTMLSpanElement>(null);
  const popId = useId();

  const close = useCallback(() => setOpen(false), []);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) close();
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        close();
        buttonRef.current?.focus();
      }
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open, close]);

  // Keep the popover inside the viewport horizontally (it is anchored to the term, which can sit near either edge).
  // It is display:none while closed so it never adds scrollable overflow on narrow screens.
  useLayoutEffect(() => {
    if (!open || !popRef.current) return;
    setShift(0);
    const r = popRef.current.getBoundingClientRect();
    const vw = document.documentElement.clientWidth;
    if (r.left < EDGE_GAP) setShift(EDGE_GAP - r.left);
    else if (r.right > vw - EDGE_GAP) setShift(vw - EDGE_GAP - r.right);
  }, [open]);

  if (!entry) return <>{children}</>;

  return (
    <span ref={rootRef} className="group relative inline-block" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={popId}
        onClick={() => setOpen((o) => !o)}
        className="cursor-help border-b border-dotted border-slate-400 bg-transparent p-0 font-[inherit] text-inherit focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-blue rounded-sm"
      >
        {children}
      </button>
      <span
        ref={popRef}
        id={popId}
        style={shift ? { transform: `translateX(${shift}px)` } : undefined}
        className={`absolute start-0 top-full z-50 mt-1 w-64 max-w-[calc(100vw-1rem)] rounded-lg border border-line bg-surface p-2.5 text-start text-xs font-normal leading-relaxed text-slate-700 shadow-lg ${
          open ? "block" : "hidden group-hover:block"
        }`}
      >
        <span className="block">{entry.definition}</span>
        <span className="mt-1 block text-[10px] text-ink-subtle" dir="ltr">
          {entry.english} — {entry.englishGloss}
        </span>
        <Link href={`/glossary#${entry.id}`} className="mt-1 inline-block text-[11px] font-semibold text-indigo-700 underline">
          المزيد في قاموس المصطلحات
        </Link>
      </span>
    </span>
  );
}
