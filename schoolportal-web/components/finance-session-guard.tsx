"use client";
import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAnyPosition } from "@/lib/auth-context";
import { api } from "@/lib/api";
import { clearSession } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { ShieldAlert } from "lucide-react";

// Finance handles money, so its sessions are short-lived. We warn 10 minutes before the
// access token's exp claim — which lands at the ~50-minute mark on a 60-minute Finance
// session — and let the user extend it without losing their work. (Step 8 / FLAG 2)
const FINANCE_POSITIONS = ["FinanceManager", "BursarDebtorsClerk", "Cashier"];
const WARN_BEFORE_EXPIRY_MS = 10 * 60 * 1000;

/** Reads the exp claim (ms epoch) from the sp_token JWT, or null if absent/unparseable. */
function readTokenExpiry(): number | null {
  const match = document.cookie.match(/(?:^|;\s*)sp_token=([^;]+)/);
  if (!match) return null;
  try {
    const payload = decodeURIComponent(match[1]).split(".")[1];
    const json = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
    return typeof json.exp === "number" ? json.exp * 1000 : null;
  } catch {
    return null;
  }
}

export function FinanceSessionGuard() {
  const isFinance = useAnyPosition(FINANCE_POSITIONS);
  const router = useRouter();
  const [showWarning, setShowWarning] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const [staying, setStaying] = useState(false);

  useEffect(() => {
    if (!isFinance) return;

    const tick = () => {
      const exp = readTokenExpiry();
      if (exp == null) {
        setShowWarning(false);
        return;
      }
      const now = Date.now();
      if (now >= exp) {
        // Token already dead — drop the session and bounce to login.
        clearSession();
        router.push("/login");
        return;
      }
      setShowWarning(now >= exp - WARN_BEFORE_EXPIRY_MS);
      setSecondsLeft(Math.max(0, Math.floor((exp - now) / 1000)));
    };

    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [isFinance, router]);

  const stay = useCallback(async () => {
    setStaying(true);
    try {
      const token = await api.auth.refresh();
      if (token) {
        // New token written — next tick recomputes against the later exp and clears the warning.
        setShowWarning(false);
      } else {
        clearSession();
        router.push("/login");
      }
    } finally {
      setStaying(false);
    }
  }, [router]);

  if (!isFinance || !showWarning) return null;

  const mins = Math.floor(secondsLeft / 60);
  const secs = secondsLeft % 60;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-2xl bg-surface-card p-6 shadow-2xl">
        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-warning-100">
          <ShieldAlert className="h-6 w-6 text-warning-700" />
        </div>
        <h2 className="text-center text-lg font-semibold text-text-primary">Your session is about to expire</h2>
        <p className="mt-1.5 text-center text-sm text-text-secondary">
          For security, Finance sessions time out after a period of inactivity. You&apos;ll be signed out in{" "}
          <span className="font-semibold text-text-primary tabular-nums">
            {mins}:{secs.toString().padStart(2, "0")}
          </span>
          .
        </p>
        <div className="mt-5 flex gap-2">
          <Button variant="outline" className="flex-1" onClick={() => { clearSession(); router.push("/login"); }}>
            Sign out
          </Button>
          <Button className="flex-1" loading={staying} onClick={stay}>
            Stay signed in
          </Button>
        </div>
      </div>
    </div>
  );
}
