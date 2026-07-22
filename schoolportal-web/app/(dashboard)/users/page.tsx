"use client";
import { useRef, useState } from "react";
import { Upload, Users as UsersIcon } from "lucide-react";
import { api, type User, type ImportCsvResult } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { useUsersList, useCreateUser, useUpdateUser } from "@/features/users/api/hooks";
import { useToastStore } from "@/stores/toast.store";

const ROLE_COLORS: Record<string, string> = {
  Admin:   "bg-purple-100 text-purple-800",
  Teacher: "bg-blue-100 text-blue-800",
  Student: "bg-green-100 text-green-800",
  Parent:  "bg-orange-100 text-orange-800",
};

const ROLE_DOT: Record<string, string> = {
  Admin:   "bg-purple-500",
  Teacher: "bg-blue-500",
  Student: "bg-green-500",
  Parent:  "bg-orange-500",
};

const AVATAR_BG: Record<string, string> = {
  Admin:   "#7c3aed",
  Teacher: "#2563eb",
  Student: "#16a34a",
  Parent:  "#ea580c",
};

export default function UsersPage() {
  const toast = useToastStore();

  const [q,          setQ]          = useState("");
  const [role,       setRole]       = useState("");
  const [page,       setPage]       = useState(1);
  const [showAdd,    setShowAdd]    = useState(false);
  const [editUser,   setEditUser]   = useState<User | null>(null);
  const [showImport, setShowImport] = useState(false);

  const { data, isLoading, isFetching } = useUsersList({ q: q || undefined, role: role || undefined, page, pageSize: 20 });
  const updateMut = useUpdateUser();

  const users = data?.items ?? [];
  const total = data?.total ?? 0;

  async function toggleActive(user: User) {
    try {
      await updateMut.mutateAsync({
        id: user.userId,
        body: { firstName: user.firstName, lastName: user.lastName, role: user.role, isActive: !user.isActive },
      });
      toast.success(user.isActive ? "User deactivated" : "User activated", "");
    } catch (err: unknown) {
      toast.error("Failed", err instanceof Error ? err.message : "Please try again");
    }
  }

  return (
    <div className="p-4 md:p-6 lg:p-8">
      <div className="mb-5 md:mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl md:text-2xl font-semibold text-text-primary tracking-tight">Users</h1>
          <p className="text-xs md:text-sm text-text-secondary mt-0.5">{total} total users</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setShowImport(true)} className="hidden sm:flex items-center gap-2">
            <Upload className="h-4 w-4" />
            Import CSV
          </Button>
          <Button onClick={() => setShowAdd(true)}>+ Add User</Button>
        </div>
      </div>

      {/* Filters */}
      <div className="mb-4 flex flex-wrap gap-3">
        <Input placeholder="Search name or email…" value={q}
          onChange={e => { setQ(e.target.value); setPage(1); }}
          className="w-full sm:max-w-xs" />
        <select value={role} onChange={e => { setRole(e.target.value); setPage(1); }}
          className="rounded-xl border border-border bg-surface-card px-3 py-2.5 text-sm font-medium text-text-primary focus:outline-none focus:ring-2 focus:ring-primary shadow-sm min-h-[44px]">
          <option value="">All roles</option>
          {["Admin", "Teacher", "Student", "Parent"].map(r => <option key={r}>{r}</option>)}
        </select>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="h-16 animate-pulse rounded-2xl bg-surface-subtle" />
          ))}
        </div>
      ) : (
        <>
          {/* Mobile card list */}
          <div className={`md:hidden space-y-2 ${isFetching && !isLoading ? "opacity-60" : ""} transition-opacity`}>
            {users.length === 0 ? (
              <div className="flex flex-col items-center py-16 text-text-muted">
                <UsersIcon className="h-10 w-10 text-text-muted mb-3" />
                <p className="text-sm">No users found</p>
              </div>
            ) : users.map(u => (
              <div key={u.userId}
                className="flex items-center gap-3 rounded-xl border border-border bg-surface-card px-4 py-3">
                <div className="h-10 w-10 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0"
                  style={{ backgroundColor: AVATAR_BG[u.role] ?? "#6b7280" }}>
                  {u.firstName[0]}{u.lastName[0]}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-text-primary truncate text-sm">{u.firstName} {u.lastName}</span>
                    <span className={`shrink-0 inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${ROLE_COLORS[u.role] ?? "bg-gray-100 text-gray-700"}`}>
                      {u.role}
                    </span>
                  </div>
                  <p className="text-xs text-text-muted truncate">{u.email}</p>
                  <div className="flex items-center gap-1 text-xs mt-0.5">
                    <span className={`h-1.5 w-1.5 rounded-full ${u.isActive ? "bg-success-500" : "bg-text-muted"}`} />
                    <span className={u.isActive ? "text-success-700" : "text-text-muted"}>
                      {u.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <button onClick={() => setEditUser(u)}
                    className="rounded-lg border border-border px-2.5 py-1.5 text-xs font-medium text-text-secondary hover:border-primary-300 hover:text-primary transition-colors min-h-[36px]">
                    Edit
                  </button>
                  <button onClick={() => toggleActive(u)}
                    className={`rounded-lg border px-2.5 py-1.5 text-xs font-medium transition-colors min-h-[36px] ${u.isActive ? "border-danger-500/30 text-danger-500 hover:bg-danger-100" : "border-success-500/30 text-success-700 hover:bg-success-100"}`}>
                    {u.isActive ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </div>
            ))}
          </div>

          {/* Desktop table */}
          <Card className={`hidden md:block ${isFetching && !isLoading ? "opacity-60" : ""} transition-opacity`}>
            <CardContent className="p-0">
              <table className="w-full text-sm">
                <thead className="border-b border-border bg-surface-subtle">
                  <tr>
                    {["Name", "Email", "Role", "Status", "Last Login", "Actions"].map(h => (
                      <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-text-muted uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {users.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-6 py-16 text-center text-text-muted">
                        <div className="flex justify-center mb-3">
                          <UsersIcon className="h-10 w-10 text-text-muted" />
                        </div>
                        <p>No users found</p>
                      </td>
                    </tr>
                  ) : users.map(u => (
                    <tr key={u.userId} className="hover:bg-surface-subtle transition-colors">
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="h-8 w-8 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0"
                            style={{ backgroundColor: AVATAR_BG[u.role] ?? "#6b7280" }}>
                            {u.firstName[0]}{u.lastName[0]}
                          </div>
                          <span className="font-medium text-text-primary">{u.firstName} {u.lastName}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-text-secondary">{u.email}</td>
                      <td className="px-6 py-4">
                        <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ${ROLE_COLORS[u.role] ?? "bg-gray-100 text-gray-700"}`}>
                          <span className={`h-1.5 w-1.5 rounded-full ${ROLE_DOT[u.role] ?? "bg-gray-400"}`} />
                          {u.role}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`inline-flex items-center gap-1 text-xs font-medium ${u.isActive ? "text-success-700" : "text-text-muted"}`}>
                          <span className={`h-1.5 w-1.5 rounded-full ${u.isActive ? "bg-success-500" : "bg-text-muted"}`} />
                          {u.isActive ? "Active" : "Inactive"}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-text-muted text-xs">
                        {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : "Never"}
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          <button onClick={() => setEditUser(u)}
                            className="text-xs text-primary hover:text-primary-800 font-medium hover:underline">
                            Edit
                          </button>
                          <span className="text-text-muted">·</span>
                          <button onClick={() => toggleActive(u)}
                            className={`text-xs font-medium hover:underline ${u.isActive ? "text-danger-500 hover:text-danger-700" : "text-success-700 hover:text-success-500"}`}>
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
        </>
      )}

      {total > 20 && (
        <div className="mt-4 flex items-center justify-between">
          <p className="text-sm text-text-secondary">
            Showing {(page - 1) * 20 + 1}–{Math.min(page * 20, total)} of {total}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page === 1} onClick={() => setPage(p => p - 1)}>← Previous</Button>
            <Button variant="outline" size="sm" disabled={page * 20 >= total} onClick={() => setPage(p => p + 1)}>Next →</Button>
          </div>
        </div>
      )}

      {showAdd    && <UserModal onClose={() => setShowAdd(false)} onSaved={() => toast.success("User created", "")} />}
      {editUser   && <UserModal user={editUser} onClose={() => setEditUser(null)} onSaved={() => toast.success("User updated", "")} />}
      {showImport && <ImportCsvModal onClose={() => setShowImport(false)} />}
    </div>
  );
}

function UserModal({ user, onClose, onSaved }: { user?: User; onClose: () => void; onSaved: () => void }) {
  const isEdit    = !!user;
  const createMut = useCreateUser();
  const updateMut = useUpdateUser();
  const isSaving  = createMut.isPending || updateMut.isPending;

  const [form, setForm] = useState({
    firstName: user?.firstName ?? "",
    lastName:  user?.lastName  ?? "",
    email:     user?.email     ?? "",
    password:  "",
    role:      user?.role      ?? "Student",
    isActive:  user?.isActive  ?? true,
  });
  const [error, setError] = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      if (isEdit) {
        await updateMut.mutateAsync({ id: user!.userId, body: { firstName: form.firstName, lastName: form.lastName, role: form.role, isActive: form.isActive } });
      } else {
        await createMut.mutateAsync({ email: form.email, password: form.password, firstName: form.firstName, lastName: form.lastName, role: form.role });
      }
      onSaved();
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Save failed");
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-surface-card shadow-2xl">
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h2 className="text-lg font-semibold text-text-primary">{isEdit ? "Edit User" : "Add User"}</h2>
          <button onClick={onClose} className="rounded-full p-1 text-text-muted hover:bg-surface-subtle hover:text-text-secondary transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">First name</label>
              <Input value={form.firstName} onChange={e => setForm(f => ({ ...f, firstName: e.target.value }))} required />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">Last name</label>
              <Input value={form.lastName} onChange={e => setForm(f => ({ ...f, lastName: e.target.value }))} required />
            </div>
          </div>
          {!isEdit && (
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">Email</label>
              <Input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} required />
            </div>
          )}
          {!isEdit && (
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">Password</label>
              <Input type="password" placeholder="Min 8 characters" value={form.password}
                onChange={e => setForm(f => ({ ...f, password: e.target.value }))} required minLength={8} />
            </div>
          )}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-text-primary">Role</label>
            <select value={form.role} onChange={e => setForm(f => ({ ...f, role: e.target.value }))}
              className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
              {["Admin", "Teacher", "Student", "Parent"].map(r => <option key={r}>{r}</option>)}
            </select>
          </div>
          {isEdit && (
            <label className="flex items-center gap-3 cursor-pointer">
              <div onClick={() => setForm(f => ({ ...f, isActive: !f.isActive }))}
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${form.isActive ? "bg-primary" : "bg-border"}`}>
                <span className={`inline-block h-4 w-4 transform rounded-full bg-surface-card transition-transform shadow ${form.isActive ? "translate-x-6" : "translate-x-1"}`} />
              </div>
              <span className="text-sm font-medium text-text-primary">Active account</span>
            </label>
          )}
          <div className="flex gap-3 pt-2">
            <Button type="submit" className="flex-1" loading={isSaving}>
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
  const fileRef     = useRef<HTMLInputElement>(null);
  const [file,      setFile]      = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [result,    setResult]    = useState<ImportCsvResult | null>(null);
  const [error,     setError]     = useState("");

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
      <div className="w-full max-w-lg rounded-2xl bg-surface-card shadow-2xl">
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h2 className="text-lg font-semibold text-text-primary">Import Users via CSV</h2>
          <button onClick={onClose} className="rounded-full p-1 text-text-muted hover:bg-surface-subtle hover:text-text-secondary transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <div className="p-6 space-y-5">
          <div className="rounded-lg bg-primary-50 border border-primary-100 px-4 py-3 flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-medium text-primary-800">Download template</p>
              <p className="text-xs text-primary mt-0.5">CSV with columns: FirstName, LastName, Email, Role</p>
            </div>
            <a href={`${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128"}/api/users/import-csv`}
              download="users_import_template.csv"
              className="shrink-0 text-sm font-medium text-primary hover:text-primary-800 underline underline-offset-2">
              Download
            </a>
          </div>
          <div>
            <label className="text-sm font-medium text-text-primary block mb-1.5">Upload CSV file</label>
            <div className="relative flex items-center gap-3 rounded-lg border-2 border-dashed border-border px-4 py-5 hover:border-primary-300 transition-colors cursor-pointer"
              onClick={() => fileRef.current?.click()}>
              <Upload className="h-5 w-5 text-text-muted shrink-0" />
              <div className="min-w-0 flex-1">
                {file ? (
                  <p className="text-sm text-text-primary truncate">{file.name}</p>
                ) : (
                  <p className="text-sm text-text-muted">Click to choose a .csv file</p>
                )}
              </div>
              <input ref={fileRef} type="file" accept=".csv,text/csv" className="sr-only"
                onChange={e => setFile(e.target.files?.[0] ?? null)} />
            </div>
          </div>
          {error && <div className="rounded-lg bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}
          {result && (
            <div className="rounded-lg border border-border overflow-hidden">
              <div className={`px-4 py-3 flex items-center gap-2 ${result.failed.length === 0 ? "bg-success-100 border-b border-success-500/20" : "bg-warning-100 border-b border-warning-500/20"}`}>
                <span className={`text-sm font-semibold ${result.failed.length === 0 ? "text-success-700" : "text-warning-700"}`}>
                  {result.created} user{result.created !== 1 ? "s" : ""} created
                  {result.failed.length > 0 && `, ${result.failed.length} failed`}
                </span>
              </div>
              {result.failed.length > 0 && (
                <div className="max-h-40 overflow-y-auto divide-y divide-border">
                  {result.failed.map((f, i) => (
                    <div key={i} className="px-4 py-2.5 text-sm">
                      <span className="font-medium text-text-secondary">Row {f.row}:</span>{" "}
                      <span className="text-danger-700">{f.reason}</span>
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
