"use client";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { api, type CaptureTaskMarks, type BulkCaptureEntry } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getCapsCode } from "@/lib/utils";
import { AlertTriangle, ArrowLeft, ChevronLeft, ChevronRight, Save, Send } from "lucide-react";

/* Sprint 1.5.2.5 — the mark capture grid. Keyboard-first: Tab/Shift+Tab move across,
   Enter/Shift+Enter move down/up — a teacher capturing 35 marks never needs the mouse.
   Absent ≠ zero: the ABS toggle clears all score inputs and greys the row; the server
   rejects any absent row that still carries a score. */

const CAPS_LEVEL_LABEL: Record<number, string> = {
  7: "Outstanding", 6: "Meritorious", 5: "Substantial",
  4: "Adequate", 3: "Moderate", 2: "Elementary", 1: "Not Achieved",
};

function capsBadgeVariant(level: number): "success" | "warning" | "destructive" {
  if (level >= 5) return "success";
  if (level >= 3) return "warning";
  return "destructive";
}

interface RowDraft {
  studentId: string;
  name: string;
  surname: string;
  studentNumber: string;
  /** Simple-entry mark as typed ("" = pending). */
  score: string;
  isAbsent: boolean;
  /** criteriaId → typed value ("" = pending). */
  criteria: Record<string, string>;
  dirty: boolean;
}

function toDraft(marks: CaptureTaskMarks): RowDraft[] {
  return marks.learners.map((l) => ({
    studentId: l.studentId,
    name: l.name,
    surname: l.surname,
    studentNumber: l.studentNumber,
    score: l.score != null && !marks.hasRubric ? String(l.score) : "",
    isAbsent: l.isAbsent,
    criteria: Object.fromEntries(l.criteriaScores.map((c) => [c.criteriaId, c.score != null ? String(c.score) : ""])),
    dirty: false,
  }));
}

function parseMark(text: string): number | null {
  if (text.trim() === "") return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : null;
}

/** Live total for a row: rubric = sum of entered criteria; simple = the mark. Null = pending. */
function rowTotal(row: RowDraft, marks: CaptureTaskMarks): number | null {
  if (row.isAbsent) return null;
  if (!marks.hasRubric) return parseMark(row.score);
  const entered = marks.criteria.map((c) => parseMark(row.criteria[c.criteriaId] ?? "")).filter((v): v is number => v != null);
  return entered.length > 0 ? entered.reduce((a, b) => a + b, 0) : null;
}

function rowInvalid(row: RowDraft, marks: CaptureTaskMarks): string | null {
  if (row.isAbsent) return null;
  if (!marks.hasRubric) {
    const v = parseMark(row.score);
    if (row.score.trim() !== "" && v == null) return "Not a number";
    if (v != null && v < 0) return "Negative mark";
    if (v != null && v > marks.maxMarks) return `Over /${marks.maxMarks}`;
    return null;
  }
  for (const c of marks.criteria) {
    const text = row.criteria[c.criteriaId] ?? "";
    const v = parseMark(text);
    if (text.trim() !== "" && v == null) return "Not a number";
    if (v != null && v < 0) return "Negative mark";
    if (v != null && v > c.maxMark) return `${c.name}: over /${c.maxMark}`;
  }
  return null;
}

export function CaptureGrid({ marks, onBack, onSaved }: {
  marks: CaptureTaskMarks;
  onBack: () => void;
  onSaved: () => void;
}) {
  const [rows, setRows] = useState<RowDraft[]>(() => toDraft(marks));
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState("");
  const [mobileIndex, setMobileIndex] = useState(0);
  const cellRefs = useRef<Map<string, HTMLInputElement>>(new Map());
  const rowsRef = useRef(rows);
  rowsRef.current = rows;

  const cols = marks.hasRubric ? marks.criteria.length : 1;

  const setRow = useCallback((studentId: string, patch: Partial<RowDraft>) => {
    setRows((prev) => prev.map((r) => (r.studentId === studentId ? { ...r, ...patch, dirty: true } : r)));
  }, []);

  const toggleAbsent = (row: RowDraft) => {
    // Toggling ON clears every score input — absent is not zero, absent is no score at all.
    setRow(row.studentId, row.isAbsent
      ? { isAbsent: false }
      : { isAbsent: true, score: "", criteria: Object.fromEntries(Object.keys(row.criteria).map((k) => [k, ""])) });
  };

  /* Keyboard navigation between cells; grid coordinates are (rowIndex, colIndex).
     Absent rows have no inputs in the DOM, so vertical moves walk PAST them to the next
     row that still has a cell — Enter never dead-ends on an absent learner. */
  const focusCell = (r: number, c: number): boolean => {
    const el = cellRefs.current.get(`${r}:${c}`);
    if (!el) return false;
    el.focus();
    el.select();
    return true;
  };
  const focusVertical = (from: number, c: number, step: -1 | 1) => {
    for (let r = from + step; r >= 0 && r < rows.length; r += step)
      if (focusCell(r, c)) return;
  };
  const onCellKeyDown = (e: React.KeyboardEvent, r: number, c: number) => {
    if (e.key === "Enter") {
      e.preventDefault();
      focusVertical(r, c, e.shiftKey ? -1 : 1);
    } else if (e.key === "Tab") {
      e.preventDefault();
      const next = e.shiftKey ? c - 1 : c + 1;
      if (next < 0) focusVertical(r, cols - 1, -1);
      else if (next >= cols) focusVertical(r, 0, 1);
      else focusCell(r, next);
    }
  };

  const anyDirty = rows.some((r) => r.dirty);
  const firstInvalid = useMemo(() => {
    for (const r of rows) {
      const msg = rowInvalid(r, marks);
      if (msg) return `${r.surname}, ${r.name}: ${msg}`;
    }
    return null;
  }, [rows, marks]);

  const save = useCallback(async (silent = false) => {
    const dirty = rowsRef.current.filter((r) => r.dirty);
    if (dirty.length === 0) return;
    if (dirty.some((r) => rowInvalid(r, marks))) {
      if (!silent) setError("Fix the highlighted marks before saving.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const entries: BulkCaptureEntry[] = dirty.map((r) => ({
        studentId: r.studentId,
        score: r.isAbsent || marks.hasRubric ? null : parseMark(r.score),
        isAbsent: r.isAbsent,
        criteriaScores: marks.hasRubric
          ? marks.criteria.map((c) => ({ criteriaId: c.criteriaId, score: r.isAbsent ? null : parseMark(r.criteria[c.criteriaId] ?? "") }))
          : undefined,
      }));
      const result = await api.gradebook.bulkCapture({ taskId: marks.assignmentId, classSubjectId: marks.classSubjectId, entries });
      setWarnings(result.warnings);
      setSavedAt(new Date());
      setRows((prev) => prev.map((r) => ({ ...r, dirty: false })));
      onSaved();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }, [marks, onSaved]);

  /* Auto-save draft every 60 seconds while there are unsaved changes. */
  useEffect(() => {
    const t = setInterval(() => { void save(true); }, 60_000);
    return () => clearInterval(t);
  }, [save]);

  /* Live class statistics — the stats bar is the Week 3 moderation tool in embryo. */
  const stats = useMemo(() => {
    const totals = rows.map((r) => rowTotal(r, marks)).filter((v): v is number => v != null);
    const pcts = totals.map((t) => (t / marks.maxMarks) * 100);
    const avg = pcts.length > 0 ? pcts.reduce((a, b) => a + b, 0) / pcts.length : null;
    const dist = [1, 2, 3, 4, 5, 6, 7].map((lvl) => ({ lvl, count: pcts.filter((p) => getCapsCode(p) === lvl).length }));
    const absent = rows.filter((r) => r.isAbsent).length;
    return { avg, dist, marked: totals.length, absent, maxCount: Math.max(1, ...dist.map((d) => d.count)) };
  }, [rows, marks]);

  const inputCls = (invalid: boolean) =>
    `w-16 rounded-md border px-2 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500 ` +
    (invalid ? "border-red-400 bg-red-50 text-red-700" : "border-gray-300");

  const renderCapsBadge = (total: number | null) => {
    if (total == null) return <span className="text-gray-300 text-xs">—</span>;
    const pct = (total / marks.maxMarks) * 100;
    const lvl = getCapsCode(pct);
    return (
      <div className="flex flex-col items-center gap-0.5">
        <Badge variant={capsBadgeVariant(lvl)}>L{lvl}</Badge>
        <span className="text-[10px] text-gray-500">{CAPS_LEVEL_LABEL[lvl]}</span>
      </div>
    );
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-start justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <button onClick={onBack} className="rounded-md border border-gray-300 p-2 hover:bg-gray-50" aria-label="Back to tasks">
            <ArrowLeft className="h-4 w-4 text-gray-600" />
          </button>
          <div>
            <h2 className="text-lg font-semibold text-gray-900">{marks.title}</h2>
            <p className="text-xs text-gray-500">
              {marks.taskType}{marks.termNumber ? ` · Term ${marks.termNumber}` : ""} · Total /{marks.maxMarks}
              {marks.sbaWeight != null ? ` · SBA ${marks.sbaWeight}%` : ""}
              {marks.hasRubric ? " · Rubric" : ""}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {savedAt && !anyDirty && (
            <span className="text-xs text-gray-400">Saved {savedAt.toLocaleTimeString()}</span>
          )}
          {anyDirty && <span className="text-xs font-medium text-amber-600">Unsaved changes</span>}
          <Button variant="outline" disabled={saving || !anyDirty} onClick={() => save()} className="flex items-center gap-2">
            <Save className="h-4 w-4" /> {saving ? "Saving…" : "Save Draft"}
          </Button>
          {/* Week 3 — HOD moderation. Built, deliberately disabled. */}
          <span title="HOD review — coming soon">
            <Button disabled className="flex items-center gap-2 opacity-50 cursor-not-allowed">
              <Send className="h-4 w-4" /> Submit for Review
            </Button>
          </span>
        </div>
      </div>

      {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}
      {firstInvalid && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{firstInvalid}</div>}
      {warnings.map((w) => (
        <div key={w} className="flex items-center gap-2 rounded-lg bg-amber-50 border border-amber-200 p-3 text-sm text-amber-800">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {w}
        </div>
      ))}

      {/* Desktop grid */}
      <div className="hidden md:block rounded-xl border border-gray-100 shadow-sm ring-1 ring-gray-100/50 bg-white overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="border-b border-gray-200 bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider sticky left-0 bg-gray-50 min-w-[180px]">Learner</th>
              <th className="px-2 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider w-14">ABS</th>
              {marks.hasRubric ? (
                marks.criteria.map((c) => (
                  <th key={c.criteriaId} className="px-3 py-3 text-center text-xs font-semibold text-gray-500 min-w-[90px]">
                    <div className="truncate max-w-[140px] mx-auto" title={c.name}>{c.name}</div>
                    <div className="text-gray-400 font-normal">/{c.maxMark}</div>
                  </th>
                ))
              ) : (
                <th className="px-3 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[90px]">
                  Mark <span className="text-gray-400 font-normal">/{marks.maxMarks}</span>
                </th>
              )}
              {marks.hasRubric && (
                <th className="px-3 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[70px]">Total</th>
              )}
              <th className="px-3 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[90px]">CAPS Level</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map((row, r) => {
              const total = rowTotal(row, marks);
              const invalidMsg = rowInvalid(row, marks);
              return (
                <tr key={row.studentId}
                  className={`hover:bg-gray-50 ${row.dirty ? "border-l-4 border-l-amber-400" : "border-l-4 border-l-transparent"} ${row.isAbsent ? "bg-gray-50" : ""}`}>
                  <td className="px-4 py-2.5 sticky left-0 bg-white hover:bg-gray-50">
                    <p className={`font-medium ${row.isAbsent ? "text-gray-400" : "text-gray-900"}`}>{row.surname}, {row.name}</p>
                    <p className="text-xs text-gray-400">{row.studentNumber}</p>
                  </td>
                  <td className="px-2 py-2.5 text-center">
                    <input type="checkbox" checked={row.isAbsent} onChange={() => toggleAbsent(row)}
                      className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" aria-label={`Mark ${row.surname} absent`} />
                  </td>
                  {row.isAbsent ? (
                    <td colSpan={marks.hasRubric ? marks.criteria.length + 1 : 1} className="px-3 py-2.5 text-center text-xs font-medium text-gray-400 uppercase tracking-wider">
                      Absent
                    </td>
                  ) : marks.hasRubric ? (
                    <>
                      {marks.criteria.map((c, ci) => (
                        <td key={c.criteriaId} className="px-3 py-2.5 text-center">
                          <input
                            ref={(el) => { if (el) cellRefs.current.set(`${r}:${ci}`, el); else cellRefs.current.delete(`${r}:${ci}`); }}
                            inputMode="decimal"
                            value={row.criteria[c.criteriaId] ?? ""}
                            onChange={(e) => setRow(row.studentId, { criteria: { ...row.criteria, [c.criteriaId]: e.target.value } })}
                            onKeyDown={(e) => onCellKeyDown(e, r, ci)}
                            className={inputCls(!!invalidMsg && parseMark(row.criteria[c.criteriaId] ?? "") != null && (parseMark(row.criteria[c.criteriaId] ?? "")! > c.maxMark || parseMark(row.criteria[c.criteriaId] ?? "")! < 0))}
                          />
                        </td>
                      ))}
                      <td className="px-3 py-2.5 text-center font-semibold text-gray-900">
                        {total != null ? total : <span className="text-gray-300 font-normal">—</span>}
                        <span className="text-xs text-gray-400 font-normal">/{marks.maxMarks}</span>
                      </td>
                    </>
                  ) : (
                    <td className="px-3 py-2.5 text-center">
                      <input
                        ref={(el) => { if (el) cellRefs.current.set(`${r}:0`, el); else cellRefs.current.delete(`${r}:0`); }}
                        inputMode="decimal"
                        value={row.score}
                        onChange={(e) => setRow(row.studentId, { score: e.target.value })}
                        onKeyDown={(e) => onCellKeyDown(e, r, 0)}
                        className={inputCls(!!invalidMsg)}
                      />
                    </td>
                  )}
                  <td className="px-3 py-2.5 text-center">{row.isAbsent ? <span className="text-gray-300 text-xs">—</span> : renderCapsBadge(total)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Mobile: one learner at a time */}
      <div className="md:hidden rounded-xl border border-gray-100 shadow-sm ring-1 ring-gray-100/50 bg-white p-4">
        {rows.length > 0 && (() => {
          const idx = Math.min(mobileIndex, rows.length - 1);
          const row = rows[idx];
          const total = rowTotal(row, marks);
          return (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <button onClick={() => setMobileIndex(Math.max(0, idx - 1))} disabled={idx === 0}
                  className="rounded-md border border-gray-300 p-2 disabled:opacity-30" aria-label="Previous learner">
                  <ChevronLeft className="h-5 w-5" />
                </button>
                <div className="text-center">
                  <p className="text-lg font-semibold text-gray-900">{row.surname}, {row.name}</p>
                  <p className="text-xs text-gray-400">{row.studentNumber} · {idx + 1} of {rows.length}</p>
                </div>
                <button onClick={() => setMobileIndex(Math.min(rows.length - 1, idx + 1))} disabled={idx === rows.length - 1}
                  className="rounded-md border border-gray-300 p-2 disabled:opacity-30" aria-label="Next learner">
                  <ChevronRight className="h-5 w-5" />
                </button>
              </div>

              <label className="flex items-center justify-center gap-2 rounded-lg border border-gray-200 py-2.5 text-sm font-medium text-gray-700">
                <input type="checkbox" checked={row.isAbsent} onChange={() => toggleAbsent(row)}
                  className="h-5 w-5 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
                Absent
              </label>

              {!row.isAbsent && (marks.hasRubric ? (
                <div className="space-y-3">
                  {marks.criteria.map((c) => (
                    <div key={c.criteriaId} className="flex items-center justify-between gap-3">
                      <span className="text-sm text-gray-700 flex-1">{c.name}</span>
                      <div className="flex items-center gap-1">
                        <input inputMode="decimal" value={row.criteria[c.criteriaId] ?? ""}
                          onChange={(e) => setRow(row.studentId, { criteria: { ...row.criteria, [c.criteriaId]: e.target.value } })}
                          className={inputCls(false)} />
                        <span className="text-xs text-gray-400">/{c.maxMark}</span>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex items-center justify-center gap-2">
                  <input inputMode="decimal" value={row.score}
                    onChange={(e) => setRow(row.studentId, { score: e.target.value })}
                    className="w-24 rounded-md border border-gray-300 px-3 py-2 text-xl text-center focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  <span className="text-gray-400">/{marks.maxMarks}</span>
                </div>
              ))}

              <div className="flex items-center justify-between border-t border-gray-100 pt-3">
                <span className="text-sm text-gray-500">Total</span>
                <div className="flex items-center gap-2">
                  <span className="text-lg font-bold text-gray-900">
                    {row.isAbsent ? "Absent" : total != null ? `${total}/${marks.maxMarks}` : "—"}
                  </span>
                  {!row.isAbsent && renderCapsBadge(total)}
                </div>
              </div>
            </div>
          );
        })()}
      </div>

      {/* Class statistics bar — live; this becomes the HOD moderation view's core in Week 3 */}
      <div className="rounded-xl border border-gray-100 shadow-sm ring-1 ring-gray-100/50 bg-white p-4">
        <div className="flex items-center justify-between flex-wrap gap-4">
          <div className="flex items-center gap-6">
            <div>
              <p className="text-2xl font-bold text-blue-600">{stats.avg != null ? `${Math.round(stats.avg)}%` : "—"}</p>
              <p className="text-xs text-gray-500">Class average</p>
            </div>
            <div>
              <p className="text-2xl font-bold text-gray-900">{stats.marked}<span className="text-sm text-gray-400 font-normal">/{rows.length}</span></p>
              <p className="text-xs text-gray-500">Marked</p>
            </div>
            <div>
              <p className="text-2xl font-bold text-gray-500">{stats.absent}</p>
              <p className="text-xs text-gray-500">Absent</p>
            </div>
          </div>
          <div className="flex items-end gap-1.5" aria-label="CAPS level distribution">
            {stats.dist.map(({ lvl, count }) => (
              <div key={lvl} className="flex flex-col items-center gap-0.5">
                <span className="text-[10px] text-gray-500">{count > 0 ? count : ""}</span>
                <div
                  className={`w-6 rounded-t ${lvl >= 5 ? "bg-green-400" : lvl >= 3 ? "bg-amber-400" : "bg-red-400"}`}
                  style={{ height: `${4 + (count / stats.maxCount) * 40}px` }}
                />
                <span className="text-[10px] text-gray-400">L{lvl}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
