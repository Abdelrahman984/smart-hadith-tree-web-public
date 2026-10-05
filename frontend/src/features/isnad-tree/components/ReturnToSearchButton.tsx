"use client";

import { useRouter } from "next/navigation";
import { ArrowRight } from "lucide-react";

interface ReturnToSearchButtonProps {
  label?: string;
  variant?: "icon" | "button";
  className?: string;
}

export default function ReturnToSearchButton({
  label = "العودة للبحث",
  variant = "icon",
  className = "",
}: ReturnToSearchButtonProps) {
  const router = useRouter();

  const handleReturn = () => {
    // If user has previous browser history in our app, go back to restore full search state
    if (typeof window !== "undefined" && window.history.length > 1) {
      router.back();
    } else {
      router.push("/search");
    }
  };

  if (variant === "button") {
    return (
      <button
        type="button"
        onClick={handleReturn}
        className={`flex items-center gap-2 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl font-bold text-xs sm:text-sm transition-all cursor-pointer shadow-2xs hover:shadow-xs shrink-0 ${className}`}
        title={label}
      >
        <ArrowRight className="w-4 h-4 text-ink-muted" />
        <span>{label}</span>
      </button>
    );
  }

  return (
    <button
      type="button"
      onClick={handleReturn}
      className={`p-2 hover:bg-slate-100 text-ink-muted hover:text-slate-900 rounded-xl transition-colors cursor-pointer flex items-center justify-center ${className}`}
      title={label}
      aria-label={label}
    >
      <ArrowRight className="w-5 h-5" />
    </button>
  );
}
