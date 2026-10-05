import Link from "next/link";
import { BookOpen, Library, ScrollText, Search, ShieldCheck } from "lucide-react";
import Logo, { LogoMark } from "./Logo";
import NavLink from "./NavLink";

const NAV_ITEMS = [
  { href: "/search", short: "البحث", long: "البحث والتخريج", Icon: Search },
  { href: "/books", short: "الدواوين", long: "فهرس الدواوين", Icon: BookOpen },
  { href: "/verify", short: "التحقق", long: "تحقق من حديث", Icon: ShieldCheck },
  { href: "/glossary", short: "المصطلحات", long: "المصطلحات", Icon: ScrollText },
  { href: "/sources", short: "المصادر", long: "المصادر والمنهج", Icon: Library },
];

interface SiteHeaderProps {
  /**
   * "default": sticky bar for the content pages.
   * "compact": a thin, non-sticky bar for the full-height graph screens (tree, takhreej).
   */
  variant?: "default" | "compact";
  /** Page-specific status shown before the navigation (e.g. the takhreej selection count on search). */
  children?: React.ReactNode;
}

/** The one site header: logo and the four main sections, on every page. */
export default function SiteHeader({ variant = "default", children }: SiteHeaderProps) {
  const isCompact = variant === "compact";

  return (
    <header
      className={
        isCompact
          ? "shrink-0 z-30 bg-surface border-b border-line"
          : "sticky top-0 z-30 bg-surface/90 backdrop-blur-md border-b border-line/80"
      }
    >
      <div
        className={`mx-auto flex items-center justify-between gap-2 px-3 sm:px-6 ${
          isCompact ? "h-12" : "max-w-7xl h-14 sm:h-16"
        }`}
      >
        <Link href="/" className="group flex min-w-0 items-center gap-2.5" aria-label="شجرة الأسانيد الذكية: الرئيسية">
          <span className="transition-transform group-hover:scale-105">
            <LogoMark size={isCompact ? 32 : 36} />
          </span>
          <Logo
            variant="wordmark"
            size={isCompact ? 32 : 36}
            className="hidden lg:inline whitespace-nowrap"
          />
        </Link>

        <div className="flex min-w-0 items-center gap-2">
          {children}
          <nav aria-label="التنقل الرئيسي" className="flex items-center gap-0.5 sm:gap-1">
            {NAV_ITEMS.map(({ href, short, long, Icon }) => (
              <NavLink
                key={href}
                href={href}
                className="flex flex-col items-center gap-0.5 rounded-lg px-1 min-[360px]:px-2 py-1 text-[11px] font-semibold text-ink-muted transition-colors hover:bg-slate-100 hover:text-brand-dark sm:flex-row sm:gap-1.5 sm:px-3 sm:py-1.5 sm:text-sm"
                activeClassName="bg-brand-blue/10 text-brand-blue"
              >
                <Icon className="h-4 w-4 shrink-0 text-brand-teal-ink" aria-hidden />
                <span className="whitespace-nowrap lg:hidden">{short}</span>
                <span className="hidden whitespace-nowrap lg:inline">{long}</span>
              </NavLink>
            ))}
          </nav>
        </div>
      </div>
    </header>
  );
}
