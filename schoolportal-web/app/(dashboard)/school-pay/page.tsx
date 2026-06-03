"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type FeeItem, type FeeStatement, type FeePaymentItem, type Term } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { getClientRole } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Plus, Trash2, CreditCard, ChevronDown, ChevronRight, X, Check, Loader2 } from "lucide-react";

function fmt(amount: number) {
  return `R ${amount.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, " ")}`;
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString("en-ZA", { day: "2-digit", month: "short", year: "numeric" });
}

// ── Admin view ────────────────────────────────────────────────────
function AdminView() {
  const [fees,      setFees]      = useState<FeeItem[]>([]);
  const [terms,     setTerms]     = useState<Term[]>([]);
  const [loading,   setLoading]   = useState(true);
  const [expanded,  setExpanded]  = useState<string | null>(null);
  const [payments,  setPayments]  = useState<Record<string, FeePaymentItem[]>>({});
  const [showForm,  setShowForm]  = useState(false);
  const [form,      setForm]      = useState({ name: "", description: "", amountZar: "", dueDate: "", termId: "" });
  const [saving,    setSaving]    = useState(false);
  const [payForm,   setPayForm]   = useState<{ feeId: string; studentNumber: string; amount: string; notes: string } | null>(null);
  const [students,  setStudents]  = useState<{ userId: string; studentId?: string; firstName: string; lastName: string; studentNumber?: string }[]>([]);
  const [error,     setError]     = useState("");

  useEffect(() => {
    Promise.all([
      api.fees.list().then(setFees),
      api.terms.list().then(setTerms),
      api.users.list({ role: "Student", pageSize: 200 }).then(r => setStudents(r.items as any)),
    ]).finally(() => setLoading(false));
  }, []);

  async function createFee() {
    if (!form.name.trim() || !form.amountZar || !form.dueDate) { setError("Name, amount and due date are required"); return; }
    setSaving(true); setError("");
    try {
      const created = await api.fees.create({
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        amountZar: parseFloat(form.amountZar),
        dueDate: new Date(form.dueDate).toISOString(),
        termId: form.termId || undefined,
      });
      setFees(f => [...f, created].sort((a, b) => a.dueDate.localeCompare(b.dueDate)));
      setShowForm(false);
      setForm({ name: "", description: "", amountZar: "", dueDate: "", termId: "" });
    } catch (e) { setError(e instanceof Error ? e.message : "Failed to create fee"); }
    finally { setSaving(false); }
  }

  async function deleteFee(id: string) {
    await api.fees.delete(id);
    setFees(f => f.filter(x => x.feeId !== id));
  }

  async function loadPayments(feeId: string) {
    if (payments[feeId]) return;
    const p = await api.fees.payments(feeId);
    setPayments(prev => ({ ...prev, [feeId]: p }));
  }

  async function recordPayment() {
    if (!payForm) return;
    const student = students.find(s => (s as any).studentNumber === payForm.studentNumber);
    if (!student) { setError("Learner not found"); return; }
    setSaving(true); setError("");
    try {
      await api.fees.recordPayment(payForm.feeId, {
        studentId: (student as any).studentId ?? student.userId,
        amountPaidZar: parseFloat(payForm.amount),
        notes: payForm.notes.trim() || undefined,
      });
      setPayments(prev => ({ ...prev, [payForm.feeId]: [] }));
      await loadPayments(payForm.feeId);
      const updated = await api.fees.list();
      setFees(updated);
      setPayForm(null);
    } catch (e) { setError(e instanceof Error ? e.message : "Failed to record payment"); }
    finally { setSaving(false); }
  }

  if (loading) return <div className="flex items-center justify-center h-64"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-6">
      {error && <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{error}</div>}

      {/* Add fee form */}
      {showForm ? (
        <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <h2 className="text-base font-semibold text-gray-900">New fee</h2>
            <button onClick={() => setShowForm(false)}><X className="h-4 w-4 text-gray-400" /></button>
          </div>
          <div className="px-6 py-5 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Name <span className="text-red-500">*</span></label>
                <Input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="e.g. Term 1 School Fee" autoFocus />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Amount (ZAR) <span className="text-red-500">*</span></label>
                <Input type="number" min={0} step={0.01} value={form.amountZar} onChange={e => setForm(f => ({ ...f, amountZar: e.target.value }))} placeholder="0.00" />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Due date <span className="text-red-500">*</span></label>
                <Input type="date" value={form.dueDate} onChange={e => setForm(f => ({ ...f, dueDate: e.target.value }))} />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Term (optional)</label>
                <select value={form.termId} onChange={e => setForm(f => ({ ...f, termId: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="">No term</option>
                  {terms.map(t => <option key={t.termId} value={t.termId}>Term {t.termNumber} {t.year}</option>)}
                </select>
              </div>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Description</label>
              <Input value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} placeholder="Optional note" />
            </div>
            <div className="flex items-center gap-3 pt-1">
              <Button onClick={createFee} loading={saving} className="gap-1.5">
                <Check className="h-3.5 w-3.5" /> Create fee
              </Button>
              <button onClick={() => setShowForm(false)} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
            </div>
          </div>
        </div>
      ) : (
        <Button onClick={() => setShowForm(true)} className="gap-2">
          <Plus className="h-4 w-4" /> Add fee
        </Button>
      )}

      {/* Record payment modal */}
      {payForm && (
        <div className="rounded-2xl bg-white border border-blue-200 shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <h2 className="text-base font-semibold text-gray-900">Record payment</h2>
            <button onClick={() => setPayForm(null)}><X className="h-4 w-4 text-gray-400" /></button>
          </div>
          <div className="px-6 py-5 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Learner student number</label>
                <Input value={payForm.studentNumber} onChange={e => setPayForm(p => p && ({ ...p, studentNumber: e.target.value }))} placeholder="STU2026001" />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Amount paid (ZAR)</label>
                <Input type="number" min={0} step={0.01} value={payForm.amount} onChange={e => setPayForm(p => p && ({ ...p, amount: e.target.value }))} placeholder="0.00" />
              </div>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Notes (optional)</label>
              <Input value={payForm.notes} onChange={e => setPayForm(p => p && ({ ...p, notes: e.target.value }))} placeholder="e.g. Cash payment, receipt #123" />
            </div>
            <div className="flex items-center gap-3 pt-1">
              <Button onClick={recordPayment} loading={saving} className="gap-1.5">
                <Check className="h-3.5 w-3.5" /> Record payment
              </Button>
              <button onClick={() => setPayForm(null)} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
            </div>
          </div>
        </div>
      )}

      {/* Fee list */}
      <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
        {fees.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <CreditCard className="h-10 w-10 text-gray-200 mb-3" />
            <p className="text-sm font-medium text-gray-500">No fees configured</p>
            <p className="text-xs text-gray-400 mt-1">Add a fee to start tracking payments.</p>
          </div>
        ) : fees.map(fee => (
          <div key={fee.feeId} className="border-b border-gray-100 last:border-0">
            <div className="flex items-center gap-4 px-5 py-4 hover:bg-gray-50">
              <button onClick={async () => {
                const next = expanded === fee.feeId ? null : fee.feeId;
                setExpanded(next);
                if (next) await loadPayments(next);
              }} className="text-gray-400 hover:text-gray-600">
                {expanded === fee.feeId ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
              </button>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-gray-900">{fee.name}</p>
                <p className="text-xs text-gray-400">
                  Due {fmtDate(fee.dueDate)}{fee.termLabel ? ` · ${fee.termLabel}` : ""}
                  {fee.description ? ` · ${fee.description}` : ""}
                </p>
              </div>
              <div className="text-right shrink-0">
                <p className="text-sm font-bold text-gray-900">{fmt(fee.amountZar)}</p>
                <p className="text-xs text-emerald-600">{fmt(fee.totalCollected)} collected · {fee.paymentCount} payment{fee.paymentCount !== 1 ? "s" : ""}</p>
              </div>
              <button onClick={() => setPayForm({ feeId: fee.feeId, studentNumber: "", amount: "", notes: "" })}
                className="shrink-0 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors">
                + Payment
              </button>
              <button onClick={() => deleteFee(fee.feeId)}
                className="h-7 w-7 rounded flex items-center justify-center text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors shrink-0">
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>

            {expanded === fee.feeId && (
              <div className="px-5 pb-4 bg-gray-50 border-t border-gray-100">
                {!payments[fee.feeId] ? (
                  <p className="text-xs text-gray-400 py-3">Loading…</p>
                ) : payments[fee.feeId].length === 0 ? (
                  <p className="text-xs text-gray-400 py-3">No payments recorded yet.</p>
                ) : (
                  <table className="w-full text-xs mt-2">
                    <thead><tr className="text-gray-400 text-left">
                      <th className="py-1.5 font-medium">Learner</th>
                      <th className="py-1.5 font-medium">Amount</th>
                      <th className="py-1.5 font-medium">Date</th>
                      <th className="py-1.5 font-medium">Notes</th>
                    </tr></thead>
                    <tbody className="divide-y divide-gray-100">
                      {payments[fee.feeId].map(p => (
                        <tr key={p.feePaymentId}>
                          <td className="py-1.5 text-gray-700">{p.studentName} <span className="text-gray-400">({p.studentNumber})</span></td>
                          <td className="py-1.5 font-semibold text-emerald-700">{fmt(p.amountPaidZar)}</td>
                          <td className="py-1.5 text-gray-500">{fmtDate(p.paidAt)}</td>
                          <td className="py-1.5 text-gray-400">{p.notes ?? "—"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Learner / Parent view ─────────────────────────────────────────
function StatementView() {
  const [statement, setStatement] = useState<FeeStatement[]>([]);
  const [loading, setLoading]     = useState(true);

  useEffect(() => {
    api.fees.myStatement().then(setStatement).catch(() => {}).finally(() => setLoading(false));
  }, []);

  const totalOwed   = statement.reduce((s, f) => s + f.balance, 0);
  const totalPaid   = statement.reduce((s, f) => s + f.amountPaid, 0);

  if (loading) return <div className="flex items-center justify-center h-64"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "Total Fees", value: fmt(statement.reduce((s, f) => s + f.amountZar, 0)), color: "text-gray-900" },
          { label: "Total Paid", value: fmt(totalPaid), color: "text-emerald-600" },
          { label: "Outstanding", value: fmt(Math.max(0, totalOwed)), color: totalOwed > 0 ? "text-red-600" : "text-gray-400" },
        ].map(s => (
          <div key={s.label} className="rounded-xl bg-white border border-gray-200 p-4 text-center shadow-sm">
            <p className={`text-2xl font-bold ${s.color}`}>{s.value}</p>
            <p className="text-xs text-gray-500 mt-1">{s.label}</p>
          </div>
        ))}
      </div>

      <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
        {statement.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <CreditCard className="h-10 w-10 text-gray-200 mb-3" />
            <p className="text-sm text-gray-500">No fees have been issued for your account.</p>
          </div>
        ) : (
          <>
            <div className="grid grid-cols-[1fr_100px_100px_100px_80px] gap-2 px-5 py-2.5 border-b border-gray-100 text-xs font-semibold text-gray-400 uppercase tracking-wider">
              <span>Fee</span><span className="text-right">Amount</span><span className="text-right">Paid</span><span className="text-right">Balance</span><span />
            </div>
            {statement.map(f => (
              <div key={f.feeId} className="grid grid-cols-[1fr_100px_100px_100px_80px] gap-2 items-center px-5 py-3.5 border-b border-gray-100 last:border-0 hover:bg-gray-50">
                <div>
                  <p className="text-sm font-medium text-gray-900">{f.name}</p>
                  <p className="text-xs text-gray-400">Due {fmtDate(f.dueDate)}{f.description ? ` · ${f.description}` : ""}</p>
                </div>
                <p className="text-sm text-right text-gray-700">{fmt(f.amountZar)}</p>
                <p className="text-sm text-right text-emerald-600 font-medium">{fmt(f.amountPaid)}</p>
                <p className={`text-sm text-right font-semibold ${f.balance > 0 ? "text-red-600" : "text-gray-400"}`}>{fmt(Math.max(0, f.balance))}</p>
                <div className="flex justify-end">
                  {f.isPaid
                    ? <span className="text-xs font-medium text-emerald-700 bg-emerald-50 rounded-full px-2 py-0.5">Paid</span>
                    : <span className="text-xs font-medium text-red-700 bg-red-50 rounded-full px-2 py-0.5">Owing</span>
                  }
                </div>
              </div>
            ))}
          </>
        )}
      </div>
    </div>
  );
}

export default function SchoolPayPage() {
  const router  = useRouter();
  const hasFlag = useFeature("schoolPay");
  const [role,  setRole] = useState("");

  useEffect(() => { setRole(getClientRole()); }, []);

  if (!hasFlag) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <CreditCard className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">SchoolPay not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable the SchoolPay feature in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  const isAdmin = role === "Admin";

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">SchoolPay</h1>
        <p className="text-sm text-gray-500 mt-1">
          {isAdmin ? "Manage school fees and record learner payments." : "View your fee statement and payment history."}
        </p>
      </div>
      {!role ? null : isAdmin ? <AdminView /> : <StatementView />}
    </div>
  );
}
