"use client";

import { getNodesBounds, getViewportForBounds, Panel, useReactFlow } from "@xyflow/react";
import { toPng } from "html-to-image";
import { Download, Eye, EyeOff, Layers, Map as MapIcon, Rows3, Search, X, ZoomIn, ZoomOut, Maximize } from "lucide-react";
import { useId, useMemo, useState, type ReactNode } from "react";
import { isCompactDensity, useGraphViewStore } from "../store/useGraphViewStore";
import { searchNarrators, type NarratorSearchItem } from "../utils/narratorSearch";

interface GraphToolbarProps {
  /** Every narrator on the graph, for the find box. */
  narrators: NarratorSearchItem[];
  /** Fit the whole graph (the canvas knows how much room the legend takes). */
  onFit: () => void;
  /** Whether the minimap is showing, and the choice that would flip it. */
  minimapOn: boolean;
  /** The legend is open. */
  legendOn: boolean;
}

/** One bar for everything you do to the graph itself: find, highlight the weak, detail, minimap, legend, zoom, export. */
export default function GraphToolbar({ narrators, onFit, minimapOn, legendOn }: GraphToolbarProps) {
  const { getNodes, zoomIn, zoomOut } = useReactFlow();
  const showWeakOnly = useGraphViewStore((s) => s.showWeakOnly);
  const setShowWeakOnly = useGraphViewStore((s) => s.setShowWeakOnly);
  const compact = useGraphViewStore((s) => isCompactDensity(s.density, s.nodeCount));
  const setDensity = useGraphViewStore((s) => s.setDensity);
  const setMinimap = useGraphViewStore((s) => s.setMinimap);
  const setLegendOpen = useGraphViewStore((s) => s.setLegendOpen);
  const [findOpen, setFindOpen] = useState(false);

  const handleDownload = async () => {
    const viewport = document.querySelector<HTMLElement>(".react-flow__viewport");
    const nodes = getNodes();
    if (!viewport || nodes.length === 0) return;

    // Render the whole graph at 1:1 into an image sized to its bounds (plus a margin), regardless of the current pan/zoom.
    const margin = 100;
    const bounds = getNodesBounds(nodes);
    const width = Math.ceil(bounds.width + margin * 2);
    const height = Math.ceil(bounds.height + margin * 2);
    const { x, y, zoom } = getViewportForBounds(bounds, width, height, 1, 1, margin / Math.max(width, height));

    const options = {
      backgroundColor: "#f8fafc",
      width,
      height,
      pixelRatio: 2,
      style: { width: `${width}px`, height: `${height}px`, transform: `translate(${x}px, ${y}px) scale(${zoom})` },
    };

    let dataUrl: string;
    try {
      dataUrl = await toPng(viewport, options);
    } catch {
      // Cross-origin web-font stylesheets can make font embedding throw; the image is still better without them than none.
      dataUrl = await toPng(viewport, { ...options, skipFonts: true });
    }

    const a = document.createElement("a");
    a.setAttribute("download", "isnad-tree.png");
    a.setAttribute("href", dataUrl);
    a.click();
  };

  return (
    <Panel position="top-left" className="m-2 flex max-w-[calc(100vw-1rem)] flex-col gap-2" dir="rtl">
      <div
        role="toolbar"
        aria-label="أدوات الشجرة"
        className="flex items-center gap-0.5 overflow-x-auto rounded-xl border border-line bg-surface/95 p-1 shadow-md backdrop-blur"
      >
        <ToolButton label="بحث عن راوٍ" pressed={findOpen} onClick={() => setFindOpen((v) => !v)}>
          <Search className="h-4 w-4" aria-hidden />
        </ToolButton>
        <ToolButton
          label={showWeakOnly ? "إظهار الجميع" : "إبراز الضعفاء"}
          title="تظليل الرواة الثقات وغير المقيَّمين وإبراز الضعفاء"
          pressed={showWeakOnly}
          onClick={() => setShowWeakOnly(!showWeakOnly)}
        >
          {showWeakOnly ? <EyeOff className="h-4 w-4" aria-hidden /> : <Eye className="h-4 w-4" aria-hidden />}
        </ToolButton>
        <ToolButton
          label={compact ? "عرض مفصّل للرواة" : "عرض مضغوط للرواة"}
          title={compact ? "البطاقات مضغوطة: الاسم والرتبة فقط. اضغط للتفصيل" : "اضغط لتصغير البطاقات إلى الاسم والرتبة"}
          pressed={compact}
          onClick={() => setDensity(compact ? "detailed" : "compact")}
        >
          <Rows3 className="h-4 w-4" aria-hidden />
        </ToolButton>
        <ToolButton label="خريطة الشجرة" pressed={minimapOn} onClick={() => setMinimap(!minimapOn)}>
          <MapIcon className="h-4 w-4" aria-hidden />
        </ToolButton>
        <ToolButton label="مفتاح الرموز" pressed={legendOn} onClick={() => setLegendOpen(!legendOn)}>
          <Layers className="h-4 w-4" aria-hidden />
        </ToolButton>
        <span className="mx-1 h-5 w-px shrink-0 bg-line" aria-hidden />
        <ToolButton label="تكبير" onClick={() => zoomIn({ duration: 150 })}>
          <ZoomIn className="h-4 w-4" aria-hidden />
        </ToolButton>
        <ToolButton label="تصغير" onClick={() => zoomOut({ duration: 150 })}>
          <ZoomOut className="h-4 w-4" aria-hidden />
        </ToolButton>
        <ToolButton label="ملاءمة الشجرة للشاشة" onClick={onFit}>
          <Maximize className="h-4 w-4" aria-hidden />
        </ToolButton>
        <span className="mx-1 h-5 w-px shrink-0 bg-line" aria-hidden />
        <ToolButton label="تصدير صورة" onClick={handleDownload}>
          <Download className="h-4 w-4" aria-hidden />
        </ToolButton>
      </div>

      {findOpen && <FindNarrator narrators={narrators} onDone={() => setFindOpen(false)} />}
    </Panel>
  );
}

function ToolButton({
  label,
  title,
  pressed,
  onClick,
  children,
}: {
  label: string;
  title?: string;
  /** Present for toggles. */
  pressed?: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={title ?? label}
      aria-pressed={pressed}
      className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition-colors cursor-pointer ${
        pressed ? "bg-brand-blue/10 text-brand-blue" : "text-ink-muted hover:bg-slate-100 hover:text-ink"
      }`}
    >
      {children}
    </button>
  );
}

/** A box that lists the graph's narrators as you type; choosing one selects it and brings it into view. */
function FindNarrator({ narrators, onDone }: { narrators: NarratorSearchItem[]; onDone: () => void }) {
  const revealNodes = useGraphViewStore((s) => s.revealNodes);
  const listId = useId();
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const results = useMemo(() => searchNarrators(narrators, query), [narrators, query]);

  const pick = (item: NarratorSearchItem | undefined) => {
    if (!item) return;
    revealNodes([item.id]);
    onDone();
  };

  return (
    <div className="w-72 max-w-full rounded-xl border border-line bg-surface p-2 shadow-lg">
      <div className="flex items-center gap-1.5 rounded-lg border border-line px-2 focus-within:border-brand-teal">
        <Search className="h-4 w-4 shrink-0 text-ink-subtle" aria-hidden />
        <input
          autoFocus
          type="search"
          role="combobox"
          aria-expanded={results.length > 0}
          aria-controls={listId}
          aria-activedescendant={results[active] ? `${listId}-${active}` : undefined}
          aria-label="ابحث عن راوٍ في الشجرة"
          placeholder="ابحث عن راوٍ في الشجرة…"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setActive(0);
          }}
          onKeyDown={(e) => {
            if (e.key === "ArrowDown") setActive((i) => Math.min(i + 1, results.length - 1));
            else if (e.key === "ArrowUp") setActive((i) => Math.max(i - 1, 0));
            else if (e.key === "Enter") pick(results[active]);
            else if (e.key === "Escape") onDone();
            else return;
            e.preventDefault();
          }}
          className="min-w-0 flex-1 bg-transparent py-1.5 text-sm text-ink outline-none placeholder:text-ink-subtle"
        />
        <button type="button" onClick={onDone} aria-label="إغلاق البحث" className="rounded p-0.5 text-ink-subtle hover:bg-slate-100 cursor-pointer">
          <X className="h-3.5 w-3.5" aria-hidden />
        </button>
      </div>

      {query.trim() !== "" && (
        <ul id={listId} role="listbox" aria-label="الرواة المطابقون" className="mt-1.5 max-h-64 overflow-y-auto">
          {results.length === 0 && <li className="px-2 py-2 text-xs text-ink-subtle">لا راوٍ بهذا الاسم في هذه الشجرة.</li>}
          {results.map((item, i) => (
            <li
              key={item.id}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              onMouseEnter={() => setActive(i)}
              onClick={() => pick(item)}
              className={`flex cursor-pointer items-baseline justify-between gap-2 rounded-lg px-2 py-1.5 text-sm ${i === active ? "bg-brand-blue/10" : ""}`}
            >
              <span className="font-semibold text-ink">{item.name}</span>
              <span className="shrink-0 text-[11px] text-ink-subtle">{[item.grade, item.tier].filter(Boolean).join(" · ")}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
