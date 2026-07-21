"use client";
import * as React from "react";
import { cn } from "@/lib/utils";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "default" | "secondary" | "outline" | "ghost" | "destructive";
  size?: "default" | "sm" | "lg";
  loading?: boolean;
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = "default", size = "default", loading, children, disabled, ...props }, ref) => {
    const base =
      "inline-flex items-center justify-center rounded-pill font-medium transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1 disabled:opacity-50 disabled:pointer-events-none";
    const variants = {
      default:     "bg-primary text-white hover:brightness-95",
      secondary:   "bg-primary-100 text-primary-800 hover:bg-primary-200",
      outline:     "border border-border bg-surface-card text-text-secondary hover:bg-surface-subtle",
      ghost:       "text-text-secondary hover:bg-surface-subtle",
      destructive: "bg-danger-500 text-white hover:brightness-95",
    };
    const sizes = {
      default: "h-9 py-2 px-4 text-[13px]",
      sm:      "h-8 px-3 text-xs",
      lg:      "h-11 px-6 text-sm",
    };
    return (
      <button
        ref={ref}
        className={cn(base, variants[variant], sizes[size], className)}
        disabled={disabled || loading}
        {...props}
      >
        {loading && (
          <svg className="mr-2 h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
          </svg>
        )}
        {children}
      </button>
    );
  }
);
Button.displayName = "Button";
