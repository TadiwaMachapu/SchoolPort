"use client";
import { useCallback, useEffect, useState } from "react";
import { IdCard, Plus, ShieldAlert, Upload, Users as UsersIcon } from "lucide-react";
import {
  api, type PositionOverviewItem, type PositionCatalogueItem,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { SkeletonCards } from "@/components/ui/skeleton";
import { AssignPositionModal } from "@/components/positions/AssignPositionModal";
import { StaffImportPanel } from "@/components/positions/StaffImportPanel";

// Sprint 1.5.0 Step 9 — position management ("who holds what" + assign/revoke). Gated server-side
// by system.positions_assign; this page surfaces a 403 as a friendly message.

function fmt(iso?: string | null) {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : d.toLocaleDateString("en-GB");
}

export default function PositionsPage() {
  const [overview, setOverview] = useState<PositionOverviewItem[] | null>(null);
  const [catalogue, setCatalogue] = useState<PositionCatalogueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [forbidden, setForbidden] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  const load = useCallback(async () => {
    try {
      const [ov, cat] = await Promise.all([api.positions.overview(), api.positions.catalogue()]);
      setOverview(ov);
      setCatalogue(cat);
      setForbidden(false);
    } catch (e) {
      if (e instanceof Error && /403|forbidden/i.test(e.message)) setForbidden(true);
      setOverview([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  async function revoke(userPositionId: string, name: string, position: string) {
    if (!confirm(`Revoke ${position} from ${name}?`)) return;
    await api.positions.revoke(userPositionId);
    load();
  }

  if (forbidden) {
    return (
      <div className="p-6 lg:p-8">
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <ShieldAlert className="h-10 w-10 text-text-muted" />
          <h3 className="mt-4 text-base font-semibold text-text-primary">Not permitted</h3>
          <p className="mt-1 text-sm text-text-secondary">Managing positions requires the Positions Assign permission (Principal, Deputy Principal, or IT Administrator).</p>
        </div>
      </div>
    );
  }

  const totalHolders = overview?.reduce((n, p) => n + p.holders.length, 0) ?? 0;

  return (
    <div className="p-6 lg:p-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-text-primary">Positions</h1>
          <p className="mt-1 text-sm text-text-secondary">Who holds what across your school — assign, scope, and revoke staff positions.</p>
        </div>
        <div className="flex shrink-0 gap-2">
          <Button variant="outline" onClick={() => setImportOpen(true)}>
            <Upload className="mr-1.5 h-4 w-4" /> Bulk import
          </Button>
          <Button onClick={() => setAssignOpen(true)}>
            <Plus className="mr-1.5 h-4 w-4" /> Assign position
          </Button>
        </div>
      </div>

      {loading ? (
        <div className="mt-6"><SkeletonCards count={6} /></div>
      ) : !overview || overview.length === 0 ? (
        <div className="mt-6 flex flex-col items-center justify-center rounded-xl border border-border bg-surface-card py-16 text-center shadow-sm ring-1 ring-border/50">
          <IdCard className="h-10 w-10 text-text-muted" />
          <h3 className="mt-4 text-base font-semibold text-text-primary">No positions assigned yet</h3>
          <p className="mt-1 text-sm text-text-secondary max-w-sm">Assign a position to a staff member, or bulk-import staff with positions from the onboarding wizard.</p>
          <Button className="mt-4" onClick={() => setAssignOpen(true)}><Plus className="mr-1.5 h-4 w-4" /> Assign position</Button>
        </div>
      ) : (
        <>
          <p className="mt-4 text-xs font-medium uppercase tracking-wider text-text-muted">
            {overview.length} positions · {totalHolders} active holders
          </p>
          <div className="mt-3 space-y-4">
            {overview.map((p) => (
              <div key={p.positionKey} className="rounded-xl border border-border bg-surface-card shadow-sm ring-1 ring-border/50">
                <div className="flex items-center justify-between border-b border-border px-5 py-3">
                  <div className="flex items-center gap-2">
                    <h2 className="text-sm font-semibold text-text-primary">{p.displayName}</h2>
                    <Badge variant="outline" className="text-[10px]">{p.category}</Badge>
                  </div>
                  <span className="flex items-center gap-1 text-xs text-text-muted"><UsersIcon className="h-3.5 w-3.5" />{p.holders.length}</span>
                </div>
                <div className="divide-y divide-border">
                  {p.holders.map((h) => (
                    <div key={h.userPositionId} className="flex items-center justify-between gap-3 px-5 py-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-text-primary">{h.userName}</p>
                        <div className="mt-1 flex flex-wrap items-center gap-1.5">
                          {h.scopes.length === 0
                            ? <span className="text-xs text-text-muted">School-wide / unscoped</span>
                            : h.scopes.map((s, i) => <Badge key={i} variant="default" className="text-[10px]">{s.label}</Badge>)}
                          <span className="text-xs text-text-muted">· {fmt(h.effectiveFrom)}{h.effectiveTo ? ` → ${fmt(h.effectiveTo)}` : ""}</span>
                        </div>
                      </div>
                      <Button variant="ghost" size="sm" className="shrink-0 text-danger-700 hover:bg-danger-100"
                        onClick={() => revoke(h.userPositionId, h.userName, p.displayName)}>Revoke</Button>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </>
      )}

      {assignOpen && (
        <AssignPositionModal
          catalogue={catalogue}
          onClose={() => setAssignOpen(false)}
          onAssigned={() => { setAssignOpen(false); load(); }}
        />
      )}
      {importOpen && (
        <StaffImportPanel onClose={() => setImportOpen(false)} onImported={() => load()} />
      )}
    </div>
  );
}
