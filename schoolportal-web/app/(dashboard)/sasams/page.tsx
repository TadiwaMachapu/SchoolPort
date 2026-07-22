"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type Term } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { FolderDown, Download, Loader2 } from "lucide-react";

function downloadWithAuth(url: string, token: string | null) {
  const a = document.createElement("a");
  a.href = token ? `${url}${url.includes("?") ? "&" : "?"}_t=${encodeURIComponent(token)}` : url;
  a.style.display = "none";
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}

export default function SaSamsPage() {
  const router = useRouter();
  const hasSaSams = useFeature("saSamsExport");
  const [terms,   setTerms]   = useState<Term[]>([]);
  const [termId,  setTermId]  = useState("");
  const [loading, setLoading] = useState<string | null>(null);

  useEffect(() => {
    api.terms.list().then(ts => {
      setTerms(ts);
      const current = ts.find(t => t.isCurrent);
      if (current) setTermId(current.termId);
      else if (ts.length) setTermId(ts[0].termId);
    }).catch(() => {});
  }, []);

  if (!hasSaSams) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <FolderDown className="h-12 w-12 text-text-muted mb-4" />
        <h2 className="text-lg font-semibold text-text-primary">SA-SAMS Export not enabled</h2>
        <p className="text-sm text-text-muted mt-1">Enable SA-SAMS Export in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-primary hover:underline">Go to Settings</button>
      </div>
    );
  }

  function download(type: "learners" | "attendance" | "results") {
    setLoading(type);
    const token = typeof document !== "undefined"
      ? (() => { const m = document.cookie.match(/(?:^|; )sp_token=([^;]*)/); return m ? decodeURIComponent(m[1]) : null; })()
      : null;

    let url = "";
    if (type === "learners")    url = api.sasams.exportLearners(termId || undefined);
    if (type === "attendance")  url = api.sasams.exportAttendance(termId || undefined);
    if (type === "results")     url = api.sasams.exportResults(termId || undefined);

    // For CSV downloads we need to redirect with auth — use a fetch+blob approach
    const fetchUrl = token ? url : url;
    fetch(fetchUrl, token ? { headers: { Authorization: `Bearer ${token}` } } : {})
      .then(r => r.blob())
      .then(blob => {
        const objectUrl = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = objectUrl;
        a.download = `sa-sams-${type}-${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(objectUrl);
      })
      .catch(() => {})
      .finally(() => setLoading(null));
  }

  const exports = [
    {
      id:    "learners" as const,
      label: "Learner Register",
      desc:  "Full list of enrolled learners with student number, name, date of birth, grade, and class.",
      fields:"LearnerNo, Surname, FirstName, DateOfBirth, Grade, ClassName, Email",
    },
    {
      id:    "attendance" as const,
      label: "Attendance Report",
      desc:  "Attendance records for the selected term. Status: P (Present), A (Absent), L (Late).",
      fields:"LearnerNo, Surname, FirstName, Date, Status",
    },
    {
      id:    "results" as const,
      label: "Assessment Results",
      desc:  "All graded assessment marks for the selected term across all subjects.",
      fields:"LearnerNo, Surname, FirstName, ClassName, Subject, Assessment, MaxMark, Mark, Percentage, Date",
    },
  ];

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">SA-SAMS Export</h1>
        <p className="text-sm text-text-secondary mt-1">Download school data in SA-SAMS-compatible CSV format for submission to the Department of Education.</p>
      </div>

      <div className="rounded-xl bg-warning-100 border border-warning-500/20 px-5 py-3 text-sm text-warning-700">
        <strong>Note:</strong> These exports follow the SA-SAMS field naming conventions. Verify against your provincial DoE requirements before submission.
      </div>

      {/* Term selector */}
      <div className="flex items-end gap-3">
        <div className="space-y-1">
          <label className="text-xs font-medium text-text-secondary">Filter by term</label>
          <select value={termId} onChange={e => setTermId(e.target.value)}
            className="rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
            <option value="">All terms</option>
            {terms.map(t => (
              <option key={t.termId} value={t.termId}>
                {t.isCurrent ? "★ " : ""}Term {t.termNumber} {t.year}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Export cards */}
      <div className="space-y-4">
        {exports.map(ex => (
          <div key={ex.id} className="rounded-xl bg-surface-card border border-border shadow-sm p-5 flex items-start justify-between gap-4">
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <FolderDown className="h-5 w-5 text-primary shrink-0" />
                <p className="font-semibold text-text-primary">{ex.label}</p>
              </div>
              <p className="text-sm text-text-secondary">{ex.desc}</p>
              <p className="text-xs text-text-muted mt-2 font-mono">{ex.fields}</p>
            </div>
            <button
              onClick={() => download(ex.id)}
              disabled={loading === ex.id}
              className="flex items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 text-primary px-4 py-2 text-sm font-medium hover:bg-primary-100 transition-colors disabled:opacity-50 shrink-0"
            >
              {loading === ex.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
              Download CSV
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
