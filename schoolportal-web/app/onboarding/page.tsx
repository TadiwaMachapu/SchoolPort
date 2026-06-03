"use client";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type Class, type ImportCsvResult, type User } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  CheckCircle2, ChevronLeft, ChevronRight, Upload, Download,
  Plus, X, Rocket, Building2, Users, GraduationCap, LinkIcon,
  BookOpen, Check,
} from "lucide-react";

// ── Types ───────────────────────────────────────────────────────
interface ClassDraft { name: string; gradeLevel: string; maxCapacity: string; classId?: string; }
interface Features {
  gradebook: boolean; virtualClassroom: boolean;
  smartReports: boolean; saSamsExport: boolean;
  skillsProfile: boolean; pathways: boolean; matricHub: boolean;
  sportsCulture: boolean; schoolPay: boolean;
  schoolChat: boolean; whatsApp: boolean; popiaCentre: boolean;
}

const STEPS = [
  { n: 1, label: "School Info",  Icon: Building2    },
  { n: 2, label: "Structure",    Icon: GraduationCap },
  { n: 3, label: "Teachers",     Icon: Users        },
  { n: 4, label: "Learners",     Icon: Users        },
  { n: 5, label: "Assign",       Icon: LinkIcon     },
  { n: 6, label: "Launch",       Icon: Rocket       },
] as const;

// ── Root wizard ─────────────────────────────────────────────────
export default function OnboardingPage() {
  const router = useRouter();
  const [step, setStep] = useState(1);

  // Step 1 state
  const [schoolName,    setSchoolName]    = useState("");
  const [schoolDomain,  setSchoolDomain]  = useState("");
  const [primaryColor,  setPrimaryColor]  = useState("#1E40AF");
  const [logoUrl,       setLogoUrl]       = useState("");
  const [welcomeMsg,    setWelcomeMsg]    = useState("");
  const [supportEmail,  setSupportEmail]  = useState("");

  // Step 2 state
  const [classes, setClasses] = useState<ClassDraft[]>([]);

  // Step 3 & 4 state
  const [teacherResult, setTeacherResult] = useState<ImportCsvResult | null>(null);
  const [studentResult, setStudentResult] = useState<ImportCsvResult | null>(null);

  // Step 5 state
  const [teachers,     setTeachers]     = useState<User[]>([]);
  const [assignments,  setAssignments]  = useState<Record<string, string>>({}); // classId → userId

  // Step 6 state
  const [features, setFeatures] = useState<Features>({
    gradebook: false, virtualClassroom: false,
    smartReports: false, saSamsExport: false,
    skillsProfile: false, pathways: false, matricHub: false,
    sportsCulture: false, schoolPay: false,
    schoolChat: false, whatsApp: false, popiaCentre: false,
  });

  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState("");

  // Load current school data on mount
  useEffect(() => {
    api.me.get().then(me => {
      if (me.user.role !== "Admin") { router.push("/dashboard"); return; }
    }).catch(() => router.push("/login"));

    api.schools.current().then(s => {
      setSchoolName((s as any).name ?? "");
      setSchoolDomain((s as any).domain ?? "");
      if (s.theme) {
        setPrimaryColor((s.theme as any).primaryColor ?? "#1E40AF");
        setLogoUrl((s.theme as any).logoUrl ?? "");
        setWelcomeMsg((s.theme as any).welcomeMessage ?? "");
        setSupportEmail((s.theme as any).supportEmail ?? "");
      }
      if (s.features) {
        const f = s.features as any;
        setFeatures({
          gradebook:        f.gradebook        ?? false,
          virtualClassroom: f.virtualClassroom ?? false,
          smartReports:     f.smartReports     ?? false,
          saSamsExport:     f.saSamsExport     ?? false,
          skillsProfile:    f.skillsProfile    ?? false,
          pathways:         f.pathways         ?? false,
          matricHub:        f.matricHub        ?? false,
          sportsCulture:    f.sportsCulture    ?? false,
          schoolPay:        f.schoolPay        ?? false,
          schoolChat:       f.schoolChat       ?? false,
          whatsApp:         f.whatsApp         ?? false,
          popiaCentre:      f.popiaCentre      ?? false,
        });
      }
    }).catch(() => {});
  }, []);

  // Load teachers when reaching step 5
  useEffect(() => {
    if (step === 5) {
      api.users.list({ role: "Teacher", pageSize: 100 })
        .then(r => setTeachers(r.items))
        .catch(() => {});
    }
  }, [step]);

  function advance() { setStep(s => Math.min(s + 1, 6) as typeof s); setError(""); }
  function retreat() { setStep(s => Math.max(s - 1, 1) as typeof s); setError(""); }

  /* ── Step 1 save ── */
  async function saveStep1() {
    setSaving(true); setError("");
    try {
      if (schoolName.trim()) await api.schools.updateInfo({ name: schoolName.trim(), domain: schoolDomain || undefined });
      await api.schools.updateTheme({ primaryColor, logoUrl: logoUrl || undefined, welcomeMessage: welcomeMsg || undefined, supportEmail: supportEmail || undefined, fontFamily: "Inter" });
      advance();
    } catch (e: unknown) { setError(e instanceof Error ? e.message : "Save failed"); }
    finally { setSaving(false); }
  }

  /* ── Step 2 save ── */
  async function saveStep2() {
    setSaving(true); setError("");
    try {
      const updated = [...classes];
      for (let i = 0; i < updated.length; i++) {
        if (!updated[i].classId && updated[i].name.trim()) {
          const created = await api.classes.create({
            name: updated[i].name.trim(),
            gradeLevel: updated[i].gradeLevel ? Number(updated[i].gradeLevel) : undefined,
            maxCapacity: updated[i].maxCapacity ? Number(updated[i].maxCapacity) : undefined,
          });
          updated[i] = { ...updated[i], classId: created.classId };
        }
      }
      setClasses(updated);
      advance();
    } catch (e: unknown) { setError(e instanceof Error ? e.message : "Failed to create classes"); }
    finally { setSaving(false); }
  }

  /* ── Step 5 save ── */
  async function saveStep5() {
    setSaving(true); setError("");
    try {
      for (const cls of classes.filter(c => c.classId)) {
        const tid = assignments[cls.classId!];
        if (tid) {
          await api.classes.update(cls.classId!, {
            name: cls.name,
            gradeLevel: cls.gradeLevel ? Number(cls.gradeLevel) : undefined,
            maxCapacity: cls.maxCapacity ? Number(cls.maxCapacity) : undefined,
            teacherId: tid,
          });
        }
      }
      advance();
    } catch (e: unknown) { setError(e instanceof Error ? e.message : "Failed to save assignments"); }
    finally { setSaving(false); }
  }

  /* ── Step 6 launch ── */
  async function launch() {
    setSaving(true); setError("");
    try {
      await api.schools.updateFeatures({
        gradebook:        features.gradebook,
        virtualClassroom: features.virtualClassroom,
        smartReports:     features.smartReports,
        saSamsExport:     features.saSamsExport,
        skillsProfile:    features.skillsProfile,
        pathways:         features.pathways,
        matricHub:        features.matricHub,
        sportsCulture:    features.sportsCulture,
        schoolPay:        features.schoolPay,
        schoolChat:       features.schoolChat,
        whatsApp:         features.whatsApp,
        popiaCentre:      features.popiaCentre,
      });
      router.push("/dashboard");
    } catch (e: unknown) { setError(e instanceof Error ? e.message : "Launch failed"); }
    finally { setSaving(false); }
  }

  const progress = ((step - 1) / 5) * 100;

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* ── Top bar ─────────────────────────────────────────── */}
      <header className="bg-white border-b border-gray-100 px-6 py-4 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <div className="h-8 w-8 rounded-lg flex items-center justify-center text-white text-sm font-bold" style={{ backgroundColor: primaryColor }}>
            {schoolName ? schoolName.charAt(0).toUpperCase() : "S"}
          </div>
          <div>
            <p className="text-xs text-gray-400 font-medium">School Portal</p>
            <p className="text-sm font-semibold text-gray-900 leading-none mt-0.5">{schoolName || "Setup Wizard"}</p>
          </div>
        </div>
        <button onClick={() => router.push("/dashboard")} className="text-sm text-gray-400 hover:text-gray-600 transition-colors">
          Exit to dashboard →
        </button>
      </header>

      {/* ── Step progress ────────────────────────────────────── */}
      <div className="bg-white border-b border-gray-100 px-6 py-4 shrink-0">
        {/* Bar */}
        <div className="relative h-1 bg-gray-100 rounded-full mb-4 max-w-2xl mx-auto">
          <div className="absolute left-0 top-0 h-full rounded-full bg-blue-600 transition-all duration-500"
            style={{ width: `${progress}%` }} />
        </div>
        {/* Steps */}
        <div className="flex items-center justify-between max-w-2xl mx-auto">
          {STEPS.map(({ n, label, Icon }) => {
            const done    = step > n;
            const current = step === n;
            return (
              <div key={n} className="flex flex-col items-center gap-1.5">
                <div className={`h-8 w-8 rounded-full flex items-center justify-center transition-all ${
                  done    ? "bg-emerald-500 text-white" :
                  current ? "bg-blue-600 text-white ring-4 ring-blue-100" :
                            "bg-gray-100 text-gray-400"
                }`}>
                  {done ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
                </div>
                <span className={`text-[10px] font-medium hidden sm:block ${
                  current ? "text-blue-700" : done ? "text-emerald-600" : "text-gray-400"
                }`}>{label}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* ── Content ─────────────────────────────────────────── */}
      <main className="flex-1 flex flex-col items-center px-4 py-8">
        <div className="w-full max-w-2xl space-y-6">
          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{error}</div>
          )}

          {step === 1 && (
            <Step1SchoolInfo
              schoolName={schoolName} setSchoolName={setSchoolName}
              schoolDomain={schoolDomain} setSchoolDomain={setSchoolDomain}
              primaryColor={primaryColor} setPrimaryColor={setPrimaryColor}
              logoUrl={logoUrl} setLogoUrl={setLogoUrl}
              welcomeMsg={welcomeMsg} setWelcomeMsg={setWelcomeMsg}
              supportEmail={supportEmail} setSupportEmail={setSupportEmail}
              saving={saving} onNext={saveStep1} onSkip={advance}
            />
          )}

          {step === 2 && (
            <Step2Structure
              classes={classes} setClasses={setClasses}
              saving={saving} onNext={saveStep2} onBack={retreat} onSkip={advance}
            />
          )}

          {step === 3 && (
            <StepCsvImport
              role="Teacher"
              result={teacherResult} setResult={setTeacherResult}
              onNext={advance} onBack={retreat} onSkip={advance}
            />
          )}

          {step === 4 && (
            <StepCsvImport
              role="Student"
              displayRole="Learner"
              result={studentResult} setResult={setStudentResult}
              onNext={advance} onBack={retreat} onSkip={advance}
            />
          )}

          {step === 5 && (
            <Step5Assign
              classes={classes} teachers={teachers}
              assignments={assignments} setAssignments={setAssignments}
              saving={saving} onNext={saveStep5} onBack={retreat} onSkip={advance}
            />
          )}

          {step === 6 && (
            <Step6Launch
              classes={classes} teacherResult={teacherResult} studentResult={studentResult}
              features={features} setFeatures={setFeatures}
              saving={saving} onLaunch={launch} onBack={retreat}
            />
          )}
        </div>
      </main>
    </div>
  );
}

// ── Step 1: School Info ──────────────────────────────────────────
function Step1SchoolInfo({ schoolName, setSchoolName, schoolDomain, setSchoolDomain, primaryColor, setPrimaryColor, logoUrl, setLogoUrl, welcomeMsg, setWelcomeMsg, supportEmail, setSupportEmail, saving, onNext, onSkip }: {
  schoolName: string; setSchoolName: (v: string) => void;
  schoolDomain: string; setSchoolDomain: (v: string) => void;
  primaryColor: string; setPrimaryColor: (v: string) => void;
  logoUrl: string; setLogoUrl: (v: string) => void;
  welcomeMsg: string; setWelcomeMsg: (v: string) => void;
  supportEmail: string; setSupportEmail: (v: string) => void;
  saving: boolean; onNext: () => void; onSkip: () => void;
}) {
  return (
    <div className="space-y-6">
      <WizardCard title="School Information" subtitle="Tell us about your school — this appears across the portal">
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Field label="School name" required>
              <Input value={schoolName} onChange={e => setSchoolName(e.target.value)} placeholder="Lincoln Academy" autoFocus />
            </Field>
            <Field label="Website / domain">
              <Input value={schoolDomain} onChange={e => setSchoolDomain(e.target.value)} placeholder="lincolnacademy.edu" />
            </Field>
          </div>
          <Field label="Support email">
            <Input type="email" value={supportEmail} onChange={e => setSupportEmail(e.target.value)} placeholder="admin@yourschool.com" />
          </Field>
          <Field label="Welcome message">
            <textarea rows={2} value={welcomeMsg} onChange={e => setWelcomeMsg(e.target.value)} placeholder="Welcome to our school portal!"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </Field>
        </div>
      </WizardCard>

      <WizardCard title="Branding" subtitle="Customise colours and your logo">
        <div className="space-y-4">
          <Field label="Primary colour">
            <div className="flex items-center gap-3">
              <input type="color" value={primaryColor} onChange={e => setPrimaryColor(e.target.value)}
                className="h-10 w-14 rounded border border-gray-300 cursor-pointer" />
              <div className="flex-1 h-10 rounded-lg flex items-center px-4 text-sm font-medium text-white shadow-sm"
                style={{ backgroundColor: primaryColor }}>
                Preview — sidebar active state
              </div>
            </div>
          </Field>
          <Field label="Logo URL">
            <Input value={logoUrl} onChange={e => setLogoUrl(e.target.value)} placeholder="https://yourschool.com/logo.png" />
          </Field>
          {logoUrl && (
            <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-200">
              <img src={logoUrl} alt="Logo preview" className="h-10 w-10 rounded object-contain border border-gray-200 bg-white" onError={e => { (e.target as HTMLImageElement).style.display = "none"; }} />
              <span className="text-sm text-gray-500">Logo preview</span>
            </div>
          )}
        </div>
      </WizardCard>

      <StepNav saving={saving} onNext={onNext} onSkip={onSkip} nextLabel="Save & Continue" />
    </div>
  );
}

// ── Step 2: Academic Structure ───────────────────────────────────
function Step2Structure({ classes, setClasses, saving, onNext, onBack, onSkip }: {
  classes: ClassDraft[]; setClasses: (v: ClassDraft[]) => void;
  saving: boolean; onNext: () => void; onBack: () => void; onSkip: () => void;
}) {
  const [form, setForm] = useState({ name: "", gradeLevel: "", maxCapacity: "" });

  function add() {
    if (!form.name.trim()) return;
    setClasses([...classes, { ...form }]);
    setForm({ name: "", gradeLevel: "", maxCapacity: "" });
  }

  function remove(i: number) {
    setClasses(classes.filter((_, idx) => idx !== i));
  }

  return (
    <div className="space-y-6">
      <WizardCard title="Academic Structure" subtitle="Create your classes and grade levels — you can add more later from the Classes page">
        <div className="space-y-3">
          {/* Add form */}
          <div className="grid grid-cols-12 gap-2 items-end">
            <div className="col-span-5">
              <label className="text-xs font-medium text-gray-600 block mb-1">Class name *</label>
              <Input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                placeholder="Grade 7A" onKeyDown={e => e.key === "Enter" && add()} autoFocus />
            </div>
            <div className="col-span-3">
              <label className="text-xs font-medium text-gray-600 block mb-1">Grade level</label>
              <Input type="number" min={1} max={13} value={form.gradeLevel}
                onChange={e => setForm(f => ({ ...f, gradeLevel: e.target.value }))} placeholder="7" />
            </div>
            <div className="col-span-3">
              <label className="text-xs font-medium text-gray-600 block mb-1">Capacity</label>
              <Input type="number" min={1} value={form.maxCapacity}
                onChange={e => setForm(f => ({ ...f, maxCapacity: e.target.value }))} placeholder="30" />
            </div>
            <div className="col-span-1">
              <button onClick={add} disabled={!form.name.trim()}
                className="h-10 w-10 rounded-lg bg-blue-600 text-white flex items-center justify-center hover:bg-blue-700 disabled:opacity-40 transition-colors">
                <Plus className="h-4 w-4" />
              </button>
            </div>
          </div>

          {/* Class list */}
          {classes.length > 0 ? (
            <div className="border border-gray-200 rounded-lg overflow-hidden mt-2">
              {classes.map((c, i) => (
                <div key={i} className="flex items-center justify-between px-4 py-3 border-b border-gray-100 last:border-0 hover:bg-gray-50">
                  <div className="flex items-center gap-3">
                    <div className="h-7 w-7 rounded-md bg-blue-50 flex items-center justify-center">
                      <BookOpen className="h-3.5 w-3.5 text-blue-600" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-gray-900">{c.name}</p>
                      <p className="text-xs text-gray-400">
                        {c.gradeLevel ? `Grade ${c.gradeLevel}` : "No grade"} · {c.maxCapacity ? `${c.maxCapacity} students` : "No capacity limit"}
                        {c.classId && <span className="ml-2 text-emerald-600">✓ saved</span>}
                      </p>
                    </div>
                  </div>
                  <button onClick={() => remove(i)} className="p-1.5 rounded-md text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors">
                    <X className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center py-8 text-center border-2 border-dashed border-gray-200 rounded-xl">
              <GraduationCap className="h-8 w-8 text-gray-300 mb-2" />
              <p className="text-sm text-gray-500">No classes yet — add your first class above</p>
            </div>
          )}
        </div>
      </WizardCard>

      <StepNav saving={saving} onNext={onNext} onBack={onBack} onSkip={onSkip}
        nextLabel={classes.length > 0 ? `Save ${classes.length} class${classes.length > 1 ? "es" : ""}` : "Skip for now"} />
    </div>
  );
}

// ── Step 3 & 4: CSV Import (reused for both roles) ───────────────
function StepCsvImport({ role, displayRole, result, setResult, onNext, onBack, onSkip }: {
  role: "Teacher" | "Student";
  displayRole?: string;
  result: ImportCsvResult | null; setResult: (r: ImportCsvResult | null) => void;
  onNext: () => void; onBack: () => void; onSkip: () => void;
}) {
  const label = displayRole ?? role;
  const [file,     setFile]     = useState<File | null>(null);
  const [uploading,setUploading]= useState(false);
  const [error,    setError]    = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  function downloadTemplate() {
    const sample = role === "Teacher"
      ? "FirstName,LastName,Email,Role\nJane,Smith,jsmith@school.com,Teacher\nMark,Jones,mjones@school.com,Teacher"
      : "FirstName,LastName,Email,Role\nAlex,Brown,abrown@school.com,Student\nSam,Davis,sdavis@school.com,Student";
    const blob = new Blob([sample], { type: "text/csv" });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement("a");
    a.href = url; a.download = `${label.toLowerCase()}s_template.csv`;
    a.click(); URL.revokeObjectURL(url);
  }

  async function upload() {
    if (!file) return;
    setUploading(true); setError(""); setResult(null);
    try {
      const res = await api.users.importCsv(file);
      setResult(res);
    } catch (e: unknown) { setError(e instanceof Error ? e.message : "Upload failed"); }
    finally { setUploading(false); }
  }

  const Icon = role === "Teacher" ? Users : GraduationCap;
  const color = role === "Teacher" ? "purple" : "blue";

  return (
    <div className="space-y-6">
      <WizardCard
        title={`Import ${label}s`}
        subtitle={`Upload a CSV to bulk-create ${label.toLowerCase()} accounts. Temporary passwords are auto-generated.`}
      >
        <div className="space-y-4">
          {/* Template download */}
          <div className={`flex items-start gap-4 p-4 rounded-xl bg-${color}-50 border border-${color}-100`}>
            <Icon className={`h-5 w-5 text-${color}-600 mt-0.5 shrink-0`} />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-900">1. Download the template</p>
              <p className="text-xs text-gray-500 mt-0.5">Fill in FirstName, LastName, Email, Role (must be <code className="bg-white px-1 rounded">{role}</code>)</p>
            </div>
            <button onClick={downloadTemplate}
              className="flex items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors shrink-0">
              <Download className="h-3.5 w-3.5" /> Template
            </button>
          </div>

          {/* Upload zone */}
          <div>
            <p className="text-sm font-medium text-gray-700 mb-2">2. Upload your CSV</p>
            <div
              onClick={() => inputRef.current?.click()}
              className={`flex flex-col items-center justify-center border-2 border-dashed rounded-xl py-8 px-4 cursor-pointer transition-colors ${
                file ? "border-blue-400 bg-blue-50" : "border-gray-200 hover:border-blue-300 hover:bg-gray-50"
              }`}
            >
              <Upload className="h-7 w-7 text-gray-400 mb-2" />
              {file ? (
                <p className="text-sm text-blue-700 font-medium">{file.name}</p>
              ) : (
                <>
                  <p className="text-sm text-gray-600">Click to select CSV</p>
                  <p className="text-xs text-gray-400 mt-1">.csv files only</p>
                </>
              )}
              <input ref={inputRef} type="file" accept=".csv" className="hidden"
                onChange={e => { setFile(e.target.files?.[0] ?? null); setResult(null); }} />
            </div>
            {file && (
              <div className="flex justify-end mt-2">
                <Button onClick={upload} loading={uploading} size="sm" disabled={!file}>
                  <Upload className="h-3.5 w-3.5 mr-1.5" /> Upload {file.name}
                </Button>
              </div>
            )}
          </div>

          {/* Results */}
          {error && <div className="rounded-lg bg-red-50 border border-red-200 px-3 py-2 text-sm text-red-700">{error}</div>}
          {result && (
            <div className="rounded-xl border border-gray-200 overflow-hidden">
              <div className="flex items-center gap-3 px-4 py-3 bg-emerald-50 border-b border-gray-200">
                <CheckCircle2 className="h-5 w-5 text-emerald-600" />
                <div>
                  <p className="text-sm font-semibold text-gray-900">{result.created} {label.toLowerCase()}{result.created !== 1 ? "s" : ""} created</p>
                  {result.failed.length > 0 && (
                    <p className="text-xs text-amber-600 mt-0.5">{result.failed.length} row{result.failed.length > 1 ? "s" : ""} skipped</p>
                  )}
                </div>
              </div>
              {result.failed.length > 0 && (
                <div className="px-4 py-3 space-y-1 max-h-32 overflow-y-auto">
                  {result.failed.map((f, i) => (
                    <p key={i} className="text-xs text-gray-600">
                      <span className="font-medium text-gray-800">Row {f.row}:</span> {f.reason}
                    </p>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </WizardCard>

      <StepNav onNext={onNext} onBack={onBack} onSkip={onSkip}
        nextLabel={result ? `Continue (${result.created} ${label.toLowerCase()}s imported)` : "Skip for now"} />
    </div>
  );
}

// ── Step 5: Assign Teachers ──────────────────────────────────────
function Step5Assign({ classes, teachers, assignments, setAssignments, saving, onNext, onBack, onSkip }: {
  classes: ClassDraft[]; teachers: User[];
  assignments: Record<string, string>; setAssignments: (v: Record<string, string>) => void;
  saving: boolean; onNext: () => void; onBack: () => void; onSkip: () => void;
}) {
  const savedClasses = classes.filter(c => c.classId);

  if (savedClasses.length === 0) {
    return (
      <div className="space-y-6">
        <WizardCard title="Assign Teachers" subtitle="No classes were created in the previous step">
          <div className="flex flex-col items-center justify-center py-8 text-center">
            <BookOpen className="h-10 w-10 text-gray-300 mb-3" />
            <p className="text-sm text-gray-500">You can assign teachers to classes later from the Classes page.</p>
          </div>
        </WizardCard>
        <StepNav onNext={onSkip} onBack={onBack} nextLabel="Continue" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <WizardCard title="Assign Teachers to Classes" subtitle="Match each class with a lead teacher — you can change this anytime">
        <div className="space-y-3">
          {teachers.length === 0 ? (
            <div className="text-sm text-gray-500 py-4 text-center">
              No teachers imported yet — you can assign teachers from the Classes page later.
            </div>
          ) : savedClasses.map(cls => (
            <div key={cls.classId} className="flex items-center gap-3 py-2.5 border-b border-gray-100 last:border-0">
              <div className="h-8 w-8 rounded-md bg-blue-50 flex items-center justify-center shrink-0">
                <BookOpen className="h-4 w-4 text-blue-600" />
              </div>
              <p className="text-sm font-medium text-gray-900 flex-1 truncate">{cls.name}</p>
              <select
                value={assignments[cls.classId!] ?? ""}
                onChange={e => setAssignments({ ...assignments, [cls.classId!]: e.target.value })}
                className="rounded-md border border-gray-200 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 max-w-[200px]"
              >
                <option value="">— unassigned —</option>
                {teachers.map(t => (
                  <option key={t.userId} value={t.userId}>
                    {t.firstName} {t.lastName}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>
      </WizardCard>
      <StepNav saving={saving} onNext={onNext} onBack={onBack} onSkip={onSkip}
        nextLabel="Save Assignments" />
    </div>
  );
}

// ── Step 6: Launch ───────────────────────────────────────────────
type PillarGroup = {
  pillar: string;
  modules: { key: keyof Features; label: string; description: string; hint?: string }[];
};

const PILLARS: PillarGroup[] = [
  {
    pillar: "Classroom",
    modules: [
      { key: "gradebook", label: "Gradebook", description: "Capture and manage learner marks per subject", hint: "Enables CAPS subject pre-loading — seed from Settings → Subjects after launch." },
      { key: "virtualClassroom", label: "Virtual Classroom", description: "Online lesson rooms and video sessions" },
    ],
  },
  {
    pillar: "Reports & Insights",
    modules: [
      { key: "smartReports", label: "Smart Reports", description: "Automated progress reports with analytics" },
      { key: "saSamsExport", label: "SA-SAMS Export", description: "Export data in SA-SAMS-compatible format" },
    ],
  },
  {
    pillar: "Pathways",
    modules: [
      { key: "skillsProfile", label: "Skills Profile", description: "Track learner skills and competencies" },
      { key: "pathways", label: "Pathways", description: "Career and subject pathway guidance" },
      { key: "matricHub", label: "Matric Hub", description: "Grade 12 exam prep and tracking" },
    ],
  },
  {
    pillar: "Life at School",
    modules: [
      { key: "sportsCulture", label: "Sports & Culture", description: "Manage teams, fixtures, and cultural events" },
      { key: "schoolPay", label: "SchoolPay", description: "School fee collection and payment tracking (ZAR)" },
    ],
  },
  {
    pillar: "Connect",
    modules: [
      { key: "schoolChat", label: "School Chat", description: "In-app messaging between staff and parents" },
      { key: "whatsApp", label: "WhatsApp Notifications", description: "Send automated updates via WhatsApp" },
      { key: "popiaCentre", label: "POPIA Centre", description: "Manage consents and data subject rights" },
    ],
  },
];

function FeatureToggle({ flag, label, description, hint, features, setFeatures }: {
  flag: keyof Features; label: string; description: string; hint?: string;
  features: Features; setFeatures: (f: Features) => void;
}) {
  return (
    <div className="flex items-start justify-between py-2.5 border-b border-gray-100 last:border-0 gap-4">
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-900">{label}</p>
        <p className="text-xs text-gray-500">{description}</p>
        {hint && features[flag] && (
          <p className="text-xs text-blue-600 mt-0.5">{hint}</p>
        )}
      </div>
      <button
        onClick={() => setFeatures({ ...features, [flag]: !features[flag] })}
        className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors focus:outline-none mt-0.5 ${features[flag] ? "bg-blue-600" : "bg-gray-200"}`}
      >
        <span className={`inline-block h-3.5 w-3.5 transform rounded-full bg-white shadow transition-transform ${features[flag] ? "translate-x-4" : "translate-x-0.5"}`} />
      </button>
    </div>
  );
}

function Step6Launch({ classes, teacherResult, studentResult, features, setFeatures, saving, onLaunch, onBack }: {
  classes: ClassDraft[]; teacherResult: ImportCsvResult | null; studentResult: ImportCsvResult | null;
  features: Features; setFeatures: (f: Features) => void;
  saving: boolean; onLaunch: () => void; onBack: () => void;
}) {
  return (
    <div className="space-y-6">
      {/* Summary */}
      <WizardCard title="You're almost ready!" subtitle="Here's what you've set up">
        <div className="grid grid-cols-3 gap-4">
          <SummaryStat icon={BookOpen}      label="Classes"  value={classes.filter(c => c.classId).length} color="blue" />
          <SummaryStat icon={Users}         label="Teachers" value={teacherResult?.created ?? 0}            color="purple" />
          <SummaryStat icon={GraduationCap} label="Learners" value={studentResult?.created ?? 0}            color="green" />
        </div>
      </WizardCard>

      {/* Pillar feature toggles */}
      <WizardCard title="Enable Features" subtitle="All features are off by default — enable what your school needs now. You can change these anytime from Settings.">
        <div className="space-y-5">
          {PILLARS.map(({ pillar, modules }) => (
            <div key={pillar}>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-1">{pillar}</p>
              <div>
                {modules.map(({ key, label, description, hint }) => (
                  <FeatureToggle
                    key={key}
                    flag={key}
                    label={label}
                    description={description}
                    hint={hint}
                    features={features}
                    setFeatures={setFeatures}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
      </WizardCard>

      {/* Launch button */}
      <div className="flex items-center gap-3 justify-between">
        <button onClick={onBack} className="flex items-center gap-1.5 rounded-lg border border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
          <ChevronLeft className="h-4 w-4" /> Back
        </button>
        <Button onClick={onLaunch} loading={saving} size="lg" className="gap-2 px-8 bg-emerald-600 hover:bg-emerald-700">
          <Rocket className="h-4 w-4" />
          Launch School Portal
        </Button>
      </div>
    </div>
  );
}

// ── Shared helpers ───────────────────────────────────────────────
function WizardCard({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-gray-100">
        <h2 className="text-base font-semibold text-gray-900">{title}</h2>
        {subtitle && <p className="text-sm text-gray-500 mt-0.5">{subtitle}</p>}
      </div>
      <div className="px-6 py-5">{children}</div>
    </div>
  );
}

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label className="text-sm font-medium text-gray-700">
        {label}{required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {children}
    </div>
  );
}

function StepNav({ saving, onNext, onBack, onSkip, nextLabel = "Next" }: {
  saving?: boolean; onNext: () => void; onBack?: () => void;
  onSkip?: () => void; nextLabel?: string;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      {onBack ? (
        <button onClick={onBack} className="flex items-center gap-1.5 rounded-lg border border-gray-200 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
          <ChevronLeft className="h-4 w-4" /> Back
        </button>
      ) : <div />}
      <div className="flex items-center gap-3">
        {onSkip && (
          <button onClick={onSkip} className="text-sm text-gray-400 hover:text-gray-600 transition-colors px-3 py-2">
            Skip for now
          </button>
        )}
        <Button onClick={onNext} loading={saving} className="gap-1.5">
          {nextLabel} <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

function SummaryStat({ icon: Icon, label, value, color }: { icon: React.ElementType; label: string; value: number; color: string }) {
  return (
    <div className="flex flex-col items-center gap-1 py-4 bg-gray-50 rounded-xl border border-gray-100">
      <Icon className={`h-6 w-6 text-${color}-500`} />
      <p className="text-2xl font-bold text-gray-900">{value}</p>
      <p className="text-xs text-gray-500 font-medium">{label}</p>
    </div>
  );
}
