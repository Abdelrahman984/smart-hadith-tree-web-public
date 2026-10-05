/** True below Tailwind's `md` breakpoint, where side panels cover the graph instead of sitting beside it. */
export const isPhone = () => typeof window !== "undefined" && window.matchMedia("(max-width: 767px)").matches;
