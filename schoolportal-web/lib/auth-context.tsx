"use client";
import { createContext, useContext } from "react";

// Sprint 1.5.0 Step 8 — client-side auth context, seeded by the dashboard layout from /api/me
// (authoritative: identity from the JWT, positions + the RESOLVED effective permission set from
// the server). These hooks are for UX gating ONLY — the backend is always the security authority.

export interface AuthPosition {
  key: string;
  effectiveFrom: string;
  effectiveTo?: string;
  scopes: { scopeType: number; scopeRefId?: string; scopeValue?: string }[];
}

export interface AuthState {
  identity: string;          // Staff | Learner | Parent | External | System
  positions: AuthPosition[]; // active, in-window positions (with scopes + dates)
  permissions: string[];     // resolved effective permission set
  gradeLevel?: number | null;   // learner's own grade (null for non-learners) — Matric Hub gate
  hasGrade12Child?: boolean;    // parent has any linked child in Grade 12 — Matric Hub gate
}

const AuthContext = createContext<AuthState>({ identity: "", positions: [], permissions: [] });

export function AuthProvider({ value, children }: { value: AuthState; children: React.ReactNode }) {
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  return useContext(AuthContext);
}

/** Layer-1 identity string (Staff | Learner | Parent | External | System). */
export function useIdentity(): string {
  return useContext(AuthContext).identity;
}

/** True if the user currently holds the named position (e.g. "Principal", "HOD"). */
export function usePosition(name: string): boolean {
  return useContext(AuthContext).positions.some((p) => p.key === name);
}

/** True if the user holds ANY of the named positions. */
export function useAnyPosition(names: string[]): boolean {
  const positions = useContext(AuthContext).positions;
  return names.some((n) => positions.some((p) => p.key === n));
}

/** True if the resolved permission set contains the key (e.g. "marks.capture"). UX only. */
export function usePermission(name: string): boolean {
  return useContext(AuthContext).permissions.includes(name);
}

/** The learner's own grade level (null for non-learners). For Matric Hub / grade-gated UI. */
export function useGradeLevel(): number | null {
  return useContext(AuthContext).gradeLevel ?? null;
}
