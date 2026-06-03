"use client";
import { useEffect, useState } from "react";
import { Download, X } from "lucide-react";

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

const DISMISSED_KEY = "pwa-install-dismissed";

export function PwaInstallPrompt() {
  const [prompt, setPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (localStorage.getItem(DISMISSED_KEY)) return;

    const handler = (e: Event) => {
      e.preventDefault();
      setPrompt(e as BeforeInstallPromptEvent);
      setVisible(true);
    };

    window.addEventListener("beforeinstallprompt", handler);
    return () => window.removeEventListener("beforeinstallprompt", handler);
  }, []);

  if (!visible || !prompt) return null;

  async function install() {
    if (!prompt) return;
    await prompt.prompt();
    const { outcome } = await prompt.userChoice;
    if (outcome === "accepted") dismiss();
  }

  function dismiss() {
    localStorage.setItem(DISMISSED_KEY, "1");
    setVisible(false);
  }

  return (
    <div className="fixed bottom-20 md:bottom-4 left-1/2 -translate-x-1/2 z-50 w-[calc(100%-2rem)] max-w-sm">
      <div className="flex items-center gap-3 rounded-2xl bg-gray-900 px-4 py-3 shadow-2xl text-white">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-500">
          <Download className="h-4 w-4" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold leading-tight">Install School Portal</p>
          <p className="text-xs text-gray-400 mt-0.5">Fast access, works offline</p>
        </div>
        <button
          onClick={install}
          className="shrink-0 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-semibold hover:bg-blue-500 transition-colors"
        >
          Install
        </button>
        <button onClick={dismiss} className="shrink-0 text-gray-500 hover:text-gray-300 transition-colors p-0.5">
          <X className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
