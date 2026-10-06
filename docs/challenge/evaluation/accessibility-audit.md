# Accessibility audit

Scope: WCAG 2.1 A/AA and best-practice rules via axe-core 4, plus manual keyboard and viewport checks,
run against the production build in Chromium.

## What was checked

| Check | Pages | Result |
|---|---|---|
| axe-core (wcag2a, wcag2aa, wcag21a, wcag21aa, best-practice) | `/`, `/search`, `/books`, `/glossary`, desktop 1280px and mobile 390px | **0 violations** after fixes |
| Horizontal overflow at 320 / 360 / 390 px | same pages | none after fixes |
| Skip link: first Tab stop, visible, jumps to `#main-content` | `/` | passes |
| Visible focus on links/buttons | `/glossary` | passes (outline on all tabbed items) |
| Advanced-search dialog: `role="dialog"`, labelled, closes on Escape | `/search` | passes |

## Found and fixed

| Issue | Fix |
|---|---|
| No `<main>` landmark on `/`, `/books`, `/glossary`, `/takhreej` | `<main id="main-content">` added |
| No skip link | added in `layout.tsx` (appears on keyboard focus) |
| Home header overflowed phones (up to 102px at 320px) after the glossary link was added | header wraps; shorter labels on small screens |
| Heading order skipped a level in the search empty state | `h3` → `h2` |
| Informative small text at `text-slate-400` (about 2.6:1 on white) | `text-slate-500` (about 4.8:1) |
| Narrator drawer close button had no accessible name | `aria-label` added |
| Narrator drawer and advanced-search modal: no dialog role, no Escape | `role="dialog"` + label; Escape closes both |
| Ilal finding cards (`role="button"`) responded to Enter only | Space also works; `aria-pressed` added |
| Home search input had only a placeholder | `aria-label` added |
| No reduced-motion handling | `prefers-reduced-motion` rule in `globals.css` |

## Glossary tooltip on touch (fixed)

`GlossaryTerm` used hover and focus only, which is unreliable on touch screens. It is now a `<button>` with
`aria-expanded`/`aria-controls`: tap, click and Enter/Space toggle it; mouse hover still previews it; Escape or a tap
outside closes it; it is nudged to stay inside the viewport; clicks and keys do not leak to a clickable parent (the Ilal
cards). While closed it is `display: none`, so it no longer adds horizontal overflow on narrow screens.
Verified in Chromium with touch emulation (360px) on a temporary test page (removed): tap opens/closes, outside tap
closes, left and right edge terms stay inside the viewport, a term inside a clickable card does not trigger the card,
Escape closes, desktop hover shows/hides, keyboard Enter opens. **Not** verified on a real iOS/Android device.

## Deferred by the project owner

**Text alternative for the isnad graph** ("عرض كقائمة": ordered chain from the Companion to the compiler, with the
transmission term and warnings as text, plus a "copy isnad" button). Not built yet; revisit after the higher-priority
items (live demo, video, measurement). Design sketch: one list item per link using the existing `transmissionTerm`,
`isAnomaly`/`anomalyReason` and Ilal findings, so no new data is needed.

## Not covered (needs a backend with data or a human)

- Tree, comparative (`/takhreej`), books-detail and chapter pages, the narrator drawer content, the Ilal panel and the
  glossary tooltips were only reviewed in code: they need the API and a database to render, so axe was not run on them.
- The React Flow canvases: keyboard operation and screen-reader semantics of nodes and edges are untested. The graph is
  inherently visual; a text alternative (e.g. the chain as an ordered list) would be the right fix and is not built.
- No focus trap in the dialogs (focus can leave the modal with Tab); focus is not returned to the trigger on close.
- Colour contrast for dynamic states (hover, selected, graph node/edge colours) and the remaining 31 uses of 9–10px text
  were not measured.
- No test with a real screen reader (NVDA / VoiceOver / TalkBack) and no test with users.

Re-run: build the frontend, `next start`, then run axe-core on each page (script used was a small Playwright + `axe-core` harness kept outside the repo).
