"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, SchoolSettings, GradeScaleEntry, AcademicTerm } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Plus, Trash2, GraduationCap, CalendarDays, Clock, Hash, Globe, Palette, BookOpen, ChevronRight } from "lucide-react";

interface SchoolTheme {
  primaryColor: string;
  logoUrl?: string;
  fontFamily: string;
  welcomeMessage?: string;
  supportEmail?: string;
}

const DEFAULT_SETTINGS: SchoolSettings = {
  gradingScale: [
    { letter: "A+", minPercent: 97, maxPercent: 100 },
    { letter: "A",  minPercent: 93, maxPercent: 96  },
    { letter: "A-", minPercent: 90, maxPercent: 92  },
    { letter: "B+", minPercent: 87, maxPercent: 89  },
    { letter: "B",  minPercent: 83, maxPercent: 86  },
    { letter: "B-", minPercent: 80, maxPercent: 82  },
    { letter: "C",  minPercent: 70, maxPercent: 79  },
    { letter: "D",  minPercent: 50, maxPercent: 69  },
    { letter: "F",  minPercent: 0,  maxPercent: 49  },
  ],
  academicTerms: [
    { name: "Term 1", startDate: "2025-01-13", endDate: "2025-04-04" },
    { name: "Term 2", startDate: "2025-04-22", endDate: "2025-07-04" },
    { name: "Term 3", startDate: "2025-07-22", endDate: "2025-10-03" },
    { name: "Term 4", startDate: "2025-10-20", endDate: "2025-12-05" },
  ],
  latePolicy: {
    acceptLate: true,
    gracePeriodHours: 24,
    penaltyPercentPerDay: 10,
    maxPenaltyPercent: 50,
    blockAfterMaxPenalty: false,
  },
  studentIdConfig: { prefix: "STU", nextNumber: 1001, paddingDigits: 4, includeYear: true },
  timezone: "Africa/Johannesburg",
  locale: "en-ZA",
};

function Section({ icon: Icon, title, description, children }: {
  icon: React.ElementType; title: string; description: string; children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-3">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-50">
            <Icon className="h-4 w-4 text-blue-600" />
          </div>
          <div>
            <CardTitle className="text-base">{title}</CardTitle>
            <p className="text-xs text-gray-500 mt-0.5">{description}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

export default function SettingsPage() {
  const router = useRouter();
  const [theme,    setTheme]    = useState<SchoolTheme>({ primaryColor: "#1E40AF", fontFamily: "Inter" });
  const [settings, setSettings] = useState<SchoolSettings>(DEFAULT_SETTINGS);
  const [saving,   setSaving]   = useState<string | null>(null);
  const [saved,    setSaved]    = useState<string | null>(null);

  useEffect(() => {
    api.schools.current().then(s => {
      if (s.theme) setTheme(s.theme as unknown as SchoolTheme);
    }).catch(() => {});
    api.schools.getSettings().then(s => {
      if (s) setSettings(s);
    }).catch(() => {});
  }, []);

  async function save(key: string, fn: () => Promise<unknown>) {
    setSaving(key);
    try {
      await fn();
      setSaved(key);
      setTimeout(() => setSaved(null), 2500);
    } finally { setSaving(null); }
  }

  // Grading scale helpers
  function updateGrade(i: number, field: keyof GradeScaleEntry, val: string | number) {
    setSettings(s => {
      const gs = [...s.gradingScale];
      gs[i] = { ...gs[i], [field]: field === "letter" ? val : Number(val) };
      return { ...s, gradingScale: gs };
    });
  }

  // Term helpers
  function updateTerm(i: number, field: keyof AcademicTerm, val: string) {
    setSettings(s => {
      const terms = [...s.academicTerms];
      terms[i] = { ...terms[i], [field]: val };
      return { ...s, academicTerms: terms };
    });
  }

  function studentIdExample() {
    const { prefix, nextNumber, paddingDigits, includeYear } = settings.studentIdConfig;
    const num = String(nextNumber).padStart(paddingDigits, "0");
    return includeYear ? `${prefix}${new Date().getFullYear()}${num}` : `${prefix}${num}`;
  }

  const SaveBtn = ({ id }: { id: string }) => (
    <div className="flex items-center gap-3 mt-4">
      <Button onClick={() => save(id, () => api.schools.updateSettings(settings))} loading={saving === id}>
        Save
      </Button>
      {saved === id && <span className="text-sm text-green-600">✓ Saved</span>}
    </div>
  );

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">School Settings</h1>
        <p className="text-sm text-gray-500 mt-1">Configure academic policies, grading, and branding for your school.</p>
      </div>

      {/* Branding */}
      <Section icon={Palette} title="Branding & Theme" description="Colours and visual identity">
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="text-sm font-medium text-gray-700 block mb-1">Primary Colour</label>
              <div className="flex items-center gap-2">
                <input type="color" value={theme.primaryColor}
                  onChange={e => setTheme(t => ({ ...t, primaryColor: e.target.value }))}
                  className="h-10 w-16 rounded border border-gray-300 cursor-pointer" />
                <Input value={theme.primaryColor}
                  onChange={e => setTheme(t => ({ ...t, primaryColor: e.target.value }))}
                  placeholder="#1E40AF" className="flex-1" />
              </div>
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700 block mb-1">Font Family</label>
              <select value={theme.fontFamily}
                onChange={e => setTheme(t => ({ ...t, fontFamily: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                {["Inter", "Roboto", "Open Sans", "Poppins", "Lato", "Nunito"].map(f => <option key={f}>{f}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1">Logo URL</label>
            <Input placeholder="https://yourschool.com/logo.png" value={theme.logoUrl ?? ""}
              onChange={e => setTheme(t => ({ ...t, logoUrl: e.target.value || undefined }))} />
          </div>
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1">Welcome Message</label>
            <Input placeholder="Welcome to our school portal" value={theme.welcomeMessage ?? ""}
              onChange={e => setTheme(t => ({ ...t, welcomeMessage: e.target.value || undefined }))} />
          </div>
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1">Support Email</label>
            <Input type="email" placeholder="support@yourschool.com" value={theme.supportEmail ?? ""}
              onChange={e => setTheme(t => ({ ...t, supportEmail: e.target.value || undefined }))} />
          </div>
          <div className="rounded-lg p-4 text-white text-sm font-medium"
            style={{ backgroundColor: theme.primaryColor }}>
            Preview: Sidebar active state
          </div>
          <div className="flex items-center gap-3">
            <Button
              onClick={() => save("theme", async () => {
                await api.schools.updateTheme(theme);
                document.documentElement.style.setProperty("--color-primary", theme.primaryColor);
              })}
              loading={saving === "theme"}
            >
              Save Branding
            </Button>
            {saved === "theme" && <span className="text-sm text-green-600">✓ Saved</span>}
          </div>
        </div>
      </Section>

      {/* Grading Scale */}
      <Section icon={GraduationCap} title="Grading Scale" description="Letter grade thresholds for your school">
        <div className="space-y-2">
          <div className="grid grid-cols-[80px_1fr_1fr_32px] gap-2 text-xs font-medium text-gray-500 px-1 mb-1">
            <span>Letter</span><span>Min %</span><span>Max %</span><span />
          </div>
          {settings.gradingScale.map((g, i) => (
            <div key={i} className="grid grid-cols-[80px_1fr_1fr_32px] gap-2 items-center">
              <Input value={g.letter} onChange={e => updateGrade(i, "letter", e.target.value)} placeholder="A+" />
              <Input type="number" min={0} max={100} value={g.minPercent}
                onChange={e => updateGrade(i, "minPercent", e.target.value)} />
              <Input type="number" min={0} max={100} value={g.maxPercent}
                onChange={e => updateGrade(i, "maxPercent", e.target.value)} />
              <button onClick={() => setSettings(s => ({ ...s, gradingScale: s.gradingScale.filter((_, j) => j !== i) }))}
                className="flex h-8 w-8 items-center justify-center rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors">
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
          <button
            onClick={() => setSettings(s => ({ ...s, gradingScale: [...s.gradingScale, { letter: "", minPercent: 0, maxPercent: 100 }] }))}
            className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 mt-2">
            <Plus className="h-4 w-4" /> Add grade
          </button>
        </div>
        <SaveBtn id="grading" />
      </Section>

      {/* Academic Terms */}
      <Section icon={CalendarDays} title="Academic Terms" description="Define term dates for the school year">
        <div className="space-y-3">
          <div className="grid grid-cols-[1fr_1fr_1fr_32px] gap-2 text-xs font-medium text-gray-500 px-1 mb-1">
            <span>Name</span><span>Start</span><span>End</span><span />
          </div>
          {settings.academicTerms.map((t, i) => (
            <div key={i} className="grid grid-cols-[1fr_1fr_1fr_32px] gap-2 items-center">
              <Input value={t.name} onChange={e => updateTerm(i, "name", e.target.value)} placeholder="Term 1" />
              <Input type="date" value={t.startDate?.split("T")[0] ?? ""} onChange={e => updateTerm(i, "startDate", e.target.value)} />
              <Input type="date" value={t.endDate?.split("T")[0] ?? ""} onChange={e => updateTerm(i, "endDate", e.target.value)} />
              <button
                onClick={() => setSettings(s => ({ ...s, academicTerms: s.academicTerms.filter((_, j) => j !== i) }))}
                className="flex h-8 w-8 items-center justify-center rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors">
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          ))}
          <button
            onClick={() => setSettings(s => ({ ...s, academicTerms: [...s.academicTerms, { name: `Term ${s.academicTerms.length + 1}`, startDate: "", endDate: "" }] }))}
            className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 mt-1">
            <Plus className="h-4 w-4" /> Add term
          </button>
        </div>
        <SaveBtn id="terms" />
      </Section>

      {/* Late Submission Policy */}
      <Section icon={Clock} title="Late Submission Policy" description="How late work is handled and penalised">
        <div className="space-y-4">
          <label className="flex items-center gap-3 cursor-pointer">
            <input type="checkbox"
              checked={settings.latePolicy.acceptLate}
              onChange={e => setSettings(s => ({ ...s, latePolicy: { ...s.latePolicy, acceptLate: e.target.checked } }))}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm font-medium text-gray-700">Accept late submissions</span>
          </label>

          {settings.latePolicy.acceptLate && (
            <div className="grid grid-cols-2 gap-4 pl-7">
              <div>
                <label className="text-sm font-medium text-gray-700 block mb-1">Grace period (hours)</label>
                <Input type="number" min={0} max={168}
                  value={settings.latePolicy.gracePeriodHours}
                  onChange={e => setSettings(s => ({ ...s, latePolicy: { ...s.latePolicy, gracePeriodHours: Number(e.target.value) } }))}
                />
              </div>
              <div>
                <label className="text-sm font-medium text-gray-700 block mb-1">Penalty per day (%)</label>
                <Input type="number" min={0} max={100}
                  value={settings.latePolicy.penaltyPercentPerDay}
                  onChange={e => setSettings(s => ({ ...s, latePolicy: { ...s.latePolicy, penaltyPercentPerDay: Number(e.target.value) } }))}
                />
              </div>
              <div>
                <label className="text-sm font-medium text-gray-700 block mb-1">Max penalty (%)</label>
                <Input type="number" min={0} max={100}
                  value={settings.latePolicy.maxPenaltyPercent}
                  onChange={e => setSettings(s => ({ ...s, latePolicy: { ...s.latePolicy, maxPenaltyPercent: Number(e.target.value) } }))}
                />
              </div>
              <div className="flex items-end">
                <label className="flex items-center gap-2 cursor-pointer mb-2">
                  <input type="checkbox"
                    checked={settings.latePolicy.blockAfterMaxPenalty}
                    onChange={e => setSettings(s => ({ ...s, latePolicy: { ...s.latePolicy, blockAfterMaxPenalty: e.target.checked } }))}
                    className="h-4 w-4 rounded border-gray-300 text-blue-600"
                  />
                  <span className="text-sm text-gray-700">Block after max penalty</span>
                </label>
              </div>
            </div>
          )}
        </div>
        <SaveBtn id="late" />
      </Section>

      {/* Student ID Format */}
      <Section icon={Hash} title="Student ID Format" description="How student IDs are generated at enrollment">
        <div className="space-y-4">
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <div>
              <label className="text-sm font-medium text-gray-700 block mb-1">Prefix</label>
              <Input value={settings.studentIdConfig.prefix}
                onChange={e => setSettings(s => ({ ...s, studentIdConfig: { ...s.studentIdConfig, prefix: e.target.value } }))}
                placeholder="STU" />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700 block mb-1">Padding digits</label>
              <Input type="number" min={2} max={8}
                value={settings.studentIdConfig.paddingDigits}
                onChange={e => setSettings(s => ({ ...s, studentIdConfig: { ...s.studentIdConfig, paddingDigits: Number(e.target.value) } }))}
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700 block mb-1">Next number</label>
              <Input type="number" min={1}
                value={settings.studentIdConfig.nextNumber}
                onChange={e => setSettings(s => ({ ...s, studentIdConfig: { ...s.studentIdConfig, nextNumber: Number(e.target.value) } }))}
              />
            </div>
            <div className="flex flex-col justify-between">
              <label className="text-sm font-medium text-gray-700 block mb-1">Include year</label>
              <label className="flex items-center gap-2 cursor-pointer h-10">
                <input type="checkbox"
                  checked={settings.studentIdConfig.includeYear}
                  onChange={e => setSettings(s => ({ ...s, studentIdConfig: { ...s.studentIdConfig, includeYear: e.target.checked } }))}
                  className="h-4 w-4 rounded border-gray-300 text-blue-600"
                />
                <span className="text-sm text-gray-600">Include year</span>
              </label>
            </div>
          </div>
          <div className="rounded-md bg-gray-50 border border-gray-200 px-4 py-3">
            <span className="text-xs text-gray-500">Example: </span>
            <span className="font-mono text-sm text-gray-800">{studentIdExample()}</span>
          </div>
        </div>
        <SaveBtn id="studentid" />
      </Section>

      {/* Subjects */}
      <button
        onClick={() => router.push("/settings/subjects")}
        className="w-full text-left rounded-xl border border-gray-200 bg-white shadow-sm hover:border-blue-300 hover:shadow-md transition-all"
      >
        <div className="flex items-center gap-4 px-6 py-5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-50 shrink-0">
            <BookOpen className="h-4 w-4 text-blue-600" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-base font-semibold text-gray-900">Subjects</p>
            <p className="text-xs text-gray-500 mt-0.5">Manage your school's CAPS subject list, add custom subjects, or seed the full SA curriculum.</p>
          </div>
          <ChevronRight className="h-4 w-4 text-gray-400 shrink-0" />
        </div>
      </button>

      {/* Timezone & Locale */}
      <Section icon={Globe} title="Timezone & Locale" description="Regional settings for dates and times">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1">Timezone</label>
            <select
              value={settings.timezone}
              onChange={e => setSettings(s => ({ ...s, timezone: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {[
                "Africa/Johannesburg", "Africa/Lagos", "Africa/Nairobi", "Africa/Cairo",
                "America/New_York", "America/Chicago", "America/Los_Angeles",
                "Europe/London", "Europe/Berlin", "Asia/Dubai", "Asia/Kolkata",
                "Australia/Sydney", "Pacific/Auckland",
              ].map(tz => <option key={tz}>{tz}</option>)}
            </select>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-700 block mb-1">Locale</label>
            <select
              value={settings.locale}
              onChange={e => setSettings(s => ({ ...s, locale: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {[
                ["en-ZA", "English (South Africa)"],
                ["en-US", "English (United States)"],
                ["en-GB", "English (United Kingdom)"],
                ["en-NG", "English (Nigeria)"],
                ["en-KE", "English (Kenya)"],
                ["fr-FR", "French (France)"],
                ["pt-BR", "Portuguese (Brazil)"],
                ["ar-EG", "Arabic (Egypt)"],
              ].map(([val, lbl]) => <option key={val} value={val}>{lbl}</option>)}
            </select>
          </div>
        </div>
        <SaveBtn id="locale" />
      </Section>
    </div>
  );
}
