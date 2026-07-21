"use client";
import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { api } from "@/lib/api";
import { queryClient } from "@/shared/api/queryClient";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { GraduationCap, Shield, BookOpen, User, Users } from "lucide-react";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

// Demo pills are role affordances, NOT status — so no status tones (success/warning) and
// no green primary wash. All four share one neutral treatment (white on the surface-subtle
// box) and differentiate by ICON + LABEL, not colour. See CLAUDE.md colour rules.
const DEMO_PILL = "bg-surface-card border border-border text-text-secondary hover:border-primary-300 hover:text-primary";
const DEMO_USERS = [
  { role: "Admin",   email: "admin@demo.schoolportal.com",   Icon: Shield },
  { role: "Teacher", email: "teacher@demo.schoolportal.com", Icon: BookOpen },
  { role: "Student", email: "student@demo.schoolportal.com", Icon: GraduationCap },
  { role: "Parent",  email: "parent@demo.schoolportal.com",  Icon: Users },
] as const;

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-surface-page flex items-center justify-center"><div className="text-text-secondary text-sm">Loading…</div></div>}>
      <LoginView />
    </Suspense>
  );
}

function LoginView() {
  const router = useRouter();
  const params = useSearchParams();
  const ssoError = params.get("error");

  const [email, setEmail]       = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading]   = useState(false);
  // null = user hasn't chosen. Persistence then defaults ON for staff/parents but OFF
  // for Learners (BYOD / shared devices) — see post-login logic below. (Step 8)
  const [rememberMe, setRememberMe] = useState<boolean | null>(null);
  const [error, setError]       = useState(
    ssoError === "sso_failed"        ? "SSO sign-in failed. Please try again." :
    ssoError === "school_not_found"  ? "Your school is not registered on this platform." : ""
  );

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const res = await api.auth.login(email, password);
      const tokenMaxAge = res.expiresAt
        ? Math.floor((new Date(res.expiresAt).getTime() - Date.now()) / 1000)
        : 8 * 3600;
      document.cookie = `sp_token=${encodeURIComponent(res.accessToken)}; path=/; max-age=${tokenMaxAge}; SameSite=Lax`;
      document.cookie = `sp_role=${res.user.role}; path=/; max-age=${tokenMaxAge}; SameSite=Lax`;
      document.cookie = `sp_userid=${res.user.userId}; path=/; max-age=${tokenMaxAge}; SameSite=Lax`;
      document.cookie = `sp_identity=${res.user.identity}; path=/; max-age=${tokenMaxAge}; SameSite=Lax`;
      if (res.refreshToken) {
        // Default OFF for Learners (shared/BYOD devices); ON for everyone else.
        // An untouched checkbox (null) resolves per-identity; an explicit choice always wins.
        const isLearner = res.user.identity === "Learner";
        const persist = rememberMe === null ? !isLearner : rememberMe;
        // Persistent: 30-day cookie. Otherwise a session cookie cleared when the browser closes.
        const refreshAttrs = persist ? `; max-age=${3600 * 24 * 30}` : "";
        document.cookie = `sp_refresh_token=${encodeURIComponent(res.refreshToken)}; path=/${refreshAttrs}; SameSite=Lax`;
      }
      // Drop any cached queries from a previous session so the new user never sees the prior
      // account's cached /api/me (or other) data (the "Welcome back, <wrong name>" bug).
      queryClient.clear();
      router.push("/dashboard");
      router.refresh();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  }

  function fillDemo(demoEmail: string) {
    setEmail(demoEmail);
    setPassword("Admin@123");
    setError("");
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4 bg-surface-page">

      <div className="relative w-full max-w-md">
        {/* Card */}
        <div className="rounded-xl bg-surface-card shadow-card">

          {/* Header */}
          <div className="px-8 pt-7 pb-5 text-center border-b border-border">
            <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-xl bg-primary">
              <GraduationCap className="h-7 w-7 text-white" />
            </div>
            <h1 className="text-xl font-bold text-text-primary">School Portal</h1>
            <p className="mt-0.5 text-sm text-text-secondary">Sign in to your account</p>
          </div>

          <div className="px-8 py-5 space-y-4">
            {/* SSO buttons */}
            <div className="space-y-2">
              <a href={`${API_URL}/api/auth/sso/google`}
                className="flex w-full items-center justify-center gap-3 rounded-md border border-border bg-surface-card px-4 py-2.5 text-sm font-medium text-text-secondary hover:bg-surface-subtle transition-colors">
                <svg viewBox="0 0 24 24" className="h-5 w-5" xmlns="http://www.w3.org/2000/svg">
                  <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
                  <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                  <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
                  <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
                </svg>
                Continue with Google
              </a>
              <a href={`${API_URL}/api/auth/sso/microsoft`}
                className="flex w-full items-center justify-center gap-3 rounded-md border border-border bg-surface-card px-4 py-2.5 text-sm font-medium text-text-secondary hover:bg-surface-subtle transition-colors">
                <svg viewBox="0 0 24 24" className="h-5 w-5" xmlns="http://www.w3.org/2000/svg">
                  <path fill="#f25022" d="M1 1h10v10H1z"/><path fill="#00a4ef" d="M13 1h10v10H13z"/>
                  <path fill="#7fba00" d="M1 13h10v10H1z"/><path fill="#ffb900" d="M13 13h10v10H13z"/>
                </svg>
                Continue with Microsoft 365
              </a>
            </div>

            <div className="relative">
              <div className="absolute inset-0 flex items-center"><div className="w-full border-t border-border" /></div>
              <div className="relative flex justify-center text-xs uppercase">
                <span className="bg-surface-card px-2 text-text-muted tracking-wider">or email</span>
              </div>
            </div>

            {/* Email form */}
            <form onSubmit={handleSubmit} className="space-y-3">
              {error && (
                <div className="flex items-start gap-2 rounded-lg bg-danger-100 p-3">
                  <span className="text-danger-500 mt-0.5 text-sm font-bold">!</span>
                  <p className="text-sm text-danger-700">{error}</p>
                </div>
              )}
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-secondary">Email</label>
                <Input type="email" placeholder="you@school.com" value={email}
                  onChange={e => setEmail(e.target.value)} required autoFocus />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-secondary">Password</label>
                <Input type="password" placeholder="••••••••" value={password}
                  onChange={e => setPassword(e.target.value)} required />
              </div>
              <label className="flex items-center gap-2 text-sm text-text-secondary select-none cursor-pointer">
                <input type="checkbox" checked={rememberMe ?? true}
                  onChange={e => setRememberMe(e.target.checked)}
                  className="h-4 w-4 rounded border-border accent-[var(--color-primary)] focus:ring-primary" />
                Keep me signed in on this device
              </label>
              <Button type="submit" className="w-full" size="lg" loading={loading}>
                Sign in
              </Button>
            </form>

            {/* Demo credentials */}
            <div className="rounded-lg bg-surface-subtle p-4">
              <p className="text-[10px] font-semibold uppercase tracking-widest text-text-muted mb-3">
                Demo accounts — click to fill
              </p>
              <div className="grid grid-cols-2 gap-2">
                {DEMO_USERS.map(u => (
                  <button key={u.role} onClick={() => fillDemo(u.email)} type="button"
                    className={`flex items-center gap-2 rounded-pill px-3 py-1.5 text-xs font-medium transition-all ${DEMO_PILL}`}>
                    <u.Icon className="h-3.5 w-3.5 shrink-0" />
                    <span>{u.role}</span>
                  </button>
                ))}
              </div>
              <p className="text-[10px] text-text-muted mt-2 text-center">Password: Admin@123</p>
            </div>
          </div>
        </div>

        <p className="mt-6 text-center text-xs text-text-muted">
          &copy; {new Date().getFullYear()} School Portal · All rights reserved
        </p>
      </div>
    </div>
  );
}
