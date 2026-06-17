import { describe, it, expect } from "vitest";
import { deriveNav, type NavContext } from "@/lib/nav";
import type { SchoolFeatures } from "@/lib/theme";

// Sprint 1.5.0 Step 8 (sidebar rebuild) — locks in the corrected nav rules. deriveNav now takes
// (identity, positions, permissions, features, ctx). Each item is gated by BOTH its feature flag
// AND its identity/position/permission rule. Finance-only / IT-only staff get a replacement
// workspace. No hard caps — flag + permission composition bounds the list.

const NO_FLAGS: Partial<SchoolFeatures> = {};
const ALL_FLAGS: Partial<SchoolFeatures> = {
  gradebook: true, virtualClassroom: true, smartReports: true, saSamsExport: true,
  skillsProfile: true, pathways: true, matricHub: true, sportsCulture: true,
  schoolPay: true, schoolChat: true, whatsApp: true, popiaCentre: true,
};
const hrefs = (sections: ReturnType<typeof deriveNav>) =>
  sections.flatMap((s) => s.items.map((i) => i.href));
const nav = (
  identity: string, positions: string[], permissions: string[],
  features: Partial<SchoolFeatures>, ctx: NavContext = {},
) => deriveNav(identity, positions, permissions, features, ctx);

describe("deriveNav — required acceptance scenarios", () => {
  it("1. Learner in Grade 12 sees Matric Hub", () => {
    const links = hrefs(nav("Learner", [], [], { matricHub: true }, { gradeLevel: 12 }));
    expect(links).toContain("/matric");
  });

  it("2. Learner in Grade 10 does NOT see Matric Hub", () => {
    const links = hrefs(nav("Learner", [], [], { matricHub: true }, { gradeLevel: 10 }));
    expect(links).not.toContain("/matric");
  });

  it("3. Teacher with no Finance position does NOT see SchoolPay", () => {
    const links = hrefs(nav("Staff", [], [], { schoolPay: true }));
    expect(links).not.toContain("/school-pay");
  });

  it("4. Parent sees SchoolPay but NOT Gradebook", () => {
    const links = hrefs(nav("Parent", [], [], { schoolPay: true, gradebook: true }));
    expect(links).toContain("/school-pay");
    expect(links).not.toContain("/gradebook");
  });

  it("5. Finance Manager sees SchoolPay but NOT Assignments or Calendar", () => {
    const sections = nav("Staff", ["FinanceManager"], [], { schoolPay: true });
    const links = hrefs(sections);
    expect(sections).toHaveLength(1);
    expect(sections[0].label).toBe("Finance"); // replacement workspace
    expect(links).toContain("/school-pay");
    expect(links).not.toContain("/assignments");
    expect(links).not.toContain("/calendar");
  });

  it("6. Courses is hidden for everyone when virtualClassroom is off", () => {
    expect(hrefs(nav("Learner", [], [], ALL_FLAGS_EXCEPT("virtualClassroom")))).not.toContain("/courses");
    expect(hrefs(nav("Staff", [], [], ALL_FLAGS_EXCEPT("virtualClassroom")))).not.toContain("/courses");
    // sanity: it DOES appear when the flag is on
    expect(hrefs(nav("Learner", [], [], { virtualClassroom: true }))).toContain("/courses");
  });
});

describe("deriveNav — structural invariants", () => {
  it("Learner never sees Classes, Quizzes, Courses(off) or SchoolPay (the over-show bug)", () => {
    const links = hrefs(nav("Learner", [], [], ALL_FLAGS, { gradeLevel: 11 }));
    expect(links).not.toContain("/classes");
    expect(links).not.toContain("/quizzes");
    expect(links).not.toContain("/school-pay");
    expect(links[0]).toBe("/dashboard");
    expect(links).toContain("/my-academics"); // gradebook on
  });

  it("IT-only admin gets the System workspace, base replaced", () => {
    const sections = nav("Staff", ["ITAdministrator"], [], { gradebook: true, schoolPay: true });
    expect(sections).toHaveLength(1);
    expect(sections[0].label).toBe("System");
    expect(hrefs(sections)).not.toContain("/classes");
  });

  it("Finance person who also teaches gets the general nav (union), not the workspace", () => {
    const sections = nav("Staff", ["FinanceManager", "SubjectTeacher"], [], { schoolPay: true });
    expect(sections[0].label).toBe(""); // general section, not "Finance"
    const links = hrefs(sections);
    expect(links).toContain("/classes");      // teaching
    expect(links).toContain("/school-pay");   // holds a finance position
  });

  it("Pathways shows for a teacher only with an advising permission", () => {
    expect(hrefs(nav("Staff", [], [], { pathways: true }))).not.toContain("/pathways");
    expect(hrefs(nav("Staff", [], ["pathways.advise"], { pathways: true }))).toContain("/pathways");
  });

  it("permission-gated staff tools appear only with the permission", () => {
    const base = nav("Staff", [], [], { gradebook: true, smartReports: true });
    expect(hrefs(base)).not.toContain("/gradebook");
    expect(hrefs(base)).not.toContain("/analytics");
    const principal = nav("Staff", ["Principal"],
      ["marks.view_class", "reporting.view", "analytics.view_school"],
      { gradebook: true, smartReports: true });
    expect(hrefs(principal)).toEqual(expect.arrayContaining(["/gradebook", "/reports", "/analytics"]));
  });

  it("HOD reaches Subjects via academics.manage; staff without it do not (Step 9.5 Fix #5)", () => {
    // HOD holds academics.manage but is NOT in the SMT/IT Settings gate — Subjects must still appear.
    const hod = hrefs(nav("Staff", ["HOD"], ["academics.manage"], NO_FLAGS));
    expect(hod).toContain("/settings/subjects");
    expect(hod).not.toContain("/settings"); // the full Settings page stays SMT/IT-only
    // A rank-and-file teacher without academics.manage sees neither.
    const teacher = hrefs(nav("Staff", ["SubjectTeacher"], [], NO_FLAGS));
    expect(teacher).not.toContain("/settings/subjects");
  });

  it("flag off hides a flagged item even when the position qualifies", () => {
    // Principal holds analytics.view_school, but smartReports is off → no Analytics.
    const links = hrefs(nav("Staff", ["Principal"], ["analytics.view_school"], NO_FLAGS));
    expect(links).not.toContain("/analytics");
  });
});

// Helper: all flags on except the named one (for the "hidden when off" assertions).
function ALL_FLAGS_EXCEPT(off: keyof SchoolFeatures): Partial<SchoolFeatures> {
  const copy: Partial<SchoolFeatures> = { ...ALL_FLAGS };
  copy[off] = false;
  return copy;
}
