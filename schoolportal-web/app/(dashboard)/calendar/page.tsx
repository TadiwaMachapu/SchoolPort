"use client";
import { useEffect, useState } from "react";
import { api, CalendarEvent, TimetableSlot } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

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
  const [year, setYear] = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth());
  const [events, setEvents] = useState<CalendarEvent[]>([]);
  const [timetable, setTimetable] = useState<TimetableSlot[]>([]);
  const [selected, setSelected] = useState<Date | null>(null);
  const [tab, setTab] = useState<"month" | "timetable">("month");
  const [loading, setLoading] = useState(true);

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

  return (
    <div className="p-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Calendar & Timetable</h1>
        <p className="text-gray-500 mt-1">School events, deadlines, and weekly schedule</p>
      </div>

      <div className="flex gap-2">
        <Button variant={tab === "month" ? "default" : "outline"} onClick={() => setTab("month")} size="sm">
          Monthly View
        </Button>
        <Button variant={tab === "timetable" ? "default" : "outline"} onClick={() => setTab("timetable")} size="sm">
          Weekly Timetable
        </Button>
      </div>

      {loading && <div className="text-gray-400 py-16 text-center">Loading…</div>}

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
                    <div key={d} className="py-2 text-xs font-medium text-gray-500">{d}</div>
                  ))}
                  {cells.map((day, i) => {
                    const dayEvents = day ? eventsForDay(day) : [];
                    const isSelected = selected && day &&
                      selected.getFullYear() === year && selected.getMonth() === month && selected.getDate() === day;
                    return (
                      <div
                        key={i}
                        onClick={() => day && setSelected(new Date(year, month, day))}
                        className={`min-h-[60px] p-1 border border-gray-100 cursor-pointer hover:bg-gray-50 rounded
                          ${isToday(day!) ? "bg-blue-50 border-blue-300" : ""}
                          ${isSelected ? "ring-2 ring-blue-500" : ""}
                          ${!day ? "bg-gray-50 cursor-default" : ""}`}
                      >
                        {day && (
                          <>
                            <span className={`text-xs font-medium ${isToday(day) ? "text-blue-700" : "text-gray-700"}`}>
                              {day}
                            </span>
                            <div className="mt-1 space-y-0.5">
                              {dayEvents.slice(0, 2).map(ev => (
                                <div key={ev.eventId} className={`text-[10px] px-1 rounded truncate ${EVENT_COLORS[ev.type] ?? EVENT_COLORS.other}`}>
                                  {ev.title}
                                </div>
                              ))}
                              {dayEvents.length > 2 && (
                                <div className="text-[10px] text-gray-400">+{dayEvents.length - 2} more</div>
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
                    <p className="text-sm text-gray-400">No events on this day</p>
                  ) : (
                    <div className="space-y-3">
                      {selectedEvents.map(ev => (
                        <div key={ev.eventId} className="border-l-4 border-blue-400 pl-3">
                          <p className="font-medium text-sm text-gray-900">{ev.title}</p>
                          {ev.description && <p className="text-xs text-gray-500 mt-0.5">{ev.description}</p>}
                          <div className="flex items-center gap-2 mt-1">
                            <Badge variant="outline" className="text-xs capitalize">{ev.type}</Badge>
                            {!ev.allDay && (
                              <span className="text-xs text-gray-400">
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
                <CardContent className="p-6 text-center text-gray-400 text-sm">
                  Click a date to see events
                </CardContent>
              </Card>
            )}

            <Card>
              <CardHeader><CardTitle className="text-base">This Month</CardTitle></CardHeader>
              <CardContent>
                {events.length === 0 ? (
                  <p className="text-sm text-gray-400">No events this month</p>
                ) : (
                  <div className="space-y-2">
                    {events.slice(0, 5).map(ev => (
                      <div key={ev.eventId} className="flex items-start gap-2">
                        <span className="text-xs text-gray-400 w-8 shrink-0 pt-0.5">
                          {new Date(ev.startAt).getDate()}
                        </span>
                        <div>
                          <p className="text-sm font-medium text-gray-800">{ev.title}</p>
                          <Badge variant="outline" className="text-[10px] capitalize">{ev.type}</Badge>
                        </div>
                      </div>
                    ))}
                    {events.length > 5 && (
                      <p className="text-xs text-gray-400">+{events.length - 5} more events</p>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {!loading && tab === "timetable" && (
        <Card>
          <CardHeader><CardTitle>Weekly Timetable</CardTitle></CardHeader>
          <CardContent className="p-0">
            {timetable.length === 0 ? (
              <div className="py-16 text-center text-gray-400">
                <div className="text-4xl mb-3">📅</div>
                <p>No timetable slots configured yet</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b bg-gray-50">
                    <tr>
                      {[1,2,3,4,5].map(d => (
                        <th key={d} className="px-4 py-3 text-left font-medium text-gray-600">{DAY_NAMES[d]}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      {[1,2,3,4,5].map(d => (
                        <td key={d} className="px-4 py-3 align-top border-r last:border-r-0">
                          <div className="space-y-2">
                            {slotsByDay(d).length === 0 ? (
                              <span className="text-gray-300 text-xs">—</span>
                            ) : (
                              slotsByDay(d).map(slot => (
                                <div key={slot.slotId} className="bg-blue-50 border border-blue-200 rounded p-2">
                                  <p className="font-medium text-blue-900 text-xs">{slot.subjectName ?? slot.subject}</p>
                                  <p className="text-blue-600 text-[10px]">{slot.startTime} – {slot.endTime}</p>
                                  {slot.room && <p className="text-blue-400 text-[10px]">Room {slot.room}</p>}
                                  <p className="text-blue-400 text-[10px]">{slot.className ?? slot.class}</p>
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
