"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useMemo } from "react";
import { BookOpen, ShieldAlert } from "lucide-react";
import SiteHeader from "@/components/SiteHeader";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { useIlalStore } from "@/features/ilal/store/useIlalStore";
import ComparativeTreeCanvas from "@/features/isnad-tree/components/ComparativeTreeCanvas";
import HadithWorkspace, { type WorkspaceTab } from "@/features/isnad-tree/components/HadithWorkspace";
import IlalTabBadge from "@/features/isnad-tree/components/IlalTabBadge";
import ReturnToSearchButton from "@/features/isnad-tree/components/ReturnToSearchButton";
import SourceChips from "@/features/isnad-tree/components/SourceChips";
import TakhreejSummary from "@/features/isnad-tree/components/TakhreejSummary";
import { IlalTab, MatnTab } from "@/features/isnad-tree/components/TakhreejTabs";
import { useTakhreej } from "@/features/isnad-tree/hooks/useTakhreej";
import { groupSourcesByCompanion } from "@/features/isnad-tree/utils/groupSources";
import { takhreejTitle } from "@/features/isnad-tree/utils/takhreejTitle";
import { useGraphViewStore } from "@/features/isnad-tree/store/useGraphViewStore";
import { useWorkspaceStore } from "@/features/isnad-tree/store/useWorkspaceStore";
import { getBookMeta } from "@/lib/bookTheme";
import { isPhone } from "@/lib/viewport";
import type { ComparativeHadithSourceDto } from "@/types/api";

function TakhreejContent() {
  const searchParams = useSearchParams();
  const idsParam = searchParams.get("ids");

  const hadithIds = useMemo(() => {
    return idsParam ? idsParam.split(",").filter((id) => id.trim().length > 0) : [];
  }, [idsParam]);

  const { data, isLoading, isError, refetch } = useTakhreej(hadithIds);
  const { setReport, reset } = useIlalStore();
  const router = useRouter();
  const focusBook = useGraphViewStore((s) => s.focusBook);
  const setFocusBook = useGraphViewStore((s) => s.setFocusBook);
  const openTab = useWorkspaceStore((s) => s.openTab);

  // Share the ilal report with the canvas so it can decorate edges and highlight narrators.
  useEffect(() => {
    setReport(data?.ilalReport ?? null);
  }, [data, setReport]);

  useEffect(() => reset, [reset]);

  // The tab title says which hadith is compared (the page's static metadata cannot know).
  const title = data ? takhreejTitle(data.sources) : null;
  useEffect(() => {
    if (title) document.title = `${title} | شجرة الأسانيد الذكية`;
  }, [title]);

  // A chip highlights that book's routes; with room for the panel it also scrolls to the narration's matn.
  const handleSelectSource = (source: ComparativeHadithSourceDto) => {
    const book = getBookMeta(source.bookName).name;
    setFocusBook(focusBook === book ? null : book);
    if (isPhone()) return;
    openTab("mutun");
    requestAnimationFrame(() =>
      document.getElementById(`matn-card-${source.hadithId}`)?.scrollIntoView({ behavior: "smooth", block: "nearest" })
    );
  };

  const handleRemoveSource = (source: ComparativeHadithSourceDto) => {
    const rest = hadithIds.filter((id) => id !== source.hadithId);
    if (rest.length >= 2) router.replace(`/takhreej?ids=${rest.join(",")}`);
  };

  if (!idsParam || hadithIds.length < 2) {
    return (
      <StatusScreen>
        <EmptyState
          title="يرجى تحديد حديثين على الأقل للتخريج"
          message="حدد الروايات من صفحة البحث، أو استخدم «تخريج فوري» على أي حديث لجمع رواياته تلقائياً."
        >
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
        </EmptyState>
      </StatusScreen>
    );
  }

  if (isLoading) {
    return (
      <StatusScreen>
        <LoadingState message="جارٍ بناء شجرة التخريج المقارنة..." />
      </StatusScreen>
    );
  }

  if (isError || !data) {
    return (
      <StatusScreen>
        <ErrorState title="تعذر بناء شجرة التخريج" message="حدث خطأ أثناء جلب بيانات التخريج. تأكد من تشغيل الخادم ثم أعد المحاولة." onRetry={() => refetch()}>
          <ReturnToSearchButton variant="button" label="العودة للبحث" />
        </ErrorState>
      </StatusScreen>
    );
  }

  const groups = groupSourcesByCompanion(data);
  const shawahidIds = new Set(groups.shawahid.map((w) => w.source.hadithId));

  const tabs: WorkspaceTab[] = [
    { id: "mutun", label: "المتون", Icon: BookOpen, render: () => <MatnTab data={data} groups={groups} /> },
    { id: "ilal", label: "العلل", Icon: ShieldAlert, badge: <IlalTabBadge />, render: () => <IlalTab data={data} /> },
  ];

  return (
    <HadithWorkspace
      header={
        <header className="bg-surface border-b border-line px-3 py-2.5 sm:p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2 sm:gap-4 z-10 shrink-0 shadow-sm">
          <div className="flex items-center gap-3 shrink-0">
            <ReturnToSearchButton variant="button" label="العودة للبحث" />
            <h1 className="text-lg sm:text-xl font-bold text-ink">شجرة التخريج المقارنة</h1>
          </div>
          <SourceChips sources={data.sources} focusBook={focusBook} shawahidIds={shawahidIds} onSelect={handleSelectSource} onRemove={handleRemoveSource} />
        </header>
      }
      summary={<TakhreejSummary data={data} onOpenIlal={() => openTab("ilal")} shawahidCount={groups.shawahid.length} />}
      canvas={<ComparativeTreeCanvas treeData={data} />}
      tabs={tabs}
      defaultTab="mutun"
      panelStartsOpen="desktop"
      panelLabel="المتون"
      bookFocus
    />
  );
}

/** Full-height page with the site header, for the empty / loading / error states. */
function StatusScreen({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen flex-col">
      <SiteHeader variant="compact" />
      <main id="main-content" className="flex flex-1 items-center justify-center p-4">
        {children}
      </main>
    </div>
  );
}

export default function TakhreejPageClient() {
  return (
    <Suspense fallback={<StatusScreen><LoadingState /></StatusScreen>}>
      <TakhreejContent />
    </Suspense>
  );
}
