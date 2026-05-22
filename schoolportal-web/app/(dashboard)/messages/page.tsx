"use client";
import { useEffect, useRef, useState } from "react";
import { api, type MessageThread, type ChatMessage, type User } from "@/lib/api";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { getClientUserId } from "@/lib/utils";

function initials(name: string) {
  return name.split(" ").map(n => n[0]).join("").toUpperCase().slice(0, 2);
}

function formatTime(iso: string) {
  const d   = new Date(iso);
  const now = new Date();
  if (d.toDateString() === now.toDateString())
    return d.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" });
  return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
}

export default function MessagesPage() {
  const [threads,    setThreads]    = useState<MessageThread[]>([]);
  const [active,     setActive]     = useState<MessageThread | null>(null);
  const [messages,   setMessages]   = useState<ChatMessage[]>([]);
  const [input,      setInput]      = useState("");
  const [sending,    setSending]    = useState(false);
  const [loading,    setLoading]    = useState(true);
  const [showNewDM,  setShowNewDM]  = useState(false);
  const [myUserId,   setMyUserId]   = useState("");
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => { setMyUserId(getClientUserId()); }, []);

  useEffect(() => {
    api.messages.threads()
      .then(t => setThreads(t as MessageThread[]))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  async function openThread(thread: MessageThread) {
    setActive(thread);
    try {
      const msgs = await api.messages.getMessages(thread.threadId) as ChatMessage[];
      setMessages(msgs);
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: "smooth" }), 100);
    } catch { setMessages([]); }
  }

  async function send() {
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
    } catch { /* ignore */ }
    finally { setSending(false); }
  }

  return (
    <div className="flex flex-col" style={{ height: "calc(100vh - 56px)" }}>
      {/* Page header */}
      <div className="flex items-center justify-between px-8 py-5 border-b border-gray-200 bg-white shrink-0">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Messages</h1>
          <p className="text-sm text-gray-500 mt-0.5">Direct messages and class discussions</p>
        </div>
        <Button size="sm" onClick={() => setShowNewDM(true)}>+ New Message</Button>
      </div>

      {/* Main area */}
      <div className="flex flex-1 overflow-hidden">
        {/* Thread list */}
        <div className="w-72 shrink-0 border-r border-gray-200 flex flex-col bg-white">
          <div className="flex-1 overflow-y-auto">
            {loading ? (
              <div className="p-4 space-y-3">
                {[1,2,3].map(i => (
                  <div key={i} className="flex items-center gap-3">
                    <Skeleton className="h-9 w-9 rounded-full" />
                    <div className="flex-1 space-y-1.5">
                      <Skeleton className="h-3.5 w-32" />
                      <Skeleton className="h-3 w-20" />
                    </div>
                  </div>
                ))}
              </div>
            ) : threads.length === 0 ? (
              <div className="p-8 text-center text-gray-400">
                <div className="text-4xl mb-3">💬</div>
                <p className="text-sm font-medium text-gray-500">No conversations yet</p>
                <p className="text-xs text-gray-400 mt-1">Start a new message to connect</p>
              </div>
            ) : threads.map(t => (
              <button key={t.threadId} onClick={() => openThread(t)}
                className={`w-full text-left px-4 py-3 border-b border-gray-50 hover:bg-gray-50 transition-colors
                  ${active?.threadId === t.threadId ? "bg-blue-50 border-l-[3px] border-l-blue-500" : ""}`}>
                <div className="flex items-start gap-3">
                  <div className="h-9 w-9 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center shrink-0">
                    {t.type === "class" ? "C" : initials(t.participants?.[0]?.name ?? "?")}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between">
                      <p className="text-sm font-medium text-gray-900 truncate">
                        {t.subject ?? t.className ?? "Direct Message"}
                      </p>
                      {t.lastMessageAt && (
                        <span className="text-[10px] text-gray-400 shrink-0 ml-2">{formatTime(t.lastMessageAt)}</span>
                      )}
                    </div>
                    <div className="flex items-center gap-1.5 mt-0.5">
                      <Badge variant="outline" className="text-[10px] capitalize py-0 px-1.5">{t.type}</Badge>
                      {(t.unreadCount ?? 0) > 0 && (
                        <span className="bg-blue-600 text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center font-bold">
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
        <div className="flex-1 flex flex-col bg-gray-50">
          {!active ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <div className="text-5xl mb-4">💬</div>
                <p className="text-base font-semibold text-gray-600">Select a conversation</p>
                <p className="text-sm text-gray-400 mt-1">Choose a thread from the left to start messaging</p>
                <Button className="mt-4" size="sm" onClick={() => setShowNewDM(true)}>+ New Message</Button>
              </div>
            </div>
          ) : (
            <>
              {/* Chat header */}
              <div className="bg-white border-b border-gray-200 px-6 py-3 flex items-center gap-3 shrink-0">
                <div className="h-9 w-9 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center">
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
                  <p className="text-center text-sm text-gray-400 py-8">No messages yet. Say hello!</p>
                )}
                {messages.map(msg => {
                  const isMe = msg.senderUserId === myUserId;
                  return (
                    <div key={msg.messageId} className={`flex items-end gap-2 ${isMe ? "flex-row-reverse" : ""}`}>
                      <div className="h-7 w-7 rounded-full bg-gray-200 text-gray-600 text-xs font-bold flex items-center justify-center shrink-0">
                        {initials(msg.senderName)}
                      </div>
                      <div className={`max-w-[70%] flex flex-col ${isMe ? "items-end" : "items-start"}`}>
                        <span className="text-[10px] text-gray-400 mb-1 px-1">{msg.senderName}</span>
                        <div className={`rounded-2xl px-4 py-2.5 text-sm ${
                          isMe ? "bg-blue-600 text-white rounded-br-sm" : "bg-white text-gray-900 border border-gray-200 rounded-bl-sm shadow-sm"
                        }`}>
                          {msg.isDeleted ? <em className="text-gray-400 text-xs">Message deleted</em> : msg.content}
                        </div>
                        <span className="text-[10px] text-gray-400 mt-1 px-1">{formatTime(msg.sentAt)}</span>
                      </div>
                    </div>
                  );
                })}
                <div ref={bottomRef} />
              </div>

              {/* Input */}
              <div className="bg-white border-t border-gray-200 p-4 shrink-0">
                <form onSubmit={e => { e.preventDefault(); send(); }} className="flex gap-2">
                  <Input value={input} onChange={e => setInput(e.target.value)}
                    placeholder="Type a message…" disabled={sending} className="flex-1" />
                  <Button type="submit" disabled={sending || !input.trim()} size="sm">
                    {sending ? "…" : "Send"}
                  </Button>
                </form>
              </div>
            </>
          )}
        </div>
      </div>

      {showNewDM && <NewDMModal onClose={thread => { setShowNewDM(false); if (thread) { setThreads(prev => [thread, ...prev]); openThread(thread); } }} />}
    </div>
  );
}

function NewDMModal({ onClose }: { onClose: (thread?: MessageThread) => void }) {
  const [users,     setUsers]     = useState<User[]>([]);
  const [q,         setQ]         = useState("");
  const [selected,  setSelected]  = useState<User | null>(null);
  const [subject,   setSubject]   = useState("");
  const [creating,  setCreating]  = useState(false);
  const [error,     setError]     = useState("");

  useEffect(() => {
    api.users.list({ pageSize: 50 })
      .then(r => setUsers(r.items))
      .catch(() => {});
  }, []);

  const filtered = q.trim()
    ? users.filter(u => `${u.firstName} ${u.lastName} ${u.email}`.toLowerCase().includes(q.toLowerCase()))
    : users;

  async function create() {
    if (!selected) return;
    setCreating(true);
    setError("");
    try {
      const res = await api.messages.createDirect(selected.userId, subject || undefined);
      const threads = await api.messages.threads() as MessageThread[];
      const thread  = threads.find(t => t.threadId === (res as { threadId: string }).threadId);
      onClose(thread);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally { setCreating(false); }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">New Message</h2>
          <button onClick={() => onClose()} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">To</label>
            {selected ? (
              <div className="flex items-center gap-2 rounded-lg border border-blue-300 bg-blue-50 px-3 py-2">
                <div className="h-6 w-6 rounded-full bg-blue-200 text-blue-800 text-xs font-bold flex items-center justify-center">
                  {selected.firstName[0]}{selected.lastName[0]}
                </div>
                <span className="text-sm font-medium text-blue-900">{selected.firstName} {selected.lastName}</span>
                <button onClick={() => setSelected(null)} className="ml-auto text-blue-400 hover:text-blue-600">×</button>
              </div>
            ) : (
              <Input placeholder="Search by name or email…" value={q} onChange={e => setQ(e.target.value)} autoFocus />
            )}
          </div>

          {!selected && q.trim() && (
            <div className="rounded-lg border border-gray-200 overflow-hidden max-h-48 overflow-y-auto">
              {filtered.length === 0 ? (
                <p className="p-3 text-sm text-gray-400 text-center">No users found</p>
              ) : filtered.slice(0, 8).map(u => (
                <button key={u.userId} onClick={() => { setSelected(u); setQ(""); }}
                  className="w-full flex items-center gap-3 px-4 py-2.5 hover:bg-gray-50 transition-colors text-left border-b border-gray-50 last:border-0">
                  <div className="h-7 w-7 rounded-full bg-gray-200 text-gray-600 text-xs font-bold flex items-center justify-center">
                    {u.firstName[0]}{u.lastName[0]}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-900">{u.firstName} {u.lastName}</p>
                    <p className="text-xs text-gray-400">{u.role} · {u.email}</p>
                  </div>
                </button>
              ))}
            </div>
          )}

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Subject <span className="text-gray-400 font-normal">(optional)</span></label>
            <Input placeholder="e.g. Question about homework" value={subject} onChange={e => setSubject(e.target.value)} />
          </div>

          <div className="flex gap-3 pt-1">
            <Button className="flex-1" onClick={create} disabled={!selected} loading={creating}>Start Conversation</Button>
            <Button variant="outline" onClick={() => onClose()}>Cancel</Button>
          </div>
        </div>
      </div>
    </div>
  );
}
