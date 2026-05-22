"use client";
import { useEffect, useRef, useState } from "react";
import { Upload, Users as UsersIcon } from "lucide-react";
import { api, type User, type ImportCsvResult } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";

const ROLE_BADGE: Record<string, "default" | "success" | "warning" | "outline"> = {
  Admin:   "outline",
  Teacher: "default",
  Student: "success",
  Parent:  "warning",
};

const ROLE_COLORS: Record<string, string> = {
  Admin:   "bg-purple-100 text-purple-800",
  Teacher: "bg-blue-100 text-blue-800",
  Student: "bg-green-100 text-green-800",
  Parent:  "bg-orange-100 text-orange-800",
};

export default function UsersPage() {
  const [users,   setUsers]   = useState<User[]>([]);
  const [total,   setTotal]   = useState(0);
  const [q,       setQ]       = useState("");
  const [role,    setRole]    = useState("");
  const [page,    setPage]    = useState(1);
  const [loading, setLoading] = useState(true);
  const [showAdd,    setShowAdd]    = useState(false);
  const [editUser,   setEditUser]   = useState<User | null>(null);
  const [showImport, setShowImport] = useState(false);

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
      firstName: user.firstName, lastName: user.lastName,
      role: user.role, isActive: !user.isActive,
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
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setShowImport(true)} className="flex items-center gap-2">
            <Upload className="h-4 w-4" />
            Import CSV
          </Button>
          <Button onClick={() => setShowAdd(true)}>+ Add User</Button>
        </div>
      </div>

      <Card className="mb-6">
        <CardContent className="flex flex-wrap gap-3 p-4">
          <Input placeholder="Search name or email…" value={q}
            onChange={e => { setQ(e.target.value); setPage(1); }}
            className="max-w-xs" />
          <select value={role} onChange={e => { setRole(e.target.value); setPage(1); }}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="">All roles</option>
            {["Admin", "Teacher", "Student", "Parent"].map(r => <option key={r}>{r}</option>)}
          </select>
        </CardContent>
      </Card>

      {loading ? (
        <SkeletonTable rows={8} cols={6} />
      ) : (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Name", "Email", "Role", "Status", "Last Login", "Actions"].map(h => (
                    <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {users.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-6 py-16 text-center text-gray-400">
                      <div className="flex justify-center mb-3">
                        <UsersIcon className="h-10 w-10 text-gray-300" />
                      </div>
                      <p>No users found</p>
                    </td>
                  </tr>
                ) : users.map(u => (
                  <tr key={u.userId} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="h-8 w-8 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0"
                          style={{ backgroundColor: u.role === "Admin" ? "#7c3aed" : u.role === "Teacher" ? "#2563eb" : u.role === "Student" ? "#16a34a" : "#ea580c" }}>
                          {u.firstName[0]}{u.lastName[0]}
                        </div>
                        <span className="font-medium text-gray-900">{u.firstName} {u.lastName}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-gray-500">{u.email}</td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${ROLE_COLORS[u.role] ?? "bg-gray-100 text-gray-700"}`}>
                        {u.role}
                      </span>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center gap-1 text-xs font-medium ${u.isActive ? "text-green-700" : "text-gray-400"}`}>
                        <span className={`h-1.5 w-1.5 rounded-full ${u.isActive ? "bg-green-500" : "bg-gray-400"}`} />
                        {u.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-gray-400 text-xs">
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : "Never"}
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-2">
                        <button onClick={() => setEditUser(u)}
                          className="text-xs text-blue-600 hover:text-blue-800 font-medium hover:underline">
                          Edit
                        </button>
                        <span className="text-gray-300">·</span>
                        <button onClick={() => toggleActive(u)}
                          className={`text-xs font-medium hover:underline ${u.isActive ? "text-red-500 hover:text-red-700" : "text-green-600 hover:text-green-800"}`}>
                          {u.isActive ? "Deactivate" : "Activate"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      {total > 20 && (
        <div className="mt-4 flex items-center justify-between">
          <p className="text-sm text-gray-500">
            Showing {(page - 1) * 20 + 1}–{Math.min(page * 20, total)} of {total}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>← Previous</Button>
            <Button variant="outline" size="sm" disabled={page * 20 >= total} onClick={() => setPage(p => p + 1)}>Next →</Button>
          </div>
        </div>
      )}

      {showAdd    && <UserModal onClose={() => { setShowAdd(false); load(); }} />}
      {editUser   && <UserModal user={editUser} onClose={() => { setEditUser(null); load(); }} />}
      {showImport && <ImportCsvModal onClose={() => { setShowImport(false); load(); }} />}
    </div>
  );
}

function UserModal({ user, onClose }: { user?: User; onClose: () => void }) {
  const isEdit = !!user;
  const [form, setForm] = useState({
    firstName: user?.firstName ?? "",
    lastName:  user?.lastName  ?? "",
    email:     user?.email     ?? "",
    password:  "",
    role:      user?.role      ?? "Student",
    isActive:  user?.isActive  ?? true,
  });
  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError("");
    try {
      if (isEdit) {
        await api.users.update(user!.userId, {
          firstName: form.firstName, lastName: form.lastName,
          role: form.role, isActive: form.isActive,
        });
      } else {
        await api.users.create({
          email: form.email, password: form.password,
          firstName: form.firstName, lastName: form.lastName, role: form.role,
        });
      }
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">{isEdit ? "Edit User" : "Add User"}</h2>
          <button onClick={onClose} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">First name</label>
              <Input value={form.firstName} onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))} required />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Last name</label>
              <Input value={form.lastName} onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))} required />
            </div>
          </div>
          {!isEdit && (
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Email</label>
              <Input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} required />
            </div>
          )}
          {!isEdit && (
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Password</label>
              <Input type="password" placeholder="Min 8 characters" value={form.password}
                onChange={e => setForm(f => ({ ...f, password: e.target.value }))} required minLength={8} />
            </div>
          )}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Role</label>
            <select value={form.role} onChange={e => setForm(f => ({ ...f, role: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {["Admin", "Teacher", "Student", "Parent"].map(r => <option key={r}>{r}</option>)}
            </select>
          </div>
          {isEdit && (
            <label className="flex items-center gap-3 cursor-pointer">
              <div onClick={() => setForm(f => ({ ...f, isActive: !f.isActive }))}
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${form.isActive ? "bg-blue-600" : "bg-gray-200"}`}>
                <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform shadow ${form.isActive ? "translate-x-6" : "translate-x-1"}`} />
              </div>
              <span className="text-sm font-medium text-gray-700">Active account</span>
            </label>
          )}
          <div className="flex gap-3 pt-2">
            <Button type="submit" className="flex-1" loading={saving}>
              {isEdit ? "Save changes" : "Create user"}
            </Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function ImportCsvModal({ onClose }: { onClose: () => void }) {
  const fileRef   = useRef<HTMLInputElement>(null);
  const [file,     setFile]     = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [result,   setResult]   = useState<ImportCsvResult | null>(null);
  const [error,    setError]    = useState("");

  const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

  async function handleUpload() {
    if (!file) return;
    setUploading(true);
    setError("");
    setResult(null);
    try {
      const res = await api.users.importCsv(file);
      setResult(res);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Upload failed");
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">Import Users via CSV</h2>
          <button onClick={onClose} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="p-6 space-y-5">
          {/* Template download */}
          <div className="rounded-lg bg-blue-50 border border-blue-100 px-4 py-3 flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-blue-900">Download template</p>
              <p className="text-xs text-blue-600 mt-0.5">CSV with columns: FirstName, LastName, Email, Role</p>
            </div>
            <a
              href={`${API_URL}/api/users/import-csv`}
              download="users_import_template.csv"
              className="shrink-0 text-sm font-medium text-blue-700 hover:text-blue-900 underline underline-offset-2"
            >
              Download
            </a>
          </div>

          {/* File picker */}
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1.5">Upload CSV file</label>
            <div
              className="relative flex items-center gap-3 rounded-lg border-2 border-dashed border-gray-200 px-4 py-5 hover:border-blue-300 transition-colors cursor-pointer"
              onClick={() => fileRef.current?.click()}
            >
              <Upload className="h-5 w-5 text-gray-400 shrink-0" />
              <div className="min-w-0 flex-1">
                {file ? (
                  <p className="text-sm text-gray-900 truncate">{file.name}</p>
                ) : (
                  <p className="text-sm text-gray-400">Click to choose a .csv file</p>
                )}
              </div>
              <input
                ref={fileRef}
                type="file"
                accept=".csv,text/csv"
                className="sr-only"
                onChange={e => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
          </div>

          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
          )}

          {/* Results */}
          {result && (
            <div className="rounded-lg border border-gray-200 overflow-hidden">
              <div className={`px-4 py-3 flex items-center gap-2 ${result.failed.length === 0 ? "bg-green-50 border-b border-green-100" : "bg-amber-50 border-b border-amber-100"}`}>
                <span className={`text-sm font-semibold ${result.failed.length === 0 ? "text-green-800" : "text-amber-800"}`}>
                  {result.created} user{result.created !== 1 ? "s" : ""} created
                  {result.failed.length > 0 && `, ${result.failed.length} failed`}
                </span>
              </div>
              {result.failed.length > 0 && (
                <div className="max-h-40 overflow-y-auto divide-y divide-gray-50">
                  {result.failed.map((f, i) => (
                    <div key={i} className="px-4 py-2.5 text-sm">
                      <span className="font-medium text-gray-600">Row {f.row}:</span>{" "}
                      <span className="text-red-600">{f.reason}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          <div className="flex gap-3 pt-1">
            {result ? (
              <Button className="flex-1" onClick={onClose}>Done</Button>
            ) : (
              <Button className="flex-1" onClick={handleUpload} disabled={!file} loading={uploading}>
                Upload & Import
              </Button>
            )}
            <Button variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </div>
      </div>
    </div>
  );
}
