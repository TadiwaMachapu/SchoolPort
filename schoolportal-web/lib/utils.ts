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

export function getClientUserId(): string {
  if (typeof document === "undefined") return "";
  const m = document.cookie.match(/(?:^|; )sp_userid=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : "";
}

/** Layer-1 identity (Staff | Learner | Parent | External | System) from the sp_identity
 *  cookie. Set at login/SSO; "" if absent. Used by Step 8 frontend hooks. */
export function getClientIdentity(): string {
  if (typeof document === "undefined") return "";
  const m = document.cookie.match(/(?:^|; )sp_identity=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : "";
}

/** One position assignment as carried in the JWT "pos" claim (compact keys). */
export interface ClientPosition {
  k: string;            // position key
  f: string;            // effective from (ISO)
  t?: string;           // effective to (ISO), optional
  s?: { st: number; id?: string; v?: string }[]; // scopes
}

/** Parses the "pos" claim out of the JWT body (sp_token). Returns [] if absent or malformed —
 *  never throws. Read-only convenience for Step 8 UI gating; NOT a security check (the server
 *  is always authoritative). Positions stay fresh because sp_token is rotated on refresh. */
export function getClientPositions(): ClientPosition[] {
  if (typeof document === "undefined") return [];
  const m = document.cookie.match(/(?:^|; )sp_token=([^;]*)/);
  if (!m) return [];
  try {
    const payload = JSON.parse(atob(decodeURIComponent(m[1]).split(".")[1]));
    return typeof payload.pos === "string" ? (JSON.parse(payload.pos) as ClientPosition[]) : [];
  } catch {
    return [];
  }
}
