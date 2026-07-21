import * as React from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface EmptyStateProps {
  icon: LucideIcon;
  heading: string;
  body?: string;
  /**
   * "neutral"  → primary tint (default; nothing here yet / no data)
   * "positive" → success tint + copy is GOOD NEWS (e.g. zero at-risk learners).
   *   An empty at-risk list means the grade is healthy — colour it that way.
   */
  tone?: "neutral" | "positive";
  /**
   * "default" → large treatment for FULL-PAGE empties (96px circle, generous padding).
   * "compact" → for IN-CARD empties (small circle, tight padding) so a card empty
   *   state doesn't balloon a dashboard tile to full-page height.
   */
  size?: "default" | "compact";
  action?: React.ReactNode;
  className?: string;
}

const tones = {
  neutral:  { circle: "bg-primary-100", icon: "text-primary-600" },
  positive: { circle: "bg-success-100", icon: "text-success-700" },
};

const sizes = {
  default: { wrap: "px-6 py-12", circle: "h-24 w-24", icon: "h-14 w-14", heading: "mt-5 text-sm" },
  compact: { wrap: "px-4 py-6",  circle: "h-12 w-12", icon: "h-6 w-6",   heading: "mt-3 text-[13px]" },
};

export function EmptyState({ icon: Icon, heading, body, tone = "neutral", size = "default", action, className }: EmptyStateProps) {
  const t = tones[tone];
  const s = sizes[size];
  return (
    <div className={cn("flex flex-col items-center justify-center text-center", s.wrap, className)}>
      <div className={cn("flex items-center justify-center rounded-full", s.circle, t.circle)}>
        <Icon className={cn(s.icon, t.icon)} strokeWidth={1.5} />
      </div>
      <h3 className={cn("font-semibold text-text-primary", s.heading)}>{heading}</h3>
      {body && <p className="mt-1 max-w-sm text-xs text-text-secondary">{body}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
