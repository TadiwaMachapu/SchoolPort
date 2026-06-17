"use client";
import { useEffect, useMemo, useState } from "react";
import { X, Search, Check } from "lucide-react";
import { api, type PositionCatalogueItem, type DirectoryUser, type Subject, type ScopeInput } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

// ScopeType enum (server): None=0, Subject=1, Phase=2, Grade=3, Class=4, Activity=5.
const SCOPE = { None: 0, Subject: 1, Phase: 2, Grade: 3, Class: 4, Activity: 5 } as const;
const GRADES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

function toIso(date: string): string | null {
  if (!date) return null;
  const d = new Date(date + "T00:00:00");
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}
function todayInput(): string { return new Date().toISOString().slice(0, 10); }
function plusDaysInput(days: number): string { return new Date(Date.now() + days * 86400000).toISOString().slice(0, 10); }

export function AssignPositionModal({
  catalogue, onClose, onAssigned,
}: {
  catalogue: PositionCatalogueItem[];
  onClose: () => void;
  onAssigned: () => void;
}) {
  const [q, setQ] = useState("");
  const [results, setResults] = useState<DirectoryUser[]>([]);
  const [user, setUser] = useState<DirectoryUser | null>(null);
  const [positionKey, setPositionKey] = useState("");
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [subjectIds, setSubjectIds] = useState<string[]>([]);
  const [grades, setGrades] = useState<string[]>([]);
  const [phase, setPhase] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(todayInput());
  const [effectiveTo, setEffectiveTo] = useState("");
  const [consentRecordId, setConsentRecordId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pos = useMemo(() => catalogue.find((c) => c.key === positionKey) ?? null, [catalogue, positionKey]);
  const timeLimited = !!pos && (pos.isExternal || pos.isSystem || pos.requiresTimeLimit);

  useEffect(() => { api.subjects.list().then(setSubjects).catch(() => {}); }, []);

  // Debounced directory search.
  useEffect(() => {
    if (q.trim().length < 2) { setResults([]); return; }
    const t = setTimeout(() => { api.users.directory(q).then(setResults).catch(() => setResults([])); }, 250);
    return () => clearTimeout(t);
  }, [q]);

  // When a time-limited position is picked, prefill a sensible expiry default.
  useEffect(() => {
    if (!pos) return;
    if (timeLimited && !effectiveTo) {
      const days = pos.defaultDurationHours ? Math.max(1, Math.round(pos.defaultDurationHours / 24)) : 30;
      setEffectiveTo(plusDaysInput(days));
    }
    if (!timeLimited) setEffectiveTo("");
    setSubjectIds([]); setGrades([]); setPhase("");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [positionKey]);

  const grouped = useMemo(() => {
    const m = new Map<string, PositionCatalogueItem[]>();
    for (const c of catalogue) { (m.get(c.category) ?? m.set(c.category, []).get(c.category)!).push(c); }
    return [...m.entries()];
  }, [catalogue]);

  function toggle<T>(arr: T[], v: T): T[] { return arr.includes(v) ? arr.filter((x) => x !== v) : [...arr, v]; }

  async function submit() {
    setError(null);
    if (!user) return setError("Select a staff member.");
    if (!pos) return setError("Select a position.");
    if (timeLimited && !effectiveTo) return setError(`${pos.displayName} is time-limited — set an end date.`);
    if (pos.requiresConsent && !consentRecordId.trim()) return setError(`${pos.displayName} requires a consent record id.`);

    let scopes: ScopeInput[] = [];
    if (pos.scopeType === SCOPE.Subject) {
      if (subjectIds.length === 0) return setError("Pick at least one subject for this position.");
      scopes = subjectIds.map((id) => ({ scopeRefId: id }));
    } else if (pos.scopeType === SCOPE.Grade) {
      if (grades.length === 0) return setError("Pick at least one grade for this position.");
      scopes = grades.map((g) => ({ scopeValue: g }));
    } else if (pos.scopeType === SCOPE.Phase) {
      if (!phase) return setError("Pick a phase (FET or Senior Phase).");
      scopes = [{ scopeValue: phase }];
    }

    setBusy(true);
    try {
      await api.positions.assign({
        userId: user.userId,
        positionKey: pos.key,
        effectiveFrom: toIso(effectiveFrom),
        effectiveTo: toIso(effectiveTo),
        consentRecordId: consentRecordId.trim() || null,
        scopes,
      });
      onAssigned();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Assignment failed.");
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-0 backdrop-blur-sm sm:items-center sm:p-4" onClick={onClose}>
      <div className="flex max-h-[92vh] w-full max-w-lg flex-col rounded-t-2xl bg-white shadow-2xl sm:rounded-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <h3 className="text-base font-semibold text-gray-900">Assign position</h3>
          <button onClick={onClose} className="rounded-md p-1 text-gray-400 hover:bg-gray-100"><X className="h-5 w-5" /></button>
        </div>

        <div className="space-y-4 overflow-y-auto px-5 py-4">
          {/* User search */}
          <div>
            <label className="text-xs font-semibold text-gray-600">Staff member</label>
            {user ? (
              <div className="mt-1 flex items-center justify-between rounded-lg border border-gray-200 px-3 py-2">
                <span className="text-sm text-gray-900">{user.firstName} {user.lastName} <span className="text-gray-400">· {user.email}</span></span>
                <button className="text-xs text-blue-600 hover:underline" onClick={() => { setUser(null); setResults([]); setQ(""); }}>Change</button>
              </div>
            ) : (
              <>
                <div className="relative mt-1">
                  <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-gray-400" />
                  <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search name or email…"
                    className="w-full rounded-lg border border-gray-200 py-2 pl-8 pr-3 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500" />
                </div>
                {results.length > 0 && (
                  <div className="mt-1 max-h-40 overflow-y-auto rounded-lg border border-gray-100">
                    {results.map((r) => (
                      <button key={r.userId} onClick={() => setUser(r)}
                        className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-gray-50">
                        <span>{r.firstName} {r.lastName}</span><span className="text-xs text-gray-400">{r.email}</span>
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>

          {/* Position */}
          <div>
            <label className="text-xs font-semibold text-gray-600">Position</label>
            <select value={positionKey} onChange={(e) => setPositionKey(e.target.value)}
              className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500">
              <option value="">Select a position…</option>
              {grouped.map(([cat, items]) => (
                <optgroup key={cat} label={cat}>
                  {items.map((c) => <option key={c.key} value={c.key}>{c.displayName}{c.inPreset ? "" : " (beyond preset)"}</option>)}
                </optgroup>
              ))}
            </select>
            {pos && !pos.inPreset && <p className="mt-1 text-xs text-amber-600">Not in this school’s size preset — allowed, just not a default.</p>}
          </div>

          {/* Scope picker */}
          {pos?.scopeType === SCOPE.Subject && (
            <div>
              <label className="text-xs font-semibold text-gray-600">Subject scope</label>
              <div className="mt-1 flex max-h-40 flex-wrap gap-1.5 overflow-y-auto">
                {subjects.map((s) => (
                  <button key={s.subjectId} onClick={() => setSubjectIds((a) => toggle(a, s.subjectId))}
                    className={cn("rounded-md px-2.5 py-1 text-xs ring-1", subjectIds.includes(s.subjectId) ? "bg-blue-600 text-white ring-blue-600" : "bg-white text-gray-600 ring-gray-200 hover:bg-gray-50")}>
                    {subjectIds.includes(s.subjectId) && <Check className="mr-1 inline h-3 w-3" />}{s.name}
                  </button>
                ))}
                {subjects.length === 0 && <span className="text-xs text-gray-400">No subjects yet — seed CAPS subjects first.</span>}
              </div>
            </div>
          )}
          {pos?.scopeType === SCOPE.Grade && (
            <div>
              <label className="text-xs font-semibold text-gray-600">Grade scope</label>
              <div className="mt-1 flex flex-wrap gap-1.5">
                {GRADES.map((g) => (
                  <button key={g} onClick={() => setGrades((a) => toggle(a, String(g)))}
                    className={cn("rounded-md px-2.5 py-1 text-xs ring-1", grades.includes(String(g)) ? "bg-blue-600 text-white ring-blue-600" : "bg-white text-gray-600 ring-gray-200 hover:bg-gray-50")}>
                    Gr {g}
                  </button>
                ))}
              </div>
            </div>
          )}
          {pos?.scopeType === SCOPE.Phase && (
            <div>
              <label className="text-xs font-semibold text-gray-600">Phase scope</label>
              <div className="mt-1 flex gap-1.5">
                {[["FET", "FET (Gr 10–12)"], ["SeniorPhase", "Senior Phase (Gr 7–9)"]].map(([val, lbl]) => (
                  <button key={val} onClick={() => setPhase(val)}
                    className={cn("rounded-md px-3 py-1 text-xs ring-1", phase === val ? "bg-blue-600 text-white ring-blue-600" : "bg-white text-gray-600 ring-gray-200 hover:bg-gray-50")}>{lbl}</button>
                ))}
              </div>
            </div>
          )}
          {pos && (pos.scopeType === SCOPE.Class || pos.scopeType === SCOPE.Activity) && (
            <p className="rounded-lg bg-gray-50 px-3 py-2 text-xs text-gray-500">
              {pos.displayName} is scoped operationally — assign the teacher to classes/subjects (or activities) in the Assign step / academics, not here.
            </p>
          )}

          {/* Effective dates */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-semibold text-gray-600">Effective from</label>
              <input type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)}
                className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500" />
            </div>
            <div>
              <label className="text-xs font-semibold text-gray-600">Effective to {timeLimited && <span className="text-rose-500">*</span>}</label>
              <input type="date" value={effectiveTo} onChange={(e) => setEffectiveTo(e.target.value)}
                className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500" />
            </div>
          </div>
          {timeLimited && <p className="-mt-2 text-xs text-amber-600">{pos?.displayName} is external/system — an end date is required (cannot be permanent).</p>}
          {pos?.requiresConsent && (
            <div>
              <label className="text-xs font-semibold text-gray-600">Consent record id <span className="text-rose-500">*</span></label>
              <input value={consentRecordId} onChange={(e) => setConsentRecordId(e.target.value)} placeholder="ConsentRecord GUID"
                className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500" />
            </div>
          )}

          {error && <p className="text-sm text-rose-600">{error}</p>}
        </div>

        <div className="flex justify-end gap-2 border-t border-gray-100 px-5 py-4">
          <Button variant="ghost" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button onClick={submit} loading={busy}>Assign</Button>
        </div>
      </div>
    </div>
  );
}
