using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Server.Seeds;

/// <summary>
/// Sprint 1.5.2 — NSC past-paper library with VERIFIED official DBE URLs.
///
/// URL sourcing discipline (same standard as the Sprint 1.5.1 university seed):
/// every direct URL below was extracted from the official education.gov.za per-year
/// exam pages and spot-verified by HEAD request (200, application/pdf, and the
/// Content-Disposition filename confirming subject/paper/year) on 2026-07-07.
/// Papers whose direct link could not be verified carry DirectUrl = null and fall
/// back to the DBE past-papers index page. NOTHING is invented.
///
/// Verified-direct vs index-fallback tally (November papers, English):
///   2024: 24/24 direct   — source: https://www.education.gov.za/2024NSCNovemberpastpapers.aspx
///   2023: 24/24 direct   — source: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/2023NSCNovemberpastpapers.aspx
///   2022: 24/24 direct   — source: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/2022NovemberExams.aspx
///   2021: 24/24 direct   — source: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/2021NSCExamPapers.aspx
///   2020: 24/24 direct   — source: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/2020NSCExamPapers.aspx
///   2019:  8/22 direct   — stable /Portals/0/CD/ PDF paths verified individually; the
///          other 14 (Maths, Maths Lit, English HL/FAL, Geography, History) use a
///          different unpublished filename shape → index fallback.
///   TOTAL: 128 verified direct, 14 index fallback.
///
/// 2014 NSC Exemplars (PaperType = Exemplar; Decision Option A, 2026-07-08):
///   17/17 published English papers verified direct + 14 verified English/bilingual memos —
///   source: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/NSC2014Exemplars.aspx
///   DBE published no English FAL exemplars; English HL / Accounting / Business Studies P1 only.
///
/// The sync also repairs two defects in the original Matric Hub v1 seed:
///   1. The old DbeUrl constant was a broken/typo'd address — every legacy row 404'd.
///      All rows still carrying it are healed to the correct index URL.
///   2. 2019 Accounting P2 and Business Studies P2 never existed (single papers in
///      2019; the P1/P2 split started in 2020) — those phantom rows are deactivated.
///
/// Additive/upsert by (Subject, Year, PaperNumber, Language, PaperType): runs on every
/// startup, so already-seeded databases (including live) converge with a fresh install.
/// </summary>
public static class MatricPastPaperSeedData
{
    /// <summary>Official DBE past-papers index page (fallback when no verified direct link).</summary>
    public const string DbeIndexUrl =
        "https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/NSCPastExaminationpapers.aspx";

    /// <summary>The broken URL the original Matric Hub v1 seed used (typo'd host path — 404s).</summary>
    private const string LegacyBrokenUrl =
        "https://www.education.gov.za/Curriculum/NationalSenioCertificate(NSC)Examinations/NSCPreviousExaminationPapers.aspx";

    private static string Dbe(string ticket, int tabid, int mid) =>
        $"https://www.education.gov.za/LinkClick.aspx?fileticket={ticket}%3d&tabid={tabid}&portalid=0&mid={mid}&forcedownload=true";

    private const string P2019 = "https://www.education.gov.za/Portals/0/CD/2019%20November%20past%20papers/Non-Languages%20Nov%202019%20PDF";

    private const string MathsN = "Mathematics";
    private const string MathLitN = "Mathematical Literacy";
    private const string PhysSciN = "Physical Sciences";
    private const string LifeSciN = "Life Sciences";
    private const string EngHlN = "English Home Language";
    private const string EngFalN = "English First Additional Language";
    private const string AccN = "Accounting";
    private const string BusN = "Business Studies";
    private const string EconN = "Economics";
    private const string GeoN = "Geography";
    private const string HistN = "History";

    // (Subject, Year, PaperNumber, DirectUrl-or-null). null → DBE index fallback.
    private static readonly (string Subject, int Year, int Paper, string? DirectUrl)[] NovemberPapers =
    {
        // ── 2024 — all 24 verified direct (fileticket links from the official 2024 page) ──
        (MathsN,   2024, 1, Dbe("8W2dAxBUTQA", 5193, 13724)), (MathsN,   2024, 2, Dbe("ycHWvBVvV2M", 5193, 13724)),
        (MathLitN, 2024, 1, Dbe("r3H6xWQUYXg", 5193, 13723)), (MathLitN, 2024, 2, Dbe("1EZXhzf3-sI", 5193, 13723)),
        (PhysSciN, 2024, 1, Dbe("jKqWYBbucS4", 5193, 13728)), (PhysSciN, 2024, 2, Dbe("ZxN41kEGHhI", 5193, 13728)),
        (LifeSciN, 2024, 1, Dbe("UH53U88PRPE", 5193, 13722)), (LifeSciN, 2024, 2, Dbe("B-Ss2iShxUE", 5193, 13722)),
        (EngHlN,   2024, 1, Dbe("de2qPWCUDzw", 5193, 13691)), (EngHlN,   2024, 2, Dbe("22HdBbdLTxU", 5193, 13691)), (EngHlN, 2024, 3, Dbe("FxQD3wruMb4", 5193, 13691)),
        (EngFalN,  2024, 1, Dbe("ZJZsyNpCu9Y", 5193, 13691)), (EngFalN,  2024, 2, Dbe("ko5NmgVF4bA", 5193, 13691)), (EngFalN, 2024, 3, Dbe("Gb_3vgw3pg8", 5193, 13691)),
        (AccN,     2024, 1, Dbe("LPWTS_eR8NI", 5193, 13703)), (AccN,     2024, 2, Dbe("tJdE50Ec9zY", 5193, 13703)),
        (BusN,     2024, 1, Dbe("VGv_B_A4kxA", 5193, 13707)), (BusN,     2024, 2, Dbe("1_eNM5ajC0g", 5193, 13707)),
        (EconN,    2024, 1, Dbe("gZ8YszYxrcI", 5193, 13714)), (EconN,    2024, 2, Dbe("bFm9Gw3zowg", 5193, 13714)),
        (GeoN,     2024, 1, Dbe("Hc8_CaQJpd4", 5193, 13717)), (GeoN,     2024, 2, Dbe("LrQ39-VlNh4", 5193, 13717)),
        (HistN,    2024, 1, Dbe("qM73YgRemdM", 5193, 13718)), (HistN,    2024, 2, Dbe("1FNRebZ9qcw", 5193, 13718)),

        // ── 2023 — all 24 verified direct ──
        (MathsN,   2023, 1, Dbe("M_7mZq2zE5o", 4682, 12681)), (MathsN,   2023, 2, Dbe("Zoios-rCurI", 4682, 12681)),
        (MathLitN, 2023, 1, Dbe("5VCFfxPhz6k", 4682, 12680)), (MathLitN, 2023, 2, Dbe("8heLhdxLIws", 4682, 12680)),
        (PhysSciN, 2023, 1, Dbe("oTJzzXEU6Ng", 4682, 12685)), (PhysSciN, 2023, 2, Dbe("Gs2cDJBpRR0", 4682, 12685)),
        (LifeSciN, 2023, 1, Dbe("5Xc2L4uffmA", 4682, 12679)), (LifeSciN, 2023, 2, Dbe("4yWO4CegNNE", 4682, 12679)),
        (EngHlN,   2023, 1, Dbe("TbjDznPFtSA", 4682, 12648)), (EngHlN,   2023, 2, Dbe("KLPJighLTHA", 4682, 12648)), (EngHlN, 2023, 3, Dbe("wwZ88J_HrGE", 4682, 12648)),
        (EngFalN,  2023, 1, Dbe("rzKvzSdEjAU", 4682, 12648)), (EngFalN,  2023, 2, Dbe("8l6DnwdLPXY", 4682, 12648)), (EngFalN, 2023, 3, Dbe("ehLuStOw2jA", 4682, 12648)),
        (AccN,     2023, 1, Dbe("I3ZOntNTjjo", 4682, 12660)), (AccN,     2023, 2, Dbe("89AYULMK6WY", 4682, 12660)),
        (BusN,     2023, 1, Dbe("j-o_oRFFS7A", 4682, 12664)), (BusN,     2023, 2, Dbe("d0WgLcInBU8", 4682, 12664)),
        (EconN,    2023, 1, Dbe("Dlshc30RtYk", 4682, 12671)), (EconN,    2023, 2, Dbe("sct4D9kRRhY", 4682, 12671)),
        (GeoN,     2023, 1, Dbe("sOLlvteQCeM", 4682, 12674)), (GeoN,     2023, 2, Dbe("qCvbCunZPCY", 4682, 12674)),
        (HistN,    2023, 1, Dbe("DrznBkYP4P0", 4682, 12675)), (HistN,    2023, 2, Dbe("YIVtG2z_yWE", 4682, 12675)),

        // ── 2022 — all 24 verified direct ──
        (MathsN,   2022, 1, Dbe("Juy5nA5N3fM", 3294, 10986)), (MathsN,   2022, 2, Dbe("DqfP-i10rEE", 3294, 10986)),
        (MathLitN, 2022, 1, Dbe("bD6ydrbzgyE", 3294, 10985)), (MathLitN, 2022, 2, Dbe("Bs_l02h3JGs", 3294, 10985)),
        (PhysSciN, 2022, 1, Dbe("5l5vQTQBaU4", 3294, 10990)), (PhysSciN, 2022, 2, Dbe("_7CRVUkWGMA", 3294, 10990)),
        (LifeSciN, 2022, 1, Dbe("Fg44KuXQ8Es", 3294, 10984)), (LifeSciN, 2022, 2, Dbe("FPGyIKhYDUw", 3294, 10984)),
        (EngHlN,   2022, 1, Dbe("TmBjjP7joNg", 3294, 10953)), (EngHlN,   2022, 2, Dbe("oQCVhvL8WnE", 3294, 10953)), (EngHlN, 2022, 3, Dbe("gFBCYV3CPk0", 3294, 10953)),
        (EngFalN,  2022, 1, Dbe("6u3vvGSi-40", 3294, 10953)), (EngFalN,  2022, 2, Dbe("7MabwVLlFcQ", 3294, 10953)), (EngFalN, 2022, 3, Dbe("ns8HTEUGA1I", 3294, 10953)),
        (AccN,     2022, 1, Dbe("l3HgEye_Xg8", 3294, 10965)), (AccN,     2022, 2, Dbe("rEzVzZbaHTs", 3294, 10965)),
        (BusN,     2022, 1, Dbe("XKSOkexd5ho", 3294, 10969)), (BusN,     2022, 2, Dbe("j4GWWzHMKt4", 3294, 10969)),
        (EconN,    2022, 1, Dbe("ZbwLwi4Nbbk", 3294, 10976)), (EconN,    2022, 2, Dbe("mz3AECevBTo", 3294, 10976)),
        (GeoN,     2022, 1, Dbe("T4gsWEf6_A0", 3294, 10979)), (GeoN,     2022, 2, Dbe("yFfqSPQtFNw", 3294, 10979)),
        (HistN,    2022, 1, Dbe("y2jZuQHPLLs", 3294, 10980)), (HistN,    2022, 2, Dbe("zuyAs-Eq55E", 3294, 10980)),

        // ── 2021 — all 24 verified direct ──
        (MathsN,   2021, 1, Dbe("sGgq9FNv0lQ", 2922, 10135)), (MathsN,   2021, 2, Dbe("jgMJbW3Aa0o", 2922, 10135)),
        (MathLitN, 2021, 1, Dbe("mcF9POk9trQ", 2922, 10134)), (MathLitN, 2021, 2, Dbe("yd34t3chxo4", 2922, 10134)),
        (PhysSciN, 2021, 1, Dbe("unBWzCuezsM", 2922, 10138)), (PhysSciN, 2021, 2, Dbe("oI4BAzXUMNA", 2922, 10138)),
        (LifeSciN, 2021, 1, Dbe("vlfOOXXMFX0", 2922, 10133)), (LifeSciN, 2021, 2, Dbe("01TyV_uHA90", 2922, 10133)),
        (EngHlN,   2021, 1, Dbe("8M2OZLlkMA4", 2922, 10168)), (EngHlN,   2021, 2, Dbe("gXE-hwPulps", 2922, 10168)), (EngHlN, 2021, 3, Dbe("87RmXBRcg44", 2922, 10168)),
        (EngFalN,  2021, 1, Dbe("xYYCutEfS_o", 2922, 10168)), (EngFalN,  2021, 2, Dbe("mI4DiSjDy9Q", 2922, 10168)), (EngFalN, 2021, 3, Dbe("HgE2qwbdjTs", 2922, 10168)),
        (AccN,     2021, 1, Dbe("IMBGaSdEu0w", 2922, 10113)), (AccN,     2021, 2, Dbe("8L2N3S3KQtE", 2922, 10113)),
        (BusN,     2021, 1, Dbe("emPQFCQG278", 2922, 10118)), (BusN,     2021, 2, Dbe("9W_YphuZjmI", 2922, 10118)),
        (EconN,    2021, 1, Dbe("sBCrCimV25U", 2922, 10126)), (EconN,    2021, 2, Dbe("VTOyhQRnp2w", 2922, 10126)),
        (GeoN,     2021, 1, Dbe("mVTAFza9KK0", 2922, 10129)), (GeoN,     2021, 2, Dbe("xU-y2hGajP8", 2922, 10129)),
        (HistN,    2021, 1, Dbe("7xVP74uoqJ4", 2922, 10130)), (HistN,    2021, 2, Dbe("azxDXVZ49Kk", 2922, 10130)),

        // ── 2020 — all 24 verified direct ──
        (MathsN,   2020, 1, Dbe("FU-0n3tYUzs", 2702, 9632)),  (MathsN,   2020, 2, Dbe("f-OMZa2HZ5U", 2702, 9632)),
        (MathLitN, 2020, 1, Dbe("fPPiLJppj2E", 2702, 9631)),  (MathLitN, 2020, 2, Dbe("X2QuTYuqb-4", 2702, 9631)),
        (PhysSciN, 2020, 1, Dbe("65cL9MczvtQ", 2702, 9635)),  (PhysSciN, 2020, 2, Dbe("xUQ6Zo6wIpI", 2702, 9635)),
        (LifeSciN, 2020, 1, Dbe("LtJTVvld-OI", 2702, 9630)),  (LifeSciN, 2020, 2, Dbe("mxFP5xM-Il4", 2702, 9630)),
        (EngHlN,   2020, 1, Dbe("adjYmnZ_mFo", 2702, 10166)), (EngHlN,   2020, 2, Dbe("9wc2gepoi9Y", 2702, 10166)), (EngHlN, 2020, 3, Dbe("yxqSYLYvX4A", 2702, 10166)),
        (EngFalN,  2020, 1, Dbe("rTt9SRIIHu4", 2702, 10166)), (EngFalN,  2020, 2, Dbe("VhPv8HhXra0", 2702, 10166)), (EngFalN, 2020, 3, Dbe("jCUN-fVNK9Q", 2702, 10166)),
        (AccN,     2020, 1, Dbe("LwMXVemcXI8", 2702, 9612)),  (AccN,     2020, 2, Dbe("hJgFl0DQaQs", 2702, 9612)),
        (BusN,     2020, 1, Dbe("Q9AdzrjDjok", 2702, 9616)),  (BusN,     2020, 2, Dbe("QbHtAF-mIUo", 2702, 9616)),
        (EconN,    2020, 1, Dbe("uvVxM4oFNA4", 2702, 9623)),  (EconN,    2020, 2, Dbe("TaNsXFOsAXY", 2702, 9623)),
        (GeoN,     2020, 1, Dbe("15WJ7q1PNB8", 2702, 9626)),  (GeoN,     2020, 2, Dbe("4BeqbN365wE", 2702, 9626)),
        (HistN,    2020, 1, Dbe("KLENKZunxig", 2702, 9627)),  (HistN,    2020, 2, Dbe("UZ1E5aqLDco", 2702, 9627)),

        // ── 2019 — 8 verified direct (stable /Portals/ PDF paths), 14 index fallbacks ──
        // Verified direct:
        (PhysSciN, 2019, 1, $"{P2019}/Physical%20Sciences/Physical%20Sciences%20P1%20Nov%202019%20Eng.pdf"),
        (PhysSciN, 2019, 2, $"{P2019}/Physical%20Sciences/Physical%20Sciences%20P2%20Nov%202019%20Eng.pdf"),
        (LifeSciN, 2019, 1, $"{P2019}/Life%20Sciences/Life%20Sciences%20P1%20Nov%202019%20Eng.pdf"),
        (LifeSciN, 2019, 2, $"{P2019}/Life%20Sciences/Life%20Sciences%20P2%20Nov%202019%20Eng.pdf"),
        (EconN,    2019, 1, $"{P2019}/Economics/Economics%20P1%20Nov%202019%20Eng.pdf"),
        (EconN,    2019, 2, $"{P2019}/Economics/Economics%20P2%20Nov%202019%20Eng.pdf"),
        (AccN,     2019, 1, $"{P2019}/Accounting/Accounting%20Nov%202019%20Eng.pdf"),        // single paper in 2019
        (BusN,     2019, 1, $"{P2019}/Business%20Studies/Business%20Studies%20Nov%202019%20Eng.pdf"), // single paper in 2019
        // Index fallbacks (direct filenames not verifiable — DO NOT invent):
        (MathsN,   2019, 1, null), (MathsN,   2019, 2, null),
        (MathLitN, 2019, 1, null), (MathLitN, 2019, 2, null),
        (EngHlN,   2019, 1, null), (EngHlN,   2019, 2, null), (EngHlN,  2019, 3, null),
        (EngFalN,  2019, 1, null), (EngFalN,  2019, 2, null), (EngFalN, 2019, 3, null),
        (GeoN,     2019, 1, null), (GeoN,     2019, 2, null),
        (HistN,    2019, 1, null), (HistN,    2019, 2, null),
    };

    // ── 2014 NSC Exemplars (Decision: Option A, 2026-07-08) ─────────────────────────
    // Source page: https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/NSC2014Exemplars.aspx
    // Every ticket below HEAD-verified 2026-07-08 (200, application/pdf, Content-Disposition
    // filename confirming subject/paper/"Exemplar 2014"). All 17 published English papers
    // verified direct; DBE published NO English FAL exemplars and only P1 for English HL,
    // Accounting, and Business Studies — absent papers are NOT seeded.
    // Memos differ from the November set: the exemplar page carries per-subject memo links,
    // so verified English/bilingual memos ARE seeded (HasMemo = true). Three memo failures
    // (flagged, not seeded): Maths P2 (DBE mislinks the memo to the paper itself),
    // Geography P1 (no English memo published), Geography P2 (English-labelled link serves
    // the Afrikaans memo file).
    private const int ExemplarYear = 2014;
    private const int ExemplarTab = 599;

    // (Subject, Paper, PaperTicket, Mid, MemoTicket-or-null). Tickets → Dbe(ticket, ExemplarTab, mid).
    private static readonly (string Subject, int Paper, string Ticket, int Mid, string? MemoTicket)[] Exemplars2014 =
    {
        (MathsN,   1, "uAytissvJtU", 1801, "CEi3BMD9v3U"), // memo is bilingual Eng & Afr
        (MathsN,   2, "uanG6qTRyuY", 1801, null),          // "Memo 2 (English)" on the DBE page mislinks to the paper
        (MathLitN, 1, "tGaVHrRPzY0", 1802, "ntL2Em9wY9k"),
        (MathLitN, 2, "JtztNKBSnaA", 1802, "R_TWtfZLTPA"),
        (PhysSciN, 1, "s-zwsd8zhjU", 1805, "r-jVC-XX834"), // memo bilingual (DBE filename typo "Afr & Afr")
        (PhysSciN, 2, "9XOor8X67aY", 1805, "n5arPjiI60o"), // memo bilingual Afr & Eng
        (LifeSciN, 1, "5aFRuf26WxA", 1800, "ebco0n3j4gA"),
        (LifeSciN, 2, "zRdLCbeyweg", 1800, "8xkbRStsmJk"),
        (EngHlN,   1, "NyDRr8HEFx0", 1822, "BLGbRsnInA8"), // only English HL exemplar published; no FAL
        (AccN,     1, "pV72X4CnyEs", 1796, "tv514Xk9n1k"), // single paper in 2014
        (BusN,     1, "AUie7rtY5Bc", 1792, "OHyQ11VYiNI"), // single paper in 2014
        (EconN,    1, "xPjRSsF7IxQ", 1785, "rQIriI2grJs"),
        (EconN,    2, "sqTKQ0LZEhk", 1785, "amUq0IZlWKk"),
        (GeoN,     1, "1JbpTJnBq3k", 1816, null),          // no English memo published
        (GeoN,     2, "ysFX9g5L5zk", 1816, null),          // English-labelled memo link serves the Afrikaans file
        (HistN,    1, "JjBG37KoxgY", 1797, "qFjVTZgL3ms"),
        (HistN,    2, "rWThR3wczgg", 1797, "X_Oih6NLrT4"),
    };

    /// <summary>Phantom rows from the v1 seed: papers DBE never published (2019 was a
    /// single-paper year for these subjects; the P1/P2 split started in 2020).</summary>
    private static readonly (string Subject, int Year, int Paper)[] PhantomRows =
    {
        (AccN, 2019, 2),
        (BusN, 2019, 2),
    };

    public static async Task SyncAsync(SchoolPortalDbContext context, ILogger logger)
    {
        var existing = await context.MatricPastPapers.ToListAsync();
        var changed = 0;
        var added = 0;

        // 1. Heal the broken v1 index URL wherever it survives (incl. subjects outside
        //    this sync's scope, e.g. Afrikaans HL / CAT / IT rows).
        foreach (var row in existing.Where(p => p.Url == LegacyBrokenUrl || p.MemoUrl == LegacyBrokenUrl))
        {
            if (row.Url == LegacyBrokenUrl) row.Url = DbeIndexUrl;
            if (row.MemoUrl == LegacyBrokenUrl) row.MemoUrl = DbeIndexUrl;
            changed++;
        }

        // 2. Upsert the verified November catalogue.
        foreach (var (subject, year, paper, directUrl) in NovemberPapers)
        {
            var url = directUrl ?? DbeIndexUrl;
            var notes = directUrl != null
                ? $"{subject} — November {year} Paper {paper}. Verified direct DBE link (2026-07-07)."
                : $"{subject} — November {year} Paper {paper}. DBE index page — direct link not verifiable.";

            var row = existing.FirstOrDefault(p =>
                p.Subject == subject && p.Year == year && p.PaperNumber == paper &&
                p.Language == "English" && p.PaperType == PastPaperType.NSCNovember);

            if (row == null)
            {
                context.MatricPastPapers.Add(new MatricPastPaper
                {
                    MatricPastPaperId = Guid.NewGuid(),
                    Subject = subject,
                    Year = year,
                    PaperNumber = paper,
                    PaperType = PastPaperType.NSCNovember,
                    Grade = 12,
                    IsActive = true,
                    Language = "English",
                    Url = url,
                    HasMemo = false,
                    MemoUrl = null,
                    Notes = notes,
                });
                added++;
            }
            else if (row.Url != url)
            {
                row.Url = url;
                row.Notes = notes;
                // Memo links were never verified — clear the legacy placeholder rather than 404.
                row.HasMemo = false;
                row.MemoUrl = null;
                changed++;
            }
        }

        // 3. Upsert the verified 2014 NSC Exemplar catalogue (Option A). Same key discipline
        //    as the November set, but PaperType = Exemplar, and verified memos ARE seeded.
        foreach (var (subject, paper, ticket, mid, memoTicket) in Exemplars2014)
        {
            var url = Dbe(ticket, ExemplarTab, mid);
            var memoUrl = memoTicket != null ? Dbe(memoTicket, ExemplarTab, mid) : null;
            var notes = memoTicket != null
                ? $"{subject} — 2014 NSC Exemplar Paper {paper}. Paper + memo verified direct DBE links (2026-07-08)."
                : $"{subject} — 2014 NSC Exemplar Paper {paper}. Verified direct DBE link (2026-07-08); no verifiable English memo.";

            var row = existing.FirstOrDefault(p =>
                p.Subject == subject && p.Year == ExemplarYear && p.PaperNumber == paper &&
                p.Language == "English" && p.PaperType == PastPaperType.Exemplar);

            if (row == null)
            {
                context.MatricPastPapers.Add(new MatricPastPaper
                {
                    MatricPastPaperId = Guid.NewGuid(),
                    Subject = subject,
                    Year = ExemplarYear,
                    PaperNumber = paper,
                    PaperType = PastPaperType.Exemplar,
                    Grade = 12,
                    IsActive = true,
                    Language = "English",
                    Url = url,
                    HasMemo = memoUrl != null,
                    MemoUrl = memoUrl,
                    Notes = notes,
                });
                added++;
            }
            else if (row.Url != url || row.MemoUrl != memoUrl)
            {
                row.Url = url;
                row.HasMemo = memoUrl != null;
                row.MemoUrl = memoUrl;
                row.Notes = notes;
                changed++;
            }
        }

        // 4. Deactivate phantom rows (never-published papers) — soft delete, keep history.
        foreach (var (subject, year, paper) in PhantomRows)
        {
            var row = existing.FirstOrDefault(p =>
                p.Subject == subject && p.Year == year && p.PaperNumber == paper &&
                p.Language == "English" && p.PaperType == PastPaperType.NSCNovember && p.IsActive);
            if (row != null)
            {
                row.IsActive = false;
                row.Notes = $"{subject} {year} had a single paper — P{paper} was never published by DBE (v1 seed error).";
                changed++;
            }
        }

        if (added > 0 || changed > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Matric past-paper sync: {Added} added, {Changed} updated/healed (Sprint 1.5.2).",
                added, changed);
        }
    }
}
