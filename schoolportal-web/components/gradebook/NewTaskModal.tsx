"use client";
import { useState } from "react";
import { api, type CaptureTaskSummary } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Plus, Trash2, X } from "lucide-react";

/* Sprint 1.5.2.5 — task creation. Rubric tasks list their criteria up front (name + max marks,
   total auto-derived); simple tasks take a plain total. PAT is a first-class task type. */

const TASK_TYPES = ["Test", "Assignment", "Quiz", "Project", "Practical", "Exam", "PAT"];

interface CriteriaRow { name: string; maxMark: string }

export function NewTaskModal({ classSubjectId, onClose, onCreated }: {
  classSubjectId: string;
  onClose: () => void;
  onCreated: (task: CaptureTaskSummary) => void;
}) {
  const [title, setTitle] = useState("");
  const [taskType, setTaskType] = useState("Test");
  const [term, setTerm] = useState("");
  const [maxMarks, setMaxMarks] = useState("");
  const [hasRubric, setHasRubric] = useState(false);
  const [sbaWeight, setSbaWeight] = useState("");
  const [criteria, setCriteria] = useState<CriteriaRow[]>([{ name: "", maxMark: "" }]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const rubricTotal = criteria.reduce((sum, c) => sum + (Number(c.maxMark) || 0), 0);

  const submit = async () => {
    setError("");
    if (!title.trim()) { setError("Task name is required."); return; }
    if (hasRubric) {
      if (criteria.some((c) => !c.name.trim() || !(Number(c.maxMark) > 0))) {
        setError("Every criterion needs a name and a positive max mark.");
        return;
      }
    } else if (!(Number(maxMarks) > 0)) {
      setError("Total marks must be a positive number.");
      return;
    }
    setSaving(true);
    try {
      const created = await api.gradebook.createTask({
        classSubjectId,
        title: title.trim(),
        taskType,
        termNumber: term ? Number(term) : null,
        maxMarks: hasRubric ? 0 : Number(maxMarks),
        hasRubric,
        sbaWeight: sbaWeight ? Number(sbaWeight) : null,
        criteria: hasRubric ? criteria.map((c) => ({ name: c.name.trim(), maxMark: Number(c.maxMark) })) : undefined,
      });
      onCreated(created);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Could not create the task");
    } finally {
      setSaving(false);
    }
  };

  const field = "w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
  const label = "block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1";

  return (
    <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="rounded-2xl bg-white shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-5 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-900">New Assessment Task</h2>
          <button onClick={onClose} className="rounded-md p-1 hover:bg-gray-100" aria-label="Close">
            <X className="h-5 w-5 text-gray-400" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

          <div>
            <label className={label}>Task name</label>
            <input className={field} value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Term 3 Design theory test" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={label}>Task type</label>
              <select className={field} value={taskType} onChange={(e) => setTaskType(e.target.value)}>
                {TASK_TYPES.map((t) => <option key={t}>{t}</option>)}
              </select>
            </div>
            <div>
              <label className={label}>Term</label>
              <select className={field} value={term} onChange={(e) => setTerm(e.target.value)}>
                <option value="">—</option>
                {[1, 2, 3, 4].map((t) => <option key={t} value={t}>Term {t}</option>)}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={label}>SBA weight % <span className="normal-case font-normal">(optional)</span></label>
              <input className={field} inputMode="decimal" value={sbaWeight} onChange={(e) => setSbaWeight(e.target.value)} placeholder="e.g. 25" />
            </div>
            <div className="flex items-end pb-1">
              <label className="flex items-center gap-2 text-sm text-gray-700">
                <input type="checkbox" checked={hasRubric} onChange={(e) => setHasRubric(e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
                Has rubric (criteria)
              </label>
            </div>
          </div>

          {hasRubric ? (
            <div className="space-y-2">
              <label className={label}>Criteria</label>
              {criteria.map((c, i) => (
                <div key={i} className="flex items-center gap-2">
                  <input className={field} placeholder="Criterion name" value={c.name}
                    onChange={(e) => setCriteria(criteria.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))} />
                  <input className="w-20 rounded-md border border-gray-300 px-2 py-2 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500"
                    inputMode="decimal" placeholder="/10" value={c.maxMark}
                    onChange={(e) => setCriteria(criteria.map((x, j) => (j === i ? { ...x, maxMark: e.target.value } : x)))} />
                  <button onClick={() => setCriteria(criteria.filter((_, j) => j !== i))} disabled={criteria.length === 1}
                    className="rounded-md p-2 hover:bg-red-50 disabled:opacity-30" aria-label="Remove criterion">
                    <Trash2 className="h-4 w-4 text-red-400" />
                  </button>
                </div>
              ))}
              <div className="flex items-center justify-between">
                <button onClick={() => setCriteria([...criteria, { name: "", maxMark: "" }])}
                  className="flex items-center gap-1 text-sm text-blue-600 hover:underline">
                  <Plus className="h-4 w-4" /> Add criterion
                </button>
                <span className="text-sm text-gray-500">Total: <span className="font-semibold text-gray-900">/{rubricTotal}</span></span>
              </div>
            </div>
          ) : (
            <div>
              <label className={label}>Total marks</label>
              <input className={field} inputMode="decimal" value={maxMarks} onChange={(e) => setMaxMarks(e.target.value)} placeholder="e.g. 50" />
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 p-5 border-t border-gray-100">
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} disabled={saving}>{saving ? "Creating…" : "Create Task"}</Button>
        </div>
      </div>
    </div>
  );
}
