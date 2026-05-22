"use client";
import { useEffect, useRef, useState } from "react";

interface Notification {
  type: string;
  title: string;
  message: string;
  link?: string;
  timestamp: string;
}

export function NotificationBell() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [open, setOpen] = useState(false);
  const [connected, setConnected] = useState(false);
  const wsRef = useRef<WebSocket | null>(null);
  const ref = useRef<HTMLDivElement>(null);

  const unread = notifications.length;

  useEffect(() => {
    const token = document.cookie.match(/(?:^|; )sp_token=([^;]*)/)?.[1];
    if (!token) return;

    const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";
    const wsUrl = apiUrl.replace("http://", "ws://").replace("https://", "wss://");

    async function connect() {
      try {
        const { HubConnectionBuilder, LogLevel } = await import("@microsoft/signalr");
        const connection = new HubConnectionBuilder()
          .withUrl(`${apiUrl}/hubs/notifications`, {
            accessTokenFactory: () => decodeURIComponent(token!),
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build();

        connection.on("notification", (n: Notification) => {
          setNotifications((prev) => [n, ...prev].slice(0, 20));
        });

        await connection.start();
        setConnected(true);
      } catch {
        // SignalR not available or connection failed — silent fail
      }
    }

    connect();
  }, []);

  // Close dropdown on outside click
  useEffect(() => {
    function handle(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, []);

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => { setOpen((o) => !o); if (!open) setNotifications((n) => n); }}
        className="relative p-2 rounded-full hover:bg-gray-100 transition-colors"
        title="Notifications"
      >
        <span className="text-xl">🔔</span>
        {unread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 h-5 w-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center font-bold">
            {unread > 9 ? "9+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 mt-2 w-80 bg-white rounded-lg shadow-lg border border-gray-200 z-50">
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
            <h3 className="font-semibold text-gray-900">Notifications</h3>
            {notifications.length > 0 && (
              <button onClick={() => setNotifications([])} className="text-xs text-gray-400 hover:text-gray-600">
                Clear all
              </button>
            )}
          </div>

          <div className="max-h-80 overflow-y-auto divide-y divide-gray-50">
            {notifications.length === 0 ? (
              <div className="py-8 text-center text-gray-400 text-sm">No notifications</div>
            ) : (
              notifications.map((n, i) => (
                <div key={i} className="px-4 py-3 hover:bg-gray-50 cursor-pointer"
                  onClick={() => { if (n.link) window.location.href = n.link; }}>
                  <p className="text-sm font-medium text-gray-900">{n.title}</p>
                  <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{n.message}</p>
                  <p className="text-xs text-gray-400 mt-1">
                    {new Date(n.timestamp).toLocaleTimeString()}
                  </p>
                </div>
              ))
            )}
          </div>

          {!connected && (
            <div className="px-4 py-2 border-t border-gray-100 text-xs text-gray-400 text-center">
              Real-time updates unavailable
            </div>
          )}
        </div>
      )}
    </div>
  );
}
