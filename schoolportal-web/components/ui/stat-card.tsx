import * as React from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

type StatCardColor = "neutral" | "blue" | "green" | "purple" | "orange" | "red" | "teal";

// Token-backed.
// - "neutral" is the DEFAULT for a plain count (My classes, Assignments…): surface-subtle
//   grey, NOT a primary tint. Primary tint means "this is branded/primary", not "this is a
//   container"; and since primary-100 === success-100 (#EAF3DE), tinting neutrals with
//   primary makes success unreadable. See CLAUDE.md "Neutral UI uses surface-subtle".
// - status colours (green/orange/red) are ONLY for genuine state (0 needs-grading = success,
//   overdue = danger) — never decorative. See CLAUDE.md "status colour never decorative".
// NOTE: purple→secondary(coral) and teal→primary — the palette has no violet/teal token
// family, so those two keys intentionally collapse onto brand tokens. Flagged for review.
const colorMap: Record<StatCardColor, { bg: string; icon: string }> = {
  neutral: { bg: "bg-surface-subtle", icon: "text-text-secondary" },
  blue:   { bg: "bg-primary-100",   icon: "text-primary-700" },
  green:  { bg: "bg-success-100",   icon: "text-success-700" },
  purple: { bg: "bg-secondary-100", icon: "text-secondary-700" },
  orange: { bg: "bg-warning-100",   icon: "text-warning-700" },
  red:    { bg: "bg-danger-100",    icon: "text-danger-700" },
  teal:   { bg: "bg-primary-100",   icon: "text-primary-700" },
};

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: string | number;
  color: StatCardColor;
  trend?: string;
  className?: string;
}

export function StatCard({ icon: Icon, label, value, color, trend, className }: StatCardProps) {
  const { bg, icon } = colorMap[color];
  return (
    <div className={cn(
      "rounded-lg bg-surface-card shadow-card p-4",
      className
    )}>
      <div className="flex items-start gap-3">
        <div className={cn("h-10 w-10 rounded-md flex items-center justify-center shrink-0", bg)}>
          <Icon className={cn("h-5 w-5", icon)} />
        </div>
        <div className="min-w-0">
          <p className="text-2xl font-bold text-text-primary leading-tight">{value}</p>
          <p className="text-xs text-text-secondary mt-0.5">{label}</p>
          {trend && (
            <p className="text-[11px] text-text-secondary mt-0.5">{trend}</p>
          )}
        </div>
      </div>
    </div>
  );
}
