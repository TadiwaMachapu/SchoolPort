import * as React from "react";
import { cn } from "@/lib/utils";

interface AvatarProps {
  /** Initials or a short label (subject/class code). */
  label: string;
  /**
   * "person"  → circle (people)
   * "subject" → rounded square (subject / class chips)
   */
  shape?: "person" | "subject";
  size?: "sm" | "md";
  /** Optional inline background (e.g. the school primary for the current user). */
  color?: string;
  className?: string;
}

const sizes = {
  sm: "h-8 w-8 text-[11px]",
  md: "h-10 w-10 text-xs",
};

export function Avatar({ label, shape = "person", size = "sm", color, className }: AvatarProps) {
  return (
    <div
      className={cn(
        "flex items-center justify-center font-medium text-white shrink-0 select-none",
        shape === "person" ? "rounded-full" : "rounded-md",
        sizes[size],
        !color && "bg-primary",
        className
      )}
      style={color ? { backgroundColor: color } : undefined}
    >
      {label}
    </div>
  );
}
