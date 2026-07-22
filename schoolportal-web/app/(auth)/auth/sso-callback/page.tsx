"use client";
import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";

function SsoCallback() {
  const router = useRouter();
  const params = useSearchParams();

  useEffect(() => {
    const token = params.get("token");
    const redirect = params.get("redirect") ?? "/dashboard";

    if (token) {
      document.cookie = `sp_token=${encodeURIComponent(token)}; path=/; max-age=${8 * 3600}; SameSite=Lax`;
      // SSO returns only the raw token (no user payload), so read the identity claim from the
      // JWT body to set sp_identity, mirroring the email-login path.
      try {
        const identity = JSON.parse(atob(token.split(".")[1])).identity;
        if (identity) document.cookie = `sp_identity=${identity}; path=/; max-age=${8 * 3600}; SameSite=Lax`;
      } catch { /* malformed token — identity cookie simply not set */ }
      router.replace(redirect);
    } else {
      router.replace("/login?error=sso_failed");
    }
  }, [params, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-text-primary">
      <div className="text-center text-white">
        <div className="text-5xl mb-4 animate-spin">⚙️</div>
        <p className="text-lg">Signing you in…</p>
      </div>
    </div>
  );
}

export default function SsoCallbackPage() {
  return (
    <Suspense fallback={
      <div className="flex min-h-screen items-center justify-center bg-text-primary">
        <div className="text-center text-white">
          <div className="text-5xl mb-4 animate-spin">⚙️</div>
          <p className="text-lg">Signing you in…</p>
        </div>
      </div>
    }>
      <SsoCallback />
    </Suspense>
  );
}
