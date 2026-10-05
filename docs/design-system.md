# Design system: logo and theme

The frontend is light-only. Colours are named by role in `frontend/src/app/globals.css` (`@theme`); use the role, not a raw Tailwind colour.

## Logo

- **Mark**: an isnad tree on a navy square. A teal root (the compiler) branches into white narrator nodes. Component: `frontend/src/components/Logo.tsx` (`variant`: `mark`, `wordmark`, `lockup`; `onDark` for the footer).
- **Browser icons**: `frontend/src/app/icon.svg` (same drawing), `apple-icon.png` (180 px, square corners; iOS rounds it) and `favicon.ico` (16/32/48 px). If you change the mark, change `Logo.tsx` and `icon.svg` together and re-export the two raster files.
- Do not use the `Network` icon from lucide as the logo; it is the "single chain" action icon.
- Theme colour of the browser UI on phones: `#1A3A5C` (`viewport.themeColor` in `layout.tsx`).

## Colour tokens

| Token | Value | Use |
|---|---|---|
| `brand-blue` | `#1A3A5C` | Primary buttons, the logo square, active states |
| `brand-dark` | `#0F243A` | Headings, the footer background |
| `brand-teal` | `#00C2CB` | Fills, rings and icons on **dark** backgrounds (footer, advanced-search header, buttons on `brand-blue`) |
| `brand-teal-ink` | `#0B7A83` | Teal **text and icons on light** surfaces. 5.1:1 on white; `brand-teal` is only 2.2:1 there |
| `surface` / `surface-muted` | `#ffffff` / `#f8fafc` | Cards and panels / the page background |
| `line` | `#e2e8f0` | Borders and dividers |
| `ink` / `ink-muted` / `ink-subtle` | `#1e293b` / `#475569` / `#64748b` | Body, secondary and tertiary text (all at least 4.5:1 on `surface`) |

Status colours (rose, amber, emerald) stay as Tailwind classes. Narrator grade colours live in one map, `features/narrator-details/utils/gradeStyle.ts`, used by the graph nodes, the legend, the narrator drawer and the sources page.

## Rules of thumb

- Teal as text on a light background: `text-brand-teal-ink`, never `text-brand-teal`.
- Text on `brand-dark` / `brand-blue`: `text-white` or `text-slate-300` and lighter. `slate-400` and lighter greys on white (placeholders, some icons) are below 4.5:1 and are a known follow-up.
- A dark theme is not built. The role tokens are the place to add one: define dark values for `surface`, `surface-muted`, `line` and `ink*`, then handle the graph (React Flow node colours) and the grade badges.
