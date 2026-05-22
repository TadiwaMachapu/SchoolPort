"use client";
import { useEffect, useState } from "react";
import { api, type User } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const roleBadge: Record<string, "default" | "success" | "warning" | "outline"> = {
  Admin: "destructive" as "default",
  Teacher: "default",
  Student: "success",
  Parent: "warning",
};

export default function UsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [total, setTotal] = useState(0);
  const [q, setQ] = useState("");
  const [role, setRole] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    try {
      const res = await api.users.list({ q: q || undefined, role: role || undefined, page, pageSize: 20 });
      setUsers(res.items);
      setTotal(res.total);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [q, role, page]);

  async function toggleActive(user: User) {
    await api.users.update(user.userId, {
      firstName: user.firstName,
      lastName: user.lastName,
      role: user.role,
      isActive: !user.isActive,
    });
    load();
  }

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Users</h1>
          <p className="text-gray-500 mt-1">{total} total users</p>
        </div>
      </div>

      <Card className="mb-6">
        <CardContent className="flex gap-4 p-4">
          <Input placeholder="Search name or email…" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className="max-w-xs" />
          <select value={role} onChange={(e) => { setRole(e.target.value); setPage(1); }}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="">All roles</option>
            {["Admin", "Teacher", "Student", "Parent"].map((r) => <option key={r}>{r}</option>)}
          </select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex justify-center py-12 text-gray-400">Loading…</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Name", "Email", "Role", "Status", "Last Login", "Actions"].map((h) => (
                    <th key={h} className="px-6 py-3 text-left font-medium text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {users.map((u) => (
                  <tr key={u.userId} className="hover:bg-gray-50">
                    <td className="px-6 py-4 font-medium text-gray-900">{u.firstName} {u.lastName}</td>
                    <td className="px-6 py-4 text-gray-600">{u.email}</td>
                    <td className="px-6 py-4"><Badge variant={roleBadge[u.role] ?? "outline"}>{u.role}</Badge></td>
                    <td className="px-6 py-4"><Badge variant={u.isActive ? "success" : "outline"}>{u.isActive ? "Active" : "Inactive"}</Badge></td>
                    <td className="px-6 py-4 text-gray-500">{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : "Never"}</td>
                    <td className="px-6 py-4">
                      <Button size="sm" variant={u.isActive ? "outline" : "default"} onClick={() => toggleActive(u)}>
                        {u.isActive ? "Deactivate" : "Activate"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      {total > 20 && (
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="outline" size="sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
          <span className="px-3 py-1 text-sm text-gray-600">Page {page}</span>
          <Button variant="outline" size="sm" disabled={page * 20 >= total} onClick={() => setPage(p => p + 1)}>Next</Button>
        </div>
      )}
    </div>
  );
}
