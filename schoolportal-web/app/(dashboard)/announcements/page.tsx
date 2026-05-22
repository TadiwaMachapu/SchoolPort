"use client";
import { useEffect, useState } from "react";
import { api, type Announcement } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";

export default function AnnouncementsPage() {
  const [items, setItems] = useState<Announcement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function load() {
    setLoading(true);
    setError("");
    try {
      const res = await api.announcements.list({ pageSize: 20 });
      setItems(res.items);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function remove(id: string) {
    try { await api.announcements.delete(id); } catch { /* ignore */ }
    load();
  }

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Announcements</h1>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}
      {loading ? (
        <div className="flex justify-center py-12 text-gray-400">Loading…</div>
      ) : items.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 py-12 text-center text-gray-400">No announcements yet</div>
      ) : (
        <div className="space-y-4">
          {items.map((a) => (
            <Card key={a.announcementId}>
              <CardContent className="p-6">
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <h3 className="font-semibold text-gray-900">{a.title}</h3>
                      <Badge variant="outline">{a.audience}</Badge>
                    </div>
                    <p className="text-gray-600 text-sm leading-relaxed">{a.content}</p>
                    <p className="text-xs text-gray-400 mt-2">
                      {a.createdByName} · {new Date(a.createdAt).toLocaleDateString()}
                      {a.expiresAt && ` · Expires ${new Date(a.expiresAt).toLocaleDateString()}`}
                    </p>
                  </div>
                  <Button size="sm" variant="ghost" onClick={() => remove(a.announcementId)} className="text-red-500 hover:text-red-700 hover:bg-red-50">
                    Delete
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
