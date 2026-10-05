"use client";

import { CircleSlash, ShieldCheck, TriangleAlert } from "lucide-react";
import GlossaryTerm from "@/features/glossary/GlossaryTerm";
import type { EvidenceStatus } from "@/types/evidence";

const STYLES: Record<EvidenceStatus, { label: string; glossaryId: string; classes: string; Icon: typeof ShieldCheck }> = {
  supported: {
    label: "مؤيَّد بنص مصدر",
    glossaryId: "evidence-supported",
    classes: "border-emerald-300 bg-emerald-50 text-emerald-900",
    Icon: ShieldCheck,
  },
  verify: {
    label: "يتطلب تحققاً",
    glossaryId: "evidence-verify",
    classes: "border-amber-300 bg-amber-50 text-amber-900",
    Icon: TriangleAlert,
  },
  insufficient: {
    label: "لا يوجد مرجع كافٍ",
    glossaryId: "evidence-insufficient",
    classes: "border-slate-300 bg-slate-100 text-ink",
    Icon: CircleSlash,
  },
};

/**
 * Shows how well the information next to it is backed: by an attributed source text, a machine inference
 * or disagreement that needs checking, or nothing in the tool's data. The label is also a glossary term.
 */
export default function EvidenceBadge({ status, compact = false }: { status: EvidenceStatus; compact?: boolean }) {
  const { label, glossaryId, classes, Icon } = STYLES[status];
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border font-bold ${classes} ${
        compact ? "px-1.5 py-0.5 text-[10px]" : "px-2.5 py-1 text-xs"
      }`}
    >
      <Icon className={compact ? "h-3 w-3" : "h-3.5 w-3.5"} aria-hidden />
      <GlossaryTerm id={glossaryId}>{label}</GlossaryTerm>
    </span>
  );
}
