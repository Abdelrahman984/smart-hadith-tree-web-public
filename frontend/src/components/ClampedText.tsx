"use client";

import { useState } from "react";
import { ChevronDown, ChevronUp } from "lucide-react";

/** Long text clamped to a few lines with a "show all" toggle. */
export default function ClampedText({ text, className = "", threshold = 220 }: { text: string; className?: string; threshold?: number }) {
  const [isExpanded, setIsExpanded] = useState(false);
  const isLong = text.length > threshold;

  return (
    <div>
      <p className={`${className} ${isLong && !isExpanded ? "line-clamp-2 sm:line-clamp-3" : ""}`}>{text}</p>
      {isLong && (
        <button
          type="button"
          onClick={() => setIsExpanded((v) => !v)}
          aria-expanded={isExpanded}
          className="mt-1 inline-flex items-center gap-1 text-xs font-bold text-brand-blue hover:underline cursor-pointer"
        >
          {isExpanded ? (
            <>
              <span>عرض أقل</span>
              <ChevronUp className="h-3.5 w-3.5" aria-hidden />
            </>
          ) : (
            <>
              <span>عرض المتن كاملاً</span>
              <ChevronDown className="h-3.5 w-3.5" aria-hidden />
            </>
          )}
        </button>
      )}
    </div>
  );
}
