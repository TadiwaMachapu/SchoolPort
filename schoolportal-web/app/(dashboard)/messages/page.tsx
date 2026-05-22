"use client";
import { useEffect, useRef, useState } from "react";
import { api, MessageThread, ChatMessage } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default function MessagesPage() {
  const [threads, setThreads] = useState<MessageThread[]>([]);
  const [active, setActive] = useState<MessageThread | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [loading, setLoading] = useState(true);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    api.messages.threads()
      .then(t => setThreads(t as MessageThread[]))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const openThread = async (thread: MessageThread) => {
    setActive(thread);
    try {
      const msgs = await api.messages.getMessages(thread.threadId);
      setMessages(msgs as ChatMessage[]);
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: "smooth" }), 100);
    } catch {
      setMessages([]);
    }
  };

  const send = async () => {
    if (!active || !input.trim() || sending) return;
    setSending(true);
    try {
      const msg = await api.messages.sendMessage(active.threadId, input.trim()) as ChatMessage;
      setMessages(prev => [...prev, msg]);
      setInput("");
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: "smooth" }), 50);
      setThreads(prev => prev.map(t =>
        t.threadId === active.threadId ? { ...t, lastMessageAt: msg.sentAt } : t
      ));
    } catch {
      // send failed
    } finally {
      setSending(false);
    }
  };

  const formatTime = (iso: string) => {
    const d = new Date(iso);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
  };

  const initials = (name: string) => name.split(" ").map(n => n[0]).join("").toUpperCase().slice(0, 2);

  return (
    <div className="p-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Messages</h1>
        <p className="text-gray-500 mt-1">Direct messages and class discussions</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-0 border border-gray-200 rounded-xl overflow-hidden" style={{ height: "calc(100vh - 220px)", minHeight: 500 }}>
        {/* Thread list */}
        <div className="border-r border-gray-200 flex flex-col bg-white">
          <div className="p-4 border-b border-gray-100">
            <p className="text-sm font-semibold text-gray-700">Conversations</p>
          </div>
          <div className="flex-1 overflow-y-auto">
            {loading && <p className="p-4 text-sm text-gray-400">Loading…</p>}
            {!loading && threads.length === 0 && (
              <div className="p-8 text-center text-gray-400">
                <div className="text-3xl mb-2">💬</div>
                <p className="text-sm">No conversations yet</p>
              </div>
            )}
            {threads.map(t => (
              <button
                key={t.threadId}
                onClick={() => openThread(t)}
                className={`w-full text-left px-4 py-3 border-b border-gray-50 hover:bg-gray-50 transition-colors
                  ${active?.threadId === t.threadId ? "bg-blue-50 border-l-2 border-l-blue-500" : ""}`}
              >
                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center shrink-0">
                    {t.type === "class" ? "C" : initials(t.participants?.[0]?.name ?? "?")}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between">
                      <p className="text-sm font-medium text-gray-900 truncate">
                        {t.subject ?? t.className ?? "Direct Message"}
                      </p>
                      {t.lastMessageAt && (
                        <span className="text-[10px] text-gray-400 shrink-0 ml-1">{formatTime(t.lastMessageAt)}</span>
                      )}
                    </div>
                    <div className="flex items-center gap-1 mt-0.5">
                      <Badge variant="outline" className="text-[10px] capitalize py-0">{t.type}</Badge>
                      {(t.unreadCount ?? 0) > 0 && (
                        <span className="bg-blue-600 text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center">
                          {t.unreadCount}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </button>
            ))}
          </div>
        </div>

        {/* Message pane */}
        <div className="lg:col-span-2 flex flex-col bg-gray-50">
          {!active ? (
            <div className="flex-1 flex items-center justify-center text-gray-400">
              <div className="text-center">
                <div className="text-5xl mb-3">💬</div>
                <p className="text-base font-medium text-gray-600">Select a conversation</p>
                <p className="text-sm text-gray-400 mt-1">Choose a thread from the left to start messaging</p>
              </div>
            </div>
          ) : (
            <>
              {/* Header */}
              <div className="bg-white border-b border-gray-200 px-6 py-3 flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center">
                  {active.type === "class" ? "C" : initials(active.participants?.[0]?.name ?? "?")}
                </div>
                <div>
                  <p className="font-semibold text-gray-900 text-sm">
                    {active.subject ?? active.className ?? "Direct Message"}
                  </p>
                  <p className="text-xs text-gray-400">
                    {active.participants?.map(p => p.name).join(", ")}
                  </p>
                </div>
              </div>

              {/* Messages */}
              <div className="flex-1 overflow-y-auto p-6 space-y-4">
                {messages.length === 0 && (
                  <p className="text-center text-sm text-gray-400">No messages yet. Say hello!</p>
                )}
                {messages.map((msg, i) => {
                  const isMe = i % 2 === 0; // temp — replace with real userId check
                  return (
                    <div key={msg.messageId} className={`flex items-end gap-2 ${isMe ? "flex-row-reverse" : ""}`}>
                      <div className="w-7 h-7 rounded-full bg-gray-200 text-gray-600 text-xs font-bold flex items-center justify-center shrink-0">
                        {initials(msg.senderName)}
                      </div>
                      <div className={`max-w-[70%] ${isMe ? "items-end" : "items-start"} flex flex-col`}>
                        <span className="text-[10px] text-gray-400 mb-1 px-1">{msg.senderName}</span>
                        <div className={`rounded-2xl px-4 py-2 text-sm
                          ${isMe ? "bg-blue-600 text-white rounded-br-sm" : "bg-white text-gray-900 border border-gray-200 rounded-bl-sm"}`}>
                          {msg.isDeleted ? <em className="text-gray-400">Message deleted</em> : msg.content}
                        </div>
                        <span className="text-[10px] text-gray-400 mt-1 px-1">{formatTime(msg.sentAt)}</span>
                      </div>
                    </div>
                  );
                })}
                <div ref={bottomRef} />
              </div>

              {/* Input */}
              <div className="bg-white border-t border-gray-200 p-4">
                <form
                  onSubmit={e => { e.preventDefault(); send(); }}
                  className="flex gap-2"
                >
                  <Input
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    placeholder="Type a message…"
                    disabled={sending}
                    className="flex-1"
                  />
                  <Button type="submit" disabled={sending || !input.trim()} size="sm">
                    {sending ? "…" : "Send"}
                  </Button>
                </form>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
