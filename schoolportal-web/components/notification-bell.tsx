"use client";
import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { AnimatePresence, motion } from "framer-motion";
import { useNotifications, useMarkNotificationRead, useMarkAllRead } from "@/features/notifications/api/hooks";
import { qk } from "@/shared/api/queryKeys";

const TYPE_ICONS: Record<string, string> = {
  new_assignment: "📝",
  grade_posted: "🎯",
  attendance_absent: "⚠️",
  announcement: "📢",
};

function typeIcon(type: string) {
  return TYPE_ICONS[type] ?? "🔔";
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const qc = useQueryClient();

  const { data, isLoading } = useNotifications(30);
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllRead();

  const items = data?.items ?? [];
  const unreadCount = data?.unreadCount ?? 0;

  // SignalR: invalidate query on push
  useEffect(() => {
    const token = document.cookie.match(/(?:^|; )sp_token=([^;]*)/)?.[1];
    if (!token) return;

    const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";
    let stopFn: (() => void) | undefined;

    async function connect() {
      try {
        const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");
        const conn = new HubConnectionBuilder()
          .withUrl(`${apiUrl}/hubs/notifications`, {
            accessTokenFactory: () => decodeURIComponent(token!),
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build();

        conn.on("notification", () => {
          qc.invalidateQueries({ queryKey: qk.notifications.all() });
        });

        await conn.start();
        stopFn = () => conn.stop();
      } catch {
        // silent — realtime unavailable
      }
    }

    connect();
    return () => stopFn?.();
  }, [qc]);

  // Close on outside click
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  function handleItemClick(item: { notificationId: string; isRead: boolean; link?: string }) {
    if (!item.isRead) markRead.mutate(item.notificationId);
    if (item.link) window.location.href = item.link;
    setOpen(false);
  }

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        className="relative p-2 rounded-full hover:bg-gray-100 transition-colors min-h-[44px] min-w-[44px] flex items-center justify-center"
        aria-label="Notifications"
      >
        <span className="text-xl">🔔</span>
        <AnimatePresence>
          {unreadCount > 0 && (
            <motion.span
              key="badge"
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              exit={{ scale: 0 }}
              className="absolute -top-0.5 -right-0.5 h-5 w-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center font-bold"
            >
              {unreadCount > 9 ? "9+" : unreadCount}
            </motion.span>
          )}
        </AnimatePresence>
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: -8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: -8 }}
            transition={{ duration: 0.15 }}
            className="absolute right-0 mt-2 w-80 bg-white rounded-2xl shadow-xl border border-gray-100 z-50 overflow-hidden"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-50">
              <h3 className="font-semibold text-gray-900 text-sm">Notifications</h3>
              {unreadCount > 0 && (
                <button
                  onClick={() => markAll.mutate()}
                  className="text-xs text-blue-500 hover:text-blue-700 font-medium transition-colors"
                >
                  Mark all read
                </button>
              )}
            </div>

            <div className="max-h-[360px] overflow-y-auto divide-y divide-gray-50">
              {isLoading ? (
                <div className="py-10 text-center text-gray-400 text-sm">Loading…</div>
              ) : items.length === 0 ? (
                <div className="py-10 text-center">
                  <p className="text-2xl mb-2">🔕</p>
                  <p className="text-sm text-gray-400">All caught up</p>
                </div>
              ) : (
                items.map((n) => (
                  <button
                    key={n.notificationId}
                    onClick={() => handleItemClick(n)}
                    className={`w-full text-left flex gap-3 px-4 py-3 transition-colors ${
                      n.isRead ? "hover:bg-gray-50" : "bg-blue-50/60 hover:bg-blue-50"
                    }`}
                  >
                    <span className="text-lg shrink-0 mt-0.5">{typeIcon(n.type)}</span>
                    <div className="min-w-0 flex-1">
                      <p className={`text-sm font-medium truncate ${n.isRead ? "text-gray-600" : "text-gray-900"}`}>
                        {n.title}
                      </p>
                      <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.message}</p>
                      <p className="text-xs text-gray-400 mt-1">
                        {new Date(n.createdAt).toLocaleString(undefined, {
                          month: "short", day: "numeric",
                          hour: "2-digit", minute: "2-digit",
                        })}
                      </p>
                    </div>
                    {!n.isRead && (
                      <span className="h-2 w-2 rounded-full bg-blue-500 shrink-0 mt-2 self-start" />
                    )}
                  </button>
                ))
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
