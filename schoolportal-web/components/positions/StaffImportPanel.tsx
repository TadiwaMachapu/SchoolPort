"use client";
import { useRef, useState } from "react";
import { X, Upload, Download, CheckCircle2, AlertTriangle } from "lucide-react";
import { api, type StaffImportResult } from "@/lib/api";
import { Button } from "@/components/ui/button";

// Sprint 1.5.0 Step 9 — bulk staff import with positions + scopes. Header:
// name,email,identity,positions,scopes  (scopes field must be quoted as it contains commas).
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

export function StaffImportPanel({ onClose, onImported }: { onClose: () => void; onImported: () => void }) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<StaffImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  function downloadTemplate() {
    const token = document.cookie.match(/(?:^|; )sp_token=([^;]*)/)?.[1];
    fetch(`${API_URL}/api/users/import-staff-csv`, { headers: token ? { Authorization: `Bearer ${decodeURIComponent(token)}` } : {} })
      .then((r) => r.blob())
      .then((b) => {
        const url = URL.createObjectURL(b);
        const a = document.createElement("a");
        a.href = url; a.download = "staff_import_template.csv"; a.click();
        URL.revokeObjectURL(url);
      });
  }

  async function upload() {
    if (!file) return;
    setBusy(true); setError(null); setResult(null);
    try {
      const res = await api.users.importStaffCsv(file);
      setResult(res);
      if (res.created > 0) onImported();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Import failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-0 backdrop-blur-sm sm:items-center sm:p-4" onClick={onClose}>
      <div className="w-full max-w-lg rounded-t-2xl bg-surface-card shadow-2xl sm:rounded-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-border px-5 py-4">
          <h3 className="text-base font-semibold text-text-primary">Bulk import staff</h3>
          <button onClick={onClose} className="rounded-md p-1 text-text-muted hover:bg-surface-subtle"><X className="h-5 w-5" /></button>
        </div>

        <div className="space-y-4 px-5 py-4">
          <p className="text-sm text-text-secondary">
            CSV columns: <code className="rounded bg-surface-subtle px-1 text-xs">name,email,identity,positions,scopes</code>.
            Positions are <code className="rounded bg-surface-subtle px-1 text-xs">;</code>-separated; scopes map a position to its scope,
            e.g. <code className="rounded bg-surface-subtle px-1 text-xs">HOD:Mathematics;GradeHead:10</code> (quote the scopes cell — it can contain commas).
          </p>

          <Button variant="outline" size="sm" onClick={downloadTemplate}><Download className="mr-1.5 h-4 w-4" /> Download template</Button>

          <div className="rounded-lg border-2 border-dashed border-border p-4 text-center">
            <input ref={fileRef} type="file" accept=".csv" className="hidden"
              onChange={(e) => { setFile(e.target.files?.[0] ?? null); setResult(null); }} />
            <button onClick={() => fileRef.current?.click()} className="text-sm font-medium text-primary hover:underline">
              {file ? file.name : "Choose a CSV file"}
            </button>
          </div>

          {error && <p className="text-sm text-danger-700">{error}</p>}

          {result && (
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm font-medium text-success-700">
                <CheckCircle2 className="h-4 w-4" /> {result.created} staff imported
              </div>
              {result.failed.length > 0 && (
                <div className="rounded-lg border border-warning-500/30 bg-warning-100 p-3">
                  <div className="mb-1 flex items-center gap-1.5 text-sm font-medium text-warning-700">
                    <AlertTriangle className="h-4 w-4" /> {result.failed.length} row{result.failed.length > 1 ? "s" : ""} skipped
                  </div>
                  <ul className="max-h-40 space-y-0.5 overflow-y-auto text-xs text-warning-700">
                    {result.failed.map((f, i) => <li key={i}>Row {f.row}: {f.reason}</li>)}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-border px-5 py-4">
          <Button variant="ghost" onClick={onClose} disabled={busy}>Close</Button>
          <Button onClick={upload} loading={busy} disabled={!file}><Upload className="mr-1.5 h-4 w-4" /> Import</Button>
        </div>
      </div>
    </div>
  );
}
