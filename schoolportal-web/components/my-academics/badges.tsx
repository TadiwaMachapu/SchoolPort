import { cn, getCapsCode } from "@/lib/utils";

// Step 8 — colour language for the My Academics page. CAPS codes and task-type badges must read
// instantly without a legend (per design brief), so colours are mapped directly here.

const CAPS_STYLES: Record<number, string> = {
  1: "bg-rose-50 text-rose-700 ring-rose-200/70",
  2: "bg-rose-50 text-rose-700 ring-rose-200/70",
  3: "bg-amber-50 text-amber-700 ring-amber-200/70",
  4: "bg-amber-50 text-amber-700 ring-amber-200/70",
  5: "bg-emerald-50 text-emerald-700 ring-emerald-200/70",
  6: "bg-emerald-50 text-emerald-700 ring-emerald-200/70",
  7: "bg-teal-100 text-teal-800 ring-teal-300/80", // distinct "outstanding"
};

/** CAPS 1–7 badge derived from a percentage. `compact` shows just the number (for dense rows). */
export function CapsBadge({ percentage, compact = false, className }: { percentage: number; compact?: boolean; className?: string }) {
  const code = getCapsCode(percentage);
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold ring-1 whitespace-nowrap",
        CAPS_STYLES[code],
        className,
      )}
      title={`CAPS code ${code}`}
    >
      {compact ? code : `Code ${code}`}
    </span>
  );
}

const TYPE_STYLES: Record<string, string> = {
  Test:       "bg-blue-50 text-blue-700 ring-blue-200/70",
  Assignment: "bg-purple-50 text-purple-700 ring-purple-200/70",
  Quiz:       "bg-teal-50 text-teal-700 ring-teal-200/70",
  Project:    "bg-orange-50 text-orange-700 ring-orange-200/70",
  Practical:  "bg-yellow-50 text-yellow-700 ring-yellow-200/70",
  Exam:       "bg-rose-50 text-rose-700 ring-rose-200/70",
};

/** Coloured task-type badge (Test/Assignment/Quiz/Project/Practical/Exam). */
export function TypeBadge({ type, className }: { type: string; className?: string }) {
  const style = TYPE_STYLES[type] ?? "bg-gray-50 text-gray-600 ring-gray-200/70";
  return (
    <span className={cn("inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 whitespace-nowrap", style, className)}>
      {type}
    </span>
  );
}

/** Text colour for a percentage: 70+ green, 50–69 amber, below 50 red. */
export function percentColor(percent: number): string {
  if (percent >= 70) return "text-emerald-600";
  if (percent >= 50) return "text-amber-600";
  return "text-rose-600";
}
