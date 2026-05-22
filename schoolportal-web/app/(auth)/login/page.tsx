"use client";
import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

const DEMO_USERS = [
  { role: "Admin",   email: "admin@demo.schoolportal.com",   icon: "🛡️", color: "bg-purple-50 border-purple-200 text-purple-700 hover:bg-purple-100" },
  { role: "Teacher", email: "teacher@demo.schoolportal.com", icon: "👨‍🏫", color: "bg-blue-50 border-blue-200 text-blue-700 hover:bg-blue-100" },
  { role: "Student", email: "student@demo.schoolportal.com", icon: "🎓", color: "bg-green-50 border-green-200 text-green-700 hover:bg-green-100" },
  { role: "Parent",  email: "parent@demo.schoolportal.com",  icon: "👨‍👩‍👧", color: "bg-orange-50 border-orange-200 text-orange-700 hover:bg-orange-100" },
] as const;

export default function LoginPage() {
  const router = useRouter();
  const params = useSearchParams();
  const ssoError = params.get("error");

  const [email, setEmail]       = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading]   = useState(false);
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
      const maxAge = 8 * 3600;
      document.cookie = `sp_token=${encodeURIComponent(res.accessToken)}; path=/; max-age=${maxAge}; SameSite=Lax`;
      document.cookie = `sp_role=${res.user.role}; path=/; max-age=${maxAge}; SameSite=Lax`;
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
    <div className="flex min-h-screen items-center justify-center p-4"
      style={{ background: "linear-gradient(135deg, #0f172a 0%, #1e3a5f 50%, #1e40af 100%)" }}>

      {/* Decorative background circles */}
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute -top-32 -left-32 h-96 w-96 rounded-full bg-blue-500/10 blur-3xl" />
        <div className="absolute -bottom-32 -right-32 h-96 w-96 rounded-full bg-indigo-500/10 blur-3xl" />
      </div>

      <div className="relative w-full max-w-md">
        {/* Card */}
        <div className="rounded-2xl border border-white/10 bg-white/95 shadow-2xl backdrop-blur-sm">

          {/* Header */}
          <div className="px-8 pt-8 pb-6 text-center border-b border-gray-100">
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-blue-600 shadow-lg shadow-blue-600/30">
              <span className="text-3xl">🏫</span>
            </div>
            <h1 className="text-2xl font-bold text-gray-900">School Portal</h1>
            <p className="mt-1 text-sm text-gray-500">Sign in to your account</p>
          </div>

          <div className="px-8 py-6 space-y-5">
            {/* SSO buttons */}
            <div className="space-y-2">
              <a href={`${API_URL}/api/auth/sso/google`}
                className="flex w-full items-center justify-center gap-3 rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50 transition-all hover:shadow-md">
                <svg viewBox="0 0 24 24" className="h-5 w-5" xmlns="http://www.w3.org/2000/svg">
                  <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
                  <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                  <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
                  <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
                </svg>
                Continue with Google
              </a>
              <a href={`${API_URL}/api/auth/sso/microsoft`}
                className="flex w-full items-center justify-center gap-3 rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50 transition-all hover:shadow-md">
                <svg viewBox="0 0 24 24" className="h-5 w-5" xmlns="http://www.w3.org/2000/svg">
                  <path fill="#f25022" d="M1 1h10v10H1z"/><path fill="#00a4ef" d="M13 1h10v10H13z"/>
                  <path fill="#7fba00" d="M1 13h10v10H1z"/><path fill="#ffb900" d="M13 13h10v10H13z"/>
                </svg>
                Continue with Microsoft 365
              </a>
            </div>

            <div className="relative">
              <div className="absolute inset-0 flex items-center"><div className="w-full border-t border-gray-200" /></div>
              <div className="relative flex justify-center text-xs uppercase">
                <span className="bg-white px-2 text-gray-400 tracking-wider">or email</span>
              </div>
            </div>

            {/* Email form */}
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div className="flex items-start gap-2 rounded-lg bg-red-50 border border-red-200 p-3">
                  <span className="text-red-500 mt-0.5">⚠</span>
                  <p className="text-sm text-red-700">{error}</p>
                </div>
              )}
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Email</label>
                <Input type="email" placeholder="you@school.com" value={email}
                  onChange={e => setEmail(e.target.value)} required autoFocus />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Password</label>
                <Input type="password" placeholder="••••••••" value={password}
                  onChange={e => setPassword(e.target.value)} required />
              </div>
              <Button type="submit" className="w-full" size="lg" loading={loading}>
                Sign in
              </Button>
            </form>

            {/* Demo credentials */}
            <div className="rounded-xl border border-dashed border-gray-300 bg-gray-50 p-4">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">
                Demo accounts — click to fill
              </p>
              <div className="grid grid-cols-2 gap-2">
                {DEMO_USERS.map(u => (
                  <button key={u.role} onClick={() => fillDemo(u.email)} type="button"
                    className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-xs font-medium transition-all ${u.color}`}>
                    <span className="text-base">{u.icon}</span>
                    <span>{u.role}</span>
                  </button>
                ))}
              </div>
              <p className="text-[10px] text-gray-400 mt-2 text-center">Password: Admin@123</p>
            </div>
          </div>
        </div>

        <p className="mt-6 text-center text-xs text-white/40">
          © {new Date().getFullYear()} School Portal · All rights reserved
        </p>
      </div>
    </div>
  );
}
