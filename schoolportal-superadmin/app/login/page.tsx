"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Shield } from "lucide-react";

export default function LoginPage() {
  const router = useRouter();
  const [email,    setEmail]    = useState("");
  const [password, setPassword] = useState("");
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const res = await api.auth.login(email, password);
      const maxAge = 12 * 3600;
      document.cookie = `sa_token=${encodeURIComponent(res.accessToken)}; path=/; max-age=${maxAge}; SameSite=Lax`;
      document.cookie = `sa_name=${encodeURIComponent(`${res.superAdmin.firstName} ${res.superAdmin.lastName}`)}; path=/; max-age=${maxAge}; SameSite=Lax`;
      router.push("/schools");
      router.refresh();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4"
      style={{ background: "linear-gradient(135deg, #0a0a14 0%, #0f172a 60%, #1e293b 100%)" }}>
      <div className="w-full max-w-sm">
        <div className="rounded-2xl border border-white/10 bg-white/95 shadow-2xl backdrop-blur-sm overflow-hidden">
          <div className="bg-gradient-to-r from-violet-600 to-indigo-600 px-8 py-6 text-center">
            <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-white/20 shadow-lg">
              <Shield className="h-7 w-7 text-white" />
            </div>
            <h1 className="text-xl font-bold text-white">Super Admin</h1>
            <p className="mt-0.5 text-sm text-violet-200">SchoolPortal Platform Console</p>
          </div>

          <form onSubmit={handleSubmit} className="p-8 space-y-4">
            {error && (
              <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
            )}
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Email</label>
              <input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
                autoFocus
                placeholder="admin@yourplatform.com"
                className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Password</label>
              <input
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
                placeholder="••••••••"
                className="w-full rounded-xl border border-gray-200 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
              />
            </div>
            <button
              type="submit"
              disabled={loading}
              className="w-full rounded-xl bg-gradient-to-r from-violet-600 to-indigo-600 px-4 py-2.5 text-sm font-semibold text-white hover:from-violet-500 hover:to-indigo-500 disabled:opacity-60 transition-all"
            >
              {loading ? "Signing in…" : "Sign in"}
            </button>
          </form>
        </div>

        <p className="mt-6 text-center text-xs text-white/30">
          &copy; {new Date().getFullYear()} SchoolPortal Platform
        </p>
      </div>
    </div>
  );
}
