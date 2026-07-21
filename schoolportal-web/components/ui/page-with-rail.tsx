import * as React from "react";

/**
 * Opt-in right-rail layout slot (Step 5). A page renders <PageWithRail rail={…}>
 * only on dashboard/overview screens; data-dense screens (gradebook, marks capture,
 * reports tables) simply DON'T use it and get full width — the rail defaults to off.
 *
 * Mobile: the rail stacks BELOW the main content (single column). At xl it sits to
 * the right at a fixed width.
 */
export function PageWithRail({ children, rail }: { children: React.ReactNode; rail: React.ReactNode }) {
  return (
    <div className="flex flex-col xl:flex-row gap-4 xl:gap-5">
      <div className="min-w-0 flex-1 order-1">{children}</div>
      <aside className="w-full xl:w-80 shrink-0 order-2 space-y-4">{rail}</aside>
    </div>
  );
}
