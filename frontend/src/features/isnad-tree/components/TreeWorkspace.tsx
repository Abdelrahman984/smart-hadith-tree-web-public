"use client";

import Link from "next/link";
import { useState } from "react";
import { BookOpen, GitCompareArrows, Info, Loader2, ShieldAlert, ShieldQuestion } from "lucide-react";
import { bookHref } from "@/features/books/bookParams";
import ClampedText from "@/components/ClampedText";
import EvidenceBadge from "@/components/EvidenceBadge";
import { useAutoTakhreej } from "@/features/search/hooks/useAutoTakhreej";
import type { IsnadTreeResponseDto } from "@/types/api";
import { useWorkspaceStore } from "../store/useWorkspaceStore";
import HadithWorkspace, { type WorkspaceTab } from "./HadithWorkspace";
import IlalTabBadge from "./IlalTabBadge";
import ReturnToSearchButton from "./ReturnToSearchButton";
import TreeCanvas from "./TreeCanvas";
import { TreeIlalTab, TreeMatnTab } from "./TreeTabs";

/** The single-hadith page: the hadith's isnad tree, with its text and its findings in the side panel. */
export default function TreeWorkspace({ treeData, hadithId }: { treeData: IsnadTreeResponseDto; hadithId: string }) {
  const openTab = useWorkspaceStore((s) => s.openTab);
  const takhreej = useAutoTakhreej();
  const [showNote, setShowNote] = useState(false);

  const tabs: WorkspaceTab[] = [
    { id: "mutun", label: "المتن", Icon: BookOpen, render: () => <TreeMatnTab tree={treeData} /> },
    {
      id: "ilal",
      label: "العلل",
      Icon: ShieldAlert,
      badge: <IlalTabBadge />,
      render: (active) => <TreeIlalTab hadithId={hadithId} active={active} />,
    },
  ];

  return (
    <HadithWorkspace
      header={
        <header className="bg-surface border-b border-line px-4 sm:px-6 py-3 flex flex-col sm:flex-row sm:items-start justify-between gap-3 z-10 shadow-sm relative shrink-0">
          <div className="max-w-3xl min-w-0">
            <h1 className="text-lg sm:text-xl font-bold text-brand-dark mb-1">
              <Link href={bookHref(treeData.bookName)} className="hover:underline" title={`أبواب ${treeData.bookName}`}>
                {treeData.bookName}
              </Link>{" "}
              - حديث رقم {treeData.hadithNumber}
            </h1>
            <ClampedText text={treeData.matnArabic} className="text-ink-muted font-arabic text-base sm:text-lg leading-relaxed" />
            <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-ink-muted">
              <span className="font-semibold">حكم الحديث:</span>
              <EvidenceBadge status="insufficient" />
              {/* On a phone the note starts collapsed; from sm up it is always shown. */}
              <button
                type="button"
                onClick={() => setShowNote((v) => !v)}
                aria-expanded={showNote}
                aria-controls="ruling-note"
                className="sm:hidden inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-semibold text-brand-blue hover:bg-slate-100 cursor-pointer"
              >
                <Info className="h-3.5 w-3.5" aria-hidden />
                {showNote ? "إخفاء" : "تفاصيل"}
              </button>
              <span id="ruling-note" className={showNote ? "" : "hidden sm:inline"}>
                لا يحمل المشروع حكماً معتمداً لهذا الحديث؛ للتحقق راجع{" "}
                <a href="https://dorar.net/hadith" target="_blank" rel="noopener noreferrer" className="font-semibold text-indigo-700 underline">
                  الدرر السنية
                </a>
                .
              </span>
            </div>
            {takhreej.error && (
              <p role="alert" className="mt-2 text-xs font-semibold text-rose-700">
                {takhreej.error.message}
              </p>
            )}
          </div>

          <div className="flex items-center gap-2 shrink-0 flex-wrap">
            <button
              type="button"
              onClick={() => takhreej.start(hadithId)}
              disabled={takhreej.loadingId !== null}
              title="يجمع روايات الحديث في الكتب الأخرى ويرسمها في شجرة واحدة"
              className="flex items-center gap-2 rounded-lg bg-brand-blue px-3 py-2 text-sm font-bold text-white shadow-sm transition-colors hover:bg-brand-dark disabled:opacity-60 cursor-pointer"
            >
              {takhreej.loadingId ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <GitCompareArrows className="h-4 w-4 text-brand-teal" aria-hidden />}
              التخريج المقارن
            </button>
            <button
              type="button"
              onClick={() => openTab("ilal")}
              aria-haspopup="dialog"
              className="flex items-center gap-2 rounded-lg border border-brand-blue/30 bg-surface px-3 py-2 text-sm font-semibold text-brand-blue shadow-sm transition-colors hover:bg-brand-blue hover:text-white cursor-pointer"
            >
              <ShieldQuestion className="h-4 w-4" aria-hidden />
              فحص العلل
              <IlalTabBadge />
            </button>
            {/* A phone has no room for a third labelled button: the arrow alone there. */}
            <span className="sm:hidden">
              <ReturnToSearchButton variant="icon" label="عودة للبحث" />
            </span>
            <span className="hidden sm:block">
              <ReturnToSearchButton variant="button" label="عودة للبحث" />
            </span>
          </div>
        </header>
      }
      canvas={<TreeCanvas treeData={treeData} />}
      tabs={tabs}
      defaultTab="mutun"
      panelStartsOpen="never"
      panelLabel="المتن والعلل"
    />
  );
}
