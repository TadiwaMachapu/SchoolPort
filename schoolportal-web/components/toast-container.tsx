"use client";
import { useToastStore } from "@/stores/toast.store";
import { CheckCircle2, XCircle, Info, AlertTriangle, X } from "lucide-react";
import { AnimatePresence, motion } from "framer-motion";

const ICONS = {
  success: CheckCircle2,
  error: XCircle,
  info: Info,
  warning: AlertTriangle,
};

const COLORS = {
  success: "bg-success-100 border-success-500/20 text-success-700",
  error: "bg-danger-100 border-danger-500/20 text-danger-700",
  info: "bg-blue-50 border-blue-200 text-blue-800", // DEFERRED: needs the info tone (see CLAUDE.md info-tone gap)
  warning: "bg-warning-100 border-warning-500/20 text-warning-700",
};

const ICON_COLORS = {
  success: "text-success-500",
  error: "text-danger-500",
  info: "text-blue-500", // DEFERRED: info-tone gap
  warning: "text-warning-500",
};

export function ToastContainer() {
  const { toasts, remove } = useToastStore();

  return (
    <div className="fixed bottom-20 md:bottom-4 right-4 z-[200] flex flex-col gap-2 w-[calc(100vw-2rem)] max-w-sm pointer-events-none">
      <AnimatePresence mode="popLayout">
        {toasts.map((t) => {
          const Icon = ICONS[t.variant];
          return (
            <motion.div
              key={t.id}
              layout
              initial={{ opacity: 0, y: 20, scale: 0.95 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, scale: 0.9, transition: { duration: 0.15 } }}
              className={`pointer-events-auto flex items-start gap-3 rounded-xl border px-4 py-3 shadow-lg ${COLORS[t.variant]}`}
            >
              <Icon className={`h-5 w-5 shrink-0 mt-0.5 ${ICON_COLORS[t.variant]}`} />
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold">{t.title}</p>
                {t.description && <p className="text-xs mt-0.5 opacity-80">{t.description}</p>}
              </div>
              <button onClick={() => remove(t.id)} className="shrink-0 opacity-60 hover:opacity-100 transition-opacity">
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          );
        })}
      </AnimatePresence>
    </div>
  );
}
