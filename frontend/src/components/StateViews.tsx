import { AlertCircle, Loader2, RotateCcw, SearchX } from "lucide-react";

/** Shared loading / error / empty blocks, so every screen reports its state the same way. */

export function LoadingState({ message = "جارٍ التحميل...", className = "" }: { message?: string; className?: string }) {
  return (
    <div role="status" className={`flex flex-col items-center justify-center gap-3 py-12 text-center text-ink-subtle ${className}`}>
      <Loader2 className="h-7 w-7 animate-spin text-brand-teal-ink" aria-hidden />
      <p className="text-sm font-medium">{message}</p>
    </div>
  );
}

interface ErrorStateProps {
  title?: string;
  message: string;
  onRetry?: () => void;
  /** Extra actions (e.g. a link back) shown next to the retry button. */
  children?: React.ReactNode;
  className?: string;
}

export function ErrorState({ title = "حدث خطأ", message, onRetry, children, className = "" }: ErrorStateProps) {
  return (
    <div role="alert" className={`mx-auto my-6 max-w-lg space-y-3 rounded-2xl border border-rose-200 bg-rose-50 p-6 text-center ${className}`}>
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-rose-100 text-rose-600">
        <AlertCircle className="h-6 w-6" aria-hidden />
      </div>
      <h2 className="text-base font-bold text-rose-900">{title}</h2>
      <p className="text-sm leading-relaxed text-rose-700">{message}</p>
      {(onRetry || children) && (
        <div className="flex flex-wrap items-center justify-center gap-2 pt-1">
          {onRetry && (
            <button
              type="button"
              onClick={onRetry}
              className="inline-flex items-center gap-1.5 rounded-xl bg-rose-600 px-4 py-2 text-xs font-bold text-white shadow-xs transition-colors hover:bg-rose-700 cursor-pointer"
            >
              <RotateCcw className="h-3.5 w-3.5" aria-hidden />
              <span>إعادة المحاولة</span>
            </button>
          )}
          {children}
        </div>
      )}
    </div>
  );
}

export function EmptyState({ title, message, children, className = "" }: { title: string; message?: string; children?: React.ReactNode; className?: string }) {
  return (
    <div className={`mx-auto my-6 max-w-lg space-y-3 rounded-2xl border border-line bg-surface p-8 text-center ${className}`}>
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-ink-subtle">
        <SearchX className="h-6 w-6" aria-hidden />
      </div>
      <h2 className="text-base font-bold text-ink">{title}</h2>
      {message && <p className="text-sm leading-relaxed text-ink-muted">{message}</p>}
      {children && <div className="flex flex-wrap items-center justify-center gap-2 pt-1">{children}</div>}
    </div>
  );
}
