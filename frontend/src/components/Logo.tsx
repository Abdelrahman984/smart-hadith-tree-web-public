interface LogoProps {
  /** "mark": the symbol only. "wordmark": the site name only. "lockup": the symbol with the name beside it. */
  variant?: "mark" | "wordmark" | "lockup";
  /** Side of the square mark in px; the wordmark scales with it. */
  size?: number;
  /** Wordmark colours for a dark background (the footer). */
  onDark?: boolean;
  className?: string;
}

/**
 * The project mark: an isnad tree. A teal root (the compiler) branches into narrators, drawn as a chain of nodes
 * on a navy square. Keep it in sync with `src/app/icon.svg`, which is the same drawing for the browser tab.
 */
export function LogoMark({ size = 36, className = "" }: { size?: number; className?: string }) {
  return (
    <svg
      viewBox="0 0 64 64"
      width={size}
      height={size}
      aria-hidden
      focusable="false"
      className={`shrink-0 ${className}`}
    >
      <rect width="64" height="64" rx="15" fill="#1A3A5C" />
      <g stroke="#ffffff" strokeOpacity="0.85" strokeWidth="2.6" strokeLinecap="round" fill="none">
        <path d="M32 15 L19 31 M32 15 L45 31" />
        <path d="M19 31 L11 49 M19 31 L27 49 M45 31 L45 49" />
      </g>
      <circle cx="32" cy="14" r="6" fill="#00C2CB" />
      <circle cx="19" cy="31" r="4.6" fill="#ffffff" />
      <circle cx="45" cy="31" r="4.6" fill="#ffffff" />
      <circle cx="11" cy="50" r="4" fill="#ffffff" />
      <circle cx="27" cy="50" r="4" fill="#ffffff" />
      <circle cx="45" cy="50" r="4" fill="#ffffff" />
    </svg>
  );
}

export default function Logo({ variant = "mark", size = 36, onDark = false, className = "" }: LogoProps) {
  if (variant === "mark") return <LogoMark size={size} className={className} />;

  const wordmark = (
    <span
      className={`font-bold leading-tight ${onDark ? "text-white" : "text-brand-dark"}`}
      style={{ fontSize: Math.round(size * 0.46) }}
    >
      شجرة الأسانيد <span className={onDark ? "text-brand-teal" : "text-brand-teal-ink"}>الذكية</span>
    </span>
  );
  if (variant === "wordmark") return <span className={className}>{wordmark}</span>;

  return (
    <span className={`inline-flex items-center gap-2.5 ${className}`}>
      <LogoMark size={size} />
      {wordmark}
    </span>
  );
}
