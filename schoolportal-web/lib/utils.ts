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

/** User-facing label for a Layer-1 identity. The identity VALUE stays "Learner" everywhere in
 *  code/data; only the label shown to users reads "Student" (product copy). Others unchanged. */
export function identityLabel(identity: string): string {
  return identity === "Learner" ? "Student" : identity;
}

/** Clears ALL session cookies on logout (Step 8 FLAG 3). Leaving sp_refresh_token behind is a
 *  security issue on shared devices, so every cookie is expired here. */
export function clearSession(): void {
  if (typeof document === "undefined") return;
  for (const name of ["sp_token", "sp_role", "sp_userid", "sp_identity", "sp_refresh_token"]) {
    document.cookie = `${name}=; path=/; max-age=0; SameSite=Lax`;
  }
}

/**
 * CAPS 7-point code (1–7) for a percentage. Derived client-side (Step 8 My Academics).
 * Scale: 0-29→1, 30-39→2, 40-49→3, 50-59→4, 60-69→5, 70-79→6, 80-100→7.
 */
export function getCapsCode(percentage: number): number {
  if (percentage >= 80) return 7;
  if (percentage >= 70) return 6;
  if (percentage >= 60) return 5;
  if (percentage >= 50) return 4;
  if (percentage >= 40) return 3;
  if (percentage >= 30) return 2;
  return 1;
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
