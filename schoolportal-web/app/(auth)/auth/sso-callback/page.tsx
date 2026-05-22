"use client";
import { useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";

export default function SsoCallbackPage() {
  const router = useRouter();
  const params = useSearchParams();

  useEffect(() => {
    const token = params.get("token");
    const redirect = params.get("redirect") ?? "/dashboard";

    if (token) {
      document.cookie = `sp_token=${encodeURIComponent(token)}; path=/; max-age=${8 * 3600}; SameSite=Lax`;
      router.replace(redirect);
    } else {
      router.replace("/login?error=sso_failed");
    }
  }, [params, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-900">
      <div className="text-center text-white">
        <div className="text-5xl mb-4 animate-spin">⚙️</div>
        <p className="text-lg">Signing you in…</p>
      </div>
    </div>
  );
}
