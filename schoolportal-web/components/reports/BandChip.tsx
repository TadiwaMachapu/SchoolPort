// Sprint 1.5.3 Smart Reports — shared intervention-band + risk visuals for the oversight views.
// The band is the 50%-line judgment from the at-risk primitive (captured subjects only); the risk
// dot is per-subject red/amber/green.

export type Band = "Watch" | "AtRisk" | "Priority" | null;

/** Priority-first ordering key (matches the server's OrderBy). */
export function bandRank(band: Band): number {
  return band === "Priority" ? 0 : band === "AtRisk" ? 1 : band === "Watch" ? 2 : 3;
}

const BAND_STYLE: Record<"Watch" | "AtRisk" | "Priority", string> = {
  Priority: "bg-red-100 text-red-800",
  AtRisk: "bg-orange-100 text-orange-800",
  Watch: "bg-amber-100 text-amber-800",
};

export function BandChip({ band }: { band: Band }) {
  if (!band)
    return (
      <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-semibold text-emerald-700">
        On track
      </span>
    );
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-bold ${BAND_STYLE[band]}`}>
      {band === "AtRisk" ? "At Risk" : band}
    </span>
  );
}

export function RiskDot({ risk }: { risk: string }) {
  const c =
    risk === "red" ? "bg-red-500" : risk === "amber" ? "bg-amber-400" : risk === "green" ? "bg-emerald-500" : "bg-gray-300";
  return <span className={`inline-block h-2 w-2 shrink-0 rounded-full ${c}`} aria-hidden />;
}

/** "captured n/total" fraction — how much of the learner's load has real marks yet. */
export function CapturedFraction({ captured, total }: { captured: number; total: number }) {
  return (
    <span className="text-[11px] text-text-muted">
      {captured}/{total} captured
    </span>
  );
}
