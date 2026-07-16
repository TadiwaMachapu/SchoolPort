using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.3 — the attendance-rate signal for the reports surfaces. Pins the two rules that stop
/// the LowAttendance false alarm: Late counts as attended, and a too-thin sample yields no signal
/// (null) rather than a misleading 0%.
/// </summary>
public class AttendanceSignalTests
{
    [Fact]
    public void SparseSample_BelowMinDays_ReturnsNull_NoFalseFlag()
    {
        // The live-demo case: a term with a few stray records (e.g. 1 absent + 2 late = 3 rows).
        // Too thin to judge → null → never flags LowAttendance for a school that hasn't captured.
        Assert.Null(AttendanceSignal.Percent(totalRecorded: 3, absentCount: 1));
        Assert.Null(AttendanceSignal.Percent(totalRecorded: 1, absentCount: 0));
    }

    [Fact]
    public void ZeroRecords_ReturnsNull()
    {
        Assert.Null(AttendanceSignal.Percent(totalRecorded: 0, absentCount: 0));
    }

    [Fact]
    public void MeaningfulSample_AllAttended_Is100()
    {
        // 10 recorded days, none absent (Present OR Late both count as attended) → 100%.
        Assert.Equal(100.0, AttendanceSignal.Percent(totalRecorded: 10, absentCount: 0));
    }

    [Fact]
    public void LateCountsAsAttended_OnlyAbsentLowersTheRate()
    {
        // 10 days, 2 absent → 8 attended (however many of those 8 were Late) → 80%. Late never
        // lowers the rate; only Absent does. (Guards the old "count(status==1)" bug where Late→absent.)
        Assert.Equal(80.0, AttendanceSignal.Percent(totalRecorded: 10, absentCount: 2));
    }

    [Fact]
    public void GenuineChronicAbsence_StillSignals()
    {
        // A real signal survives: 20 recorded days, 18 absent → 10% → below the 80% LowAttendance line.
        Assert.Equal(10.0, AttendanceSignal.Percent(totalRecorded: 20, absentCount: 18));
        // All-absent over a meaningful sample is a true 0% (not the false-flag kind).
        Assert.Equal(0.0, AttendanceSignal.Percent(totalRecorded: 10, absentCount: 10));
    }

    [Fact]
    public void AtThreshold_IsASignal()
    {
        // Exactly MinDaysForSignal recorded days is enough to judge.
        Assert.Equal(80.0, AttendanceSignal.Percent(totalRecorded: AttendanceSignal.MinDaysForSignal, absentCount: 1));
    }
}
