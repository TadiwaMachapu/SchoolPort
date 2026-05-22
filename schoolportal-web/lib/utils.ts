import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function getClientRole(): string {
  if (typeof document === "undefined") return "";
  const m = document.cookie.match(/(?:^|; )sp_role=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : "";
}
