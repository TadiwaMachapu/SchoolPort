"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

interface SchoolTheme {
  primaryColor: string;
  logoUrl?: string;
  faviconUrl?: string;
  fontFamily: string;
  welcomeMessage?: string;
  supportEmail?: string;
}

interface SchoolFeatures {
  quizzes: boolean;
  attendance: boolean;
  parentPortal: boolean;
  messaging: boolean;
  courses: boolean;
  analytics: boolean;
  aiGrading: boolean;
  plagiarismDetection: boolean;
  sso: boolean;
  customReports: boolean;
  whiteLabel: boolean;
  pluginApi: boolean;
}

const FEATURE_LABELS: Record<keyof SchoolFeatures, { label: string; description: string; tier: string }> = {
  quizzes:             { label: "Quizzes",              description: "Quiz builder and student quiz-taking",                 tier: "Basic" },
  attendance:          { label: "Attendance",           description: "Class attendance tracking",                            tier: "Basic" },
  parentPortal:        { label: "Parent Portal",        description: "Parents view child's grades and attendance",           tier: "Basic" },
  messaging:           { label: "Messaging",            description: "In-app messaging and discussion forums",               tier: "Pro" },
  courses:             { label: "Courses",              description: "Course and lesson content builder",                    tier: "Pro" },
  analytics:           { label: "Analytics",            description: "School-wide analytics and at-risk detection",          tier: "Pro" },
  aiGrading:           { label: "AI Grading",           description: "Claude AI suggests grades and feedback",               tier: "Enterprise" },
  plagiarismDetection: { label: "Plagiarism Detection", description: "AI-powered similarity detection across submissions",   tier: "Enterprise" },
  sso:                 { label: "SSO",                  description: "Google Workspace & Microsoft 365 single sign-on",     tier: "Enterprise" },
  customReports:       { label: "Custom Reports",       description: "Custom PDF report cards and exports",                  tier: "Enterprise" },
  whiteLabel:          { label: "White Label",          description: "Full branding customisation and custom domain",        tier: "Enterprise" },
  pluginApi:           { label: "Plugin API",           description: "Third-party plugin marketplace access",                tier: "Enterprise" },
};

const TIER_COLORS: Record<string, string> = {
  Basic: "bg-blue-100 text-blue-700",
  Pro: "bg-purple-100 text-purple-700",
  Enterprise: "bg-orange-100 text-orange-700",
};

export default function SettingsPage() {
  const [theme, setTheme] = useState<SchoolTheme>({ primaryColor: "#1E40AF", fontFamily: "Inter" });
  const [features, setFeatures] = useState<SchoolFeatures>({
    quizzes: true, attendance: true, parentPortal: true, messaging: true,
    courses: true, analytics: true, aiGrading: false, plagiarismDetection: false,
    sso: false, customReports: false, whiteLabel: false, pluginApi: false
  });
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState("");

  useEffect(() => {
    api.schools.current().then((s) => {
      if (s.theme) setTheme(s.theme);
      if (s.features) setFeatures(s.features);
    }).catch(() => {});
  }, []);

  async function saveTheme() {
    setSaving(true);
    try {
      await api.schools.updateTheme(theme);
      setSaved("theme");
      document.documentElement.style.setProperty("--color-primary", theme.primaryColor);
      setTimeout(() => setSaved(""), 2000);
    } finally { setSaving(false); }
  }

  async function saveFeatures() {
    setSaving(true);
    try {
      await api.schools.updateFeatures(features);
      setSaved("features");
      setTimeout(() => setSaved(""), 2000);
    } finally { setSaving(false); }
  }

  return (
    <div className="p-6 lg:p-8 max-w-3xl mx-auto space-y-8">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">School Settings</h1>
        <p className="text-sm text-gray-500 mt-1">Customise your school's branding and enabled features</p>
      </div>

      {/* Theme */}
      <Card>
        <CardHeader>
          <CardTitle>Branding & Theme</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
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
                {["Inter", "Roboto", "Open Sans", "Poppins", "Lato", "Nunito"].map(f => (
                  <option key={f}>{f}</option>
                ))}
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

          {/* Live Preview */}
          <div className="rounded-lg p-4 text-white text-sm font-medium"
            style={{ backgroundColor: theme.primaryColor }}>
            Preview: Sidebar active state with this colour
          </div>

          <div className="flex items-center gap-3">
            <Button onClick={saveTheme} loading={saving}>Save Branding</Button>
            {saved === "theme" && <span className="text-sm text-green-600">✓ Saved</span>}
          </div>
        </CardContent>
      </Card>

      {/* Feature Flags */}
      <Card>
        <CardHeader>
          <CardTitle>Feature Modules</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-gray-500 mb-4">Enable or disable modules for your school. Enterprise features require an Enterprise plan.</p>
          {(Object.keys(FEATURE_LABELS) as Array<keyof SchoolFeatures>).map((key) => {
            const meta = FEATURE_LABELS[key];
            return (
              <div key={key} className="flex items-center justify-between py-3 border-b border-gray-100 last:border-0">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{meta.label}</span>
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${TIER_COLORS[meta.tier]}`}>
                      {meta.tier}
                    </span>
                  </div>
                  <p className="text-xs text-gray-500 mt-0.5">{meta.description}</p>
                </div>
                <button
                  onClick={() => setFeatures(f => ({ ...f, [key]: !f[key] }))}
                  className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none ${features[key] ? "bg-blue-600" : "bg-gray-200"}`}
                >
                  <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${features[key] ? "translate-x-6" : "translate-x-1"}`} />
                </button>
              </div>
            );
          })}

          <div className="flex items-center gap-3 pt-2">
            <Button onClick={saveFeatures} loading={saving}>Save Features</Button>
            {saved === "features" && <span className="text-sm text-green-600">✓ Saved</span>}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
