"use client";
import { useEffect, useRef, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { MessageSquare } from "lucide-react";
import { api, type MessageThread, type ChatMessage, type DirectoryUser } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { getClientUserId } from "@/lib/utils";

const POLL_MS = 5000;

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
  const router = useRouter();
  const hasSchoolChat = useFeature("schoolChat");
  const [threads,   setThreads]   = useState<MessageThread[]>([]);
  const [active,    setActive]    = useState<MessageThread | null>(null);
  const [messages,  setMessages]  = useState<ChatMessage[]>([]);
  const [input,     setInput]     = useState("");
  const [sending,   setSending]   = useState(false);
  const [loading,   setLoading]   = useState(true);
  const [showNewDM, setShowNewDM] = useState(false);
  const [myUserId,  setMyUserId]  = useState("");
  const bottomRef    = useRef<HTMLDivElement>(null);
  const activeRef    = useRef<MessageThread | null>(null);
  const messagesRef  = useRef<ChatMessage[]>([]);
  const pollTimer    = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => { setMyUserId(getClientUserId()); }, []);

  // Keep refs in sync so the poll closure sees fresh values
  useEffect(() => { activeRef.current = active; }, [active]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);

  useEffect(() => {
    api.messages.threads()
      .then(t => setThreads(t as MessageThread[]))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  // Poll for new messages in active thread every 5 s
  const pollMessages = useCallback(async () => {
    const thread = activeRef.current;
    if (!thread) return;
    try {
      const fresh = await api.messages.getMessages(thread.threadId) as ChatMessage[];
      const current = messagesRef.current;
      if (fresh.length > current.length) {
        setMessages(fresh);
        // Only scroll if the user is near the bottom
        const el = bottomRef.current?.parentElement;
        const nearBottom = el ? el.scrollHeight - el.scrollTop - el.clientHeight < 120 : true;
        if (nearBottom) setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: "smooth" }), 50);
        // Refresh thread list unread counts
        setThreads(prev => prev.map(t =>
          t.threadId === thread.threadId
            ? { ...t, lastMessageAt: fresh.at(-1)?.sentAt ?? t.lastMessageAt }
            : t
        ));
      }
    } catch { /* silent */ }
  }, []);

  // Start/stop poll when active thread changes
  useEffect(() => {
    if (pollTimer.current) clearInterval(pollTimer.current);
    if (!active) return;
    pollTimer.current = setInterval(pollMessages, POLL_MS);
    return () => { if (pollTimer.current) clearInterval(pollTimer.current); };
  }, [active, pollMessages]);

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

  if (!hasSchoolChat) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <MessageSquare className="h-12 w-12 text-text-muted mb-4" />
        <h2 className="text-lg font-semibold text-text-primary">SchoolChat not enabled</h2>
        <p className="text-sm text-text-muted mt-1">Enable SchoolChat in Settings to send and receive messages.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-primary hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="flex flex-col" style={{ height: "calc(100vh - 56px)" }}>
      {/* Page header */}
      <div className="flex items-center justify-between px-8 py-5 border-b border-border bg-surface-card shrink-0">
        <div>
          <h1 className="text-2xl font-bold text-text-primary">Messages</h1>
          <p className="text-sm text-text-secondary mt-0.5">Direct messages and class discussions</p>
        </div>
        <Button size="sm" onClick={() => setShowNewDM(true)}>+ New Message</Button>
      </div>

      {/* Main area */}
      <div className="flex flex-1 overflow-hidden">
        {/* Thread list */}
        <div className="w-72 shrink-0 border-r border-border flex flex-col bg-surface-card">
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
              <div className="p-8 text-center text-text-muted">
                <div className="flex justify-center mb-3">
                  <MessageSquare className="h-10 w-10 text-text-muted" />
                </div>
                <p className="text-sm font-medium text-text-secondary">No conversations yet</p>
                <p className="text-xs text-text-muted mt-1">Start a new message to connect</p>
              </div>
            ) : threads.map(t => (
              <button key={t.threadId} onClick={() => openThread(t)}
                className={`w-full text-left px-4 py-3 border-b border-border hover:bg-surface-subtle transition-colors
                  ${active?.threadId === t.threadId ? "bg-primary-50 border-l-[3px] border-l-primary" : ""}`}>
                <div className="flex items-start gap-3">
                  <div className="h-9 w-9 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center shrink-0">
                    {t.type === "class" ? "C" : initials(t.participants?.[0]?.name ?? "?")}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between">
                      <p className="text-sm font-medium text-text-primary truncate">
                        {t.subject ?? t.className ?? "Direct Message"}
                      </p>
                      {t.lastMessageAt && (
                        <span className="text-[10px] text-text-muted shrink-0 ml-2">{formatTime(t.lastMessageAt)}</span>
                      )}
                    </div>
                    <div className="flex items-center gap-1.5 mt-0.5">
                      <Badge variant="outline" className="text-[10px] capitalize py-0 px-1.5">{t.type}</Badge>
                      {(t.unreadCount ?? 0) > 0 && (
                        <span className="bg-primary text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center font-bold">
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
        <div className="flex-1 flex flex-col bg-surface-subtle">
          {!active ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <div className="flex justify-center mb-4">
                  <MessageSquare className="h-12 w-12 text-text-muted" />
                </div>
                <p className="text-base font-semibold text-text-secondary">Select a conversation</p>
                <p className="text-sm text-text-muted mt-1">Choose a thread from the left to start messaging</p>
                <Button className="mt-4" size="sm" onClick={() => setShowNewDM(true)}>+ New Message</Button>
              </div>
            </div>
          ) : (
            <>
              {/* Chat header */}
              <div className="bg-surface-card border-b border-border px-6 py-3 flex items-center gap-3 shrink-0">
                <div className="h-9 w-9 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center shrink-0">
                  {active.type === "class" ? "C" : initials(active.participants?.[0]?.name ?? "?")}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-text-primary text-sm truncate">
                    {active.subject ?? active.className ?? "Direct Message"}
                  </p>
                  <p className="text-xs text-text-muted truncate">
                    {active.participants?.map(p => p.name).join(", ")}
                  </p>
                </div>
                <div className="flex items-center gap-1.5 shrink-0 text-xs text-success-700">
                  <span className="h-1.5 w-1.5 rounded-full bg-success-500 animate-pulse" />
                  Live
                </div>
              </div>

              {/* Messages */}
              <div className="flex-1 overflow-y-auto p-6 space-y-4">
                {messages.length === 0 && (
                  <p className="text-center text-sm text-text-muted py-8">No messages yet. Say hello!</p>
                )}
                {messages.map(msg => {
                  const isMe = msg.senderUserId === myUserId;
                  return (
                    <div key={msg.messageId} className={`flex items-end gap-2 ${isMe ? "flex-row-reverse" : ""}`}>
                      <div className="h-7 w-7 rounded-full bg-surface-subtle text-text-secondary text-xs font-bold flex items-center justify-center shrink-0">
                        {initials(msg.senderName)}
                      </div>
                      <div className={`max-w-[70%] flex flex-col ${isMe ? "items-end" : "items-start"}`}>
                        <span className="text-[10px] text-text-muted mb-1 px-1">{msg.senderName}</span>
                        <div className={`rounded-2xl px-4 py-2.5 text-sm ${
                          isMe ? "bg-primary text-white rounded-br-sm" : "bg-surface-card text-text-primary border border-border rounded-bl-sm shadow-sm"
                        }`}>
                          {msg.isDeleted ? <em className="text-text-muted text-xs">Message deleted</em> : msg.content}
                        </div>
                        <span className="text-[10px] text-text-muted mt-1 px-1">{formatTime(msg.sentAt)}</span>
                      </div>
                    </div>
                  );
                })}
                <div ref={bottomRef} />
              </div>

              {/* Input */}
              <div className="bg-surface-card border-t border-border p-4 shrink-0">
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
  const [results,   setResults]   = useState<DirectoryUser[]>([]);
  const [q,         setQ]         = useState("");
  const [searching, setSearching] = useState(false);
  const [selected,  setSelected]  = useState<DirectoryUser | null>(null);
  const [subject,   setSubject]   = useState("");
  const [creating,  setCreating]  = useState(false);
  const [error,     setError]     = useState("");

  // Debounced live search against the directory endpoint
  useEffect(() => {
    if (!q.trim()) { setResults([]); return; }
    const timer = setTimeout(() => {
      setSearching(true);
      api.users.directory(q.trim())
        .then(r => setResults(r))
        .catch(() => setResults([]))
        .finally(() => setSearching(false));
    }, 250);
    return () => clearTimeout(timer);
  }, [q]);

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
      <div className="w-full max-w-md rounded-2xl bg-surface-card shadow-2xl">
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h2 className="text-lg font-semibold text-text-primary">New Message</h2>
          <button onClick={() => onClose()} className="rounded-full p-1 text-text-muted hover:bg-surface-subtle hover:text-text-secondary transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-text-primary">To</label>
            {selected ? (
              <div className="flex items-center gap-2 rounded-lg border border-primary-300 bg-primary-50 px-3 py-2">
                <div className="h-6 w-6 rounded-full bg-primary-200 text-primary-800 text-xs font-bold flex items-center justify-center">
                  {selected.firstName[0]}{selected.lastName[0]}
                </div>
                <span className="text-sm font-medium text-primary-800">{selected.firstName} {selected.lastName}</span>
                <button onClick={() => { setSelected(null); setResults([]); }} className="ml-auto text-primary hover:text-primary-800">×</button>
              </div>
            ) : (
              <Input placeholder="Search by name or email…" value={q} onChange={e => setQ(e.target.value)} autoFocus />
            )}
          </div>

          {!selected && q.trim() && (
            <div className="rounded-lg border border-border overflow-hidden max-h-48 overflow-y-auto">
              {searching ? (
                <p className="p-3 text-sm text-text-muted text-center">Searching…</p>
              ) : results.length === 0 ? (
                <p className="p-3 text-sm text-text-muted text-center">No users found</p>
              ) : results.slice(0, 8).map(u => (
                <button key={u.userId} onClick={() => { setSelected(u); setQ(""); setResults([]); }}
                  className="w-full flex items-center gap-3 px-4 py-2.5 hover:bg-surface-subtle transition-colors text-left border-b border-border last:border-0">
                  <div className="h-7 w-7 rounded-full bg-surface-subtle text-text-secondary text-xs font-bold flex items-center justify-center">
                    {u.firstName[0]}{u.lastName[0]}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-text-primary">{u.firstName} {u.lastName}</p>
                    <p className="text-xs text-text-muted">{u.role} · {u.email}</p>
                  </div>
                </button>
              ))}
            </div>
          )}

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-text-primary">Subject <span className="text-text-muted font-normal">(optional)</span></label>
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
