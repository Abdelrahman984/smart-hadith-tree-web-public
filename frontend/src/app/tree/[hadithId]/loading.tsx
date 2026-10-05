import SiteHeader from "@/components/SiteHeader";
import { LoadingState } from "@/components/StateViews";

export default function TreeLoading() {
  return (
    <div className="flex h-screen flex-col overflow-hidden">
      <SiteHeader variant="compact" />
      <div className="shrink-0 border-b border-line bg-surface px-4 py-3 sm:px-6" aria-hidden>
        <div className="h-6 w-56 animate-pulse rounded bg-slate-200" />
        <div className="mt-2 h-4 w-full max-w-2xl animate-pulse rounded bg-slate-100" />
        <div className="mt-1.5 h-4 w-2/3 max-w-xl animate-pulse rounded bg-slate-100" />
      </div>
      <main id="main-content" className="flex flex-1 items-center justify-center bg-surface-muted">
        <LoadingState message="جارٍ رسم شجرة الإسناد..." />
      </main>
    </div>
  );
}
