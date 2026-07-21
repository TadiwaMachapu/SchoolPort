import * as React from "react";
import { cn } from "@/lib/utils";

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: "default" | "success" | "warning" | "destructive" | "outline";
}

const variants = {
  default:     "bg-primary-100 text-primary-800",
  success:     "bg-success-100 text-success-700",
  warning:     "bg-warning-100 text-warning-700",
  destructive: "bg-danger-100 text-danger-700",
  outline:     "bg-surface-subtle text-text-secondary",
};

export function Badge({ className, variant = "default", ...props }: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-pill px-2.5 py-0.5 text-[11px] font-medium",
        variants[variant],
        className
      )}
      {...props}
    />
  );
}
