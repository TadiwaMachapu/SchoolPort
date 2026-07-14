namespace SchoolPortal.Server.Services;

/// <summary>
/// Sprint 1.5.3 — the ONE attendance-rate signal shared by the reports surfaces (term-report
/// display + the at-risk LowAttendance flag), so neither false-flags on thin data and the two
/// can't disagree. Fixes two things a raw <c>Present = count(status==1)</c> / total got wrong:
///
/// <list type="bullet">
/// <item>"Late" (status 2) is PRESENT-but-tardy — it counts as attended. Only "Absent" (status 0)
/// lowers the rate. Counting only status==1 made every late learner read as absent (→ 0%).</item>
/// <item>A term with only a handful of stray records — a pilot school that hasn't really captured
/// attendance yet — must NOT read as "0% attendance" for everyone. Below
/// <see cref="MinDaysForSignal"/> recorded days the sample is too thin to judge, so there is no
/// signal (null) and nothing is flagged — the same graceful degradation as zero records. This is
/// the attendance analogue of "absent ≠ zero mark": absence of data is not a bad score.</item>
/// </list>
/// </summary>
public static class AttendanceSignal
{
    // Attendance.Status codes: 0=Absent, 1=Present, 2=Late.
    public const int Absent = 0;

    /// <summary>Minimum recorded days in the window before attendance is a usable signal. Below
    /// this, a couple of stray absence/late rows would otherwise produce a misleading extreme %.</summary>
    public const int MinDaysForSignal = 5;

    /// <summary>Attendance rate % over a window's records, or null when too sparse to judge.
    /// <paramref name="totalRecorded"/> = recorded days; <paramref name="absentCount"/> = days marked
    /// Absent (Present AND Late both count as attended). Null → "no attendance signal", never a flag.</summary>
    public static double? Percent(int totalRecorded, int absentCount)
    {
        if (totalRecorded < MinDaysForSignal) return null;
        return Math.Round((double)(totalRecorded - absentCount) / totalRecorded * 100, 1);
    }
}
