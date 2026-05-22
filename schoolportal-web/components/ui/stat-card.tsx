import * as React from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

type StatCardColor = "blue" | "green" | "purple" | "orange" | "red" | "teal";

const colorMap: Record<StatCardColor, { bg: string; icon: string }> = {
  blue:   { bg: "bg-indigo-50",  icon: "text-indigo-600" },
  green:  { bg: "bg-emerald-50", icon: "text-emerald-600" },
  purple: { bg: "bg-violet-50",  icon: "text-violet-600" },
  orange: { bg: "bg-amber-50",   icon: "text-amber-600" },
  red:    { bg: "bg-rose-50",    icon: "text-rose-600" },
  teal:   { bg: "bg-teal-50",    icon: "text-teal-600" },
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
      "rounded-xl border border-gray-100 bg-white shadow-sm ring-1 ring-gray-100/50 p-4",
      className
    )}>
      <div className="flex items-start gap-3">
        <div className={cn("h-10 w-10 rounded-lg flex items-center justify-center shrink-0", bg)}>
          <Icon className={cn("h-5 w-5", icon)} />
        </div>
        <div className="min-w-0">
          <p className="text-2xl font-bold text-gray-900 leading-tight">{value}</p>
          <p className="text-xs text-gray-500 uppercase tracking-wide mt-0.5">{label}</p>
          {trend && (
            <p className="text-xs text-gray-400 mt-0.5">{trend}</p>
          )}
        </div>
      </div>
    </div>
  );
}
