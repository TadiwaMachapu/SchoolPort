"use client";
import { useEffect, useState } from "react";
import { api, CalendarEvent, TimetableSlot } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { usePermission } from "@/lib/auth-context";
import { CalendarDays } from "lucide-react";

const DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const DAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const MONTHS = ["January","February","March","April","May","June","July","August","September","October","November","December"];

const EVENT_COLORS: Record<string, string> = {
  assignment: "bg-blue-100 text-blue-800",
  Assignment: "bg-blue-100 text-blue-800",
  exam: "bg-red-100 text-red-800",
  holiday: "bg-green-100 text-green-800",
  meeting: "bg-purple-100 text-purple-800",
  other: "bg-gray-100 text-gray-700",
};

export default function CalendarPage() {
  const today = new Date();
  const [year,      setYear]      = useState(today.getFullYear());
  const [month,     setMonth]     = useState(today.getMonth());
  const [events,    setEvents]    = useState<CalendarEvent[]>([]);
  const [timetable, setTimetable] = useState<TimetableSlot[]>([]);
  const [selected,  setSelected]  = useState<Date | null>(null);
  const [tab,       setTab]       = useState<"month" | "timetable">("month");
  const [loading,   setLoading]   = useState(true);
  const [showAdd,   setShowAdd]   = useState(false);
  const canManageCalendar = usePermission("calendar.manage"); // Step 8

  useEffect(() => {
    const from = new Date(year, month, 1).toISOString();
    const to = new Date(year, month + 1, 0, 23, 59, 59).toISOString();
    Promise.allSettled([
      api.calendar.events({ from, to }),
      api.calendar.timetable(),
    ]).then(([ev, tt]) => {
      if (ev.status === "fulfilled") {
        const data = ev.value as { events?: CalendarEvent[]; assignmentDueDates?: CalendarEvent[] };
        const calEvents: CalendarEvent[] = [
          ...(data.events ?? []),
          ...(data.assignmentDueDates ?? []).map((a: any) => ({
            eventId: a.eventId,
            title: a.title,
            type: "Assignment",
            startAt: a.startAt,
            endAt: a.endAt,
            allDay: false,
            classId: a.classId,
          })),
        ];
        setEvents(calEvents);
      }
      if (tt.status === "fulfilled") setTimetable(tt.value as TimetableSlot[]);
    }).finally(() => setLoading(false));
  }, [year, month]);

  const prevMonth = () => {
    if (month === 0) { setMonth(11); setYear(y => y - 1); }
    else setMonth(m => m - 1);
  };
  const nextMonth = () => {
    if (month === 11) { setMonth(0); setYear(y => y + 1); }
    else setMonth(m => m + 1);
  };

  const firstDay = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const cells: (number | null)[] = [
    ...Array(firstDay).fill(null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];
  while (cells.length % 7 !== 0) cells.push(null);

  const eventsForDay = (day: number) =>
    events.filter(e => {
      const d = new Date(e.startAt);
      return d.getFullYear() === year && d.getMonth() === month && d.getDate() === day;
    });

  const selectedEvents = selected ? eventsForDay(selected.getDate()) : [];
  const isToday = (day: number) =>
    today.getFullYear() === year && today.getMonth() === month && today.getDate() === day;

  const slotsByDay = (dow: number) => timetable.filter(s => s.dayOfWeek === dow);

  async function loadEvents() {
    const from = new Date(year, month, 1).toISOString();
    const to   = new Date(year, month + 1, 0, 23, 59, 59).toISOString();
    const [ev] = await Promise.allSettled([api.calendar.events({ from, to })]);
    if (ev.status === "fulfilled") {
      const data = ev.value as { events?: CalendarEvent[]; assignmentDueDates?: CalendarEvent[] };
      setEvents([...(data.events ?? []), ...(data.assignmentDueDates ?? []).map((a: any) => ({ ...a, type: "Assignment" }))]);
    }
  }

  return (
    <div className="p-6 lg:p-8 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-text-primary tracking-tight">Calendar & Timetable</h1>
          <p className="text-sm text-text-secondary mt-1">School events, deadlines, and weekly schedule</p>
        </div>
        {canManageCalendar && (
          <Button size="sm" onClick={() => setShowAdd(true)}>+ Add Event</Button>
        )}
      </div>

      <div className="flex gap-2">
        <Button variant={tab === "month" ? "default" : "outline"} onClick={() => setTab("month")} size="sm">
          Monthly View
        </Button>
        <Button variant={tab === "timetable" ? "default" : "outline"} onClick={() => setTab("timetable")} size="sm">
          Weekly Timetable
        </Button>
      </div>

      {loading && <div className="text-text-muted py-16 text-center">Loading…</div>}

      {!loading && tab === "month" && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <Card>
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <Button variant="ghost" size="sm" onClick={prevMonth}>‹</Button>
                  <CardTitle className="text-lg">{MONTHS[month]} {year}</CardTitle>
                  <Button variant="ghost" size="sm" onClick={nextMonth}>›</Button>
                </div>
              </CardHeader>
              <CardContent className="p-2">
                <div className="grid grid-cols-7 text-center">
                  {DAYS.map(d => (
                    <div key={d} className="py-2 text-xs font-medium text-text-muted">{d}</div>
                  ))}
                  {cells.map((day, i) => {
                    const dayEvents = day ? eventsForDay(day) : [];
                    const isSelected = selected && day &&
                      selected.getFullYear() === year && selected.getMonth() === month && selected.getDate() === day;
                    return (
                      <div
                        key={i}
                        onClick={() => day && setSelected(new Date(year, month, day))}
                        className={`min-h-[60px] p-1 border border-border cursor-pointer hover:bg-surface-subtle rounded
                          ${isToday(day!) ? "bg-primary-50 border-primary-300" : ""}
                          ${isSelected ? "ring-2 ring-primary" : ""}
                          ${!day ? "bg-surface-subtle cursor-default" : ""}`}
                      >
                        {day && (
                          <>
                            <span className={`text-xs font-medium ${isToday(day) ? "text-primary" : "text-text-primary"}`}>
                              {day}
                            </span>
                            <div className="mt-1 space-y-0.5">
                              {dayEvents.slice(0, 2).map(ev => (
                                <div key={ev.eventId} className={`text-[10px] px-1 rounded truncate ${EVENT_COLORS[ev.type] ?? EVENT_COLORS.other}`}>
                                  {ev.title}
                                </div>
                              ))}
                              {dayEvents.length > 2 && (
                                <div className="text-[10px] text-text-muted">+{dayEvents.length - 2} more</div>
                              )}
                            </div>
                          </>
                        )}
                      </div>
                    );
                  })}
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="space-y-4">
            {selected ? (
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">
                    {selected.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" })}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  {selectedEvents.length === 0 ? (
                    <p className="text-sm text-text-muted">No events on this day</p>
                  ) : (
                    <div className="space-y-3">
                      {selectedEvents.map(ev => (
                        <div key={ev.eventId} className="border-l-4 border-primary pl-3">
                          <p className="font-medium text-sm text-text-primary">{ev.title}</p>
                          {ev.description && <p className="text-xs text-text-secondary mt-0.5">{ev.description}</p>}
                          <div className="flex items-center gap-2 mt-1">
                            <Badge variant="outline" className="text-xs capitalize">{ev.type}</Badge>
                            {!ev.allDay && (
                              <span className="text-xs text-text-muted">
                                {new Date(ev.startAt).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })}
                                {ev.endAt && ` – ${new Date(ev.endAt).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })}`}
                              </span>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            ) : (
              <Card>
                <CardContent className="p-6 text-center text-text-muted text-sm">
                  Click a date to see events
                </CardContent>
              </Card>
            )}

            <Card>
              <CardHeader><CardTitle className="text-base">This Month</CardTitle></CardHeader>
              <CardContent>
                {events.length === 0 ? (
                  <p className="text-sm text-text-muted">No events this month</p>
                ) : (
                  <div className="space-y-2">
                    {events.slice(0, 5).map(ev => (
                      <div key={ev.eventId} className="flex items-start gap-2">
                        <span className="text-xs text-text-muted w-8 shrink-0 pt-0.5">
                          {new Date(ev.startAt).getDate()}
                        </span>
                        <div>
                          <p className="text-sm font-medium text-text-primary">{ev.title}</p>
                          <Badge variant="outline" className="text-[10px] capitalize">{ev.type}</Badge>
                        </div>
                      </div>
                    ))}
                    {events.length > 5 && (
                      <p className="text-xs text-text-muted">+{events.length - 5} more events</p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {showAdd && (
        <AddEventModal
          defaultDate={selected ?? today}
          onClose={() => { setShowAdd(false); loadEvents(); }}
        />
      )}

      {!loading && tab === "timetable" && (
        <Card>
          <CardHeader><CardTitle>Weekly Timetable</CardTitle></CardHeader>
          <CardContent className="p-0">
            {timetable.length === 0 ? (
              <div className="py-16 text-center text-text-muted">
                <div className="flex justify-center mb-3">
                  <CalendarDays className="h-10 w-10 text-text-muted" />
                </div>
                <p>No timetable slots configured yet</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b bg-surface-subtle">
                    <tr>
                      {[1,2,3,4,5].map(d => (
                        <th key={d} className="px-4 py-3 text-left font-medium text-text-secondary">{DAY_NAMES[d]}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      {[1,2,3,4,5].map(d => (
                        <td key={d} className="px-4 py-3 align-top border-r last:border-r-0">
                          <div className="space-y-2">
                            {slotsByDay(d).length === 0 ? (
                              <span className="text-text-muted text-xs">—</span>
                            ) : (
                              slotsByDay(d).map(slot => (
                                <div key={slot.slotId} className="bg-primary-50 border border-primary-200 rounded p-2">
                                  <p className="font-medium text-primary-900 text-xs">{slot.subjectName ?? slot.subject}</p>
                                  <p className="text-primary text-[10px]">{slot.startTime} – {slot.endTime}</p>
                                  {slot.room && <p className="text-primary text-[10px]">Room {slot.room}</p>}
                                  <p className="text-primary text-[10px]">{slot.className ?? slot.class}</p>
                                </div>
                              ))
                            )}
                          </div>
                        </td>
                      ))}
                    </tr>
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

const EVENT_TYPES = ["exam", "holiday", "meeting", "other"] as const;

function AddEventModal({ defaultDate, onClose }: { defaultDate: Date; onClose: () => void }) {
  const pad = (n: number) => String(n).padStart(2, "0");
  const defaultISO = `${defaultDate.getFullYear()}-${pad(defaultDate.getMonth()+1)}-${pad(defaultDate.getDate())}`;
  const [form, setForm] = useState({
    title: "", description: "", type: "other",
    startAt: `${defaultISO}T09:00`, endAt: `${defaultISO}T10:00`, allDay: false,
  });
  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true); setError("");
    try {
      await api.calendar.create({
        title:       form.title,
        description: form.description || undefined,
        type:        form.type,
        startAt:     new Date(form.startAt).toISOString(),
        endAt:       form.allDay ? undefined : new Date(form.endAt).toISOString(),
        allDay:      form.allDay,
      });
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally { setSaving(false); }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-surface-card shadow-2xl">
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h2 className="text-lg font-semibold text-text-primary">Add Event</h2>
          <button onClick={onClose} className="rounded-full p-1 text-text-muted hover:bg-surface-subtle hover:text-text-secondary transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-text-primary">Title</label>
            <Input placeholder="e.g. End of Term Exam" value={form.title}
              onChange={e => setForm(f => ({ ...f, title: e.target.value }))} required autoFocus />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">Type</label>
              <select value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value }))}
                className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
                {EVENT_TYPES.map(t => <option key={t} value={t} className="capitalize">{t.charAt(0).toUpperCase()+t.slice(1)}</option>)}
              </select>
            </div>
            <div className="space-y-1.5 flex flex-col justify-end">
              <label className="flex items-center gap-2 cursor-pointer pb-2">
                <input type="checkbox" checked={form.allDay}
                  onChange={e => setForm(f => ({ ...f, allDay: e.target.checked }))}
                  className="rounded text-primary" />
                <span className="text-sm font-medium text-text-primary">All day</span>
              </label>
            </div>
          </div>
          <div className={`grid gap-3 ${form.allDay ? "grid-cols-1" : "grid-cols-2"}`}>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-text-primary">{form.allDay ? "Date" : "Start"}</label>
              <input type={form.allDay ? "date" : "datetime-local"} required
                value={form.allDay ? form.startAt.slice(0, 10) : form.startAt}
                onChange={e => setForm(f => ({ ...f, startAt: e.target.value }))}
                className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
            </div>
            {!form.allDay && (
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">End</label>
                <input type="datetime-local"
                  value={form.endAt}
                  onChange={e => setForm(f => ({ ...f, endAt: e.target.value }))}
                  className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
              </div>
            )}
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-text-primary">Description <span className="text-text-muted font-normal">(optional)</span></label>
            <textarea rows={2} placeholder="Additional details…" value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              className="w-full rounded-md border border-border px-3 py-2 text-sm placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-primary resize-none" />
          </div>
          <div className="flex gap-3 pt-1">
            <Button type="submit" className="flex-1" loading={saving}>Add Event</Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
