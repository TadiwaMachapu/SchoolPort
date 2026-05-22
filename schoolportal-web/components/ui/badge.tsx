import * as React from "react";
import { cn } from "@/lib/utils";

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "default" | "success" | "warning" | "destructive" | "outline";
}

const variants = {
  default:     "bg-blue-50 text-blue-700 ring-1 ring-blue-200/60",
  success:     "bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/60",
  warning:     "bg-amber-50 text-amber-700 ring-1 ring-amber-200/60",
  destructive: "bg-rose-50 text-rose-700 ring-1 ring-rose-200/60",
  outline:     "bg-white text-gray-600 ring-1 ring-gray-200",
};

export function Badge({ className, variant = "default", ...props }: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium",
        variants[variant],
        className
      )}
      {...props}
    />
  );
}
