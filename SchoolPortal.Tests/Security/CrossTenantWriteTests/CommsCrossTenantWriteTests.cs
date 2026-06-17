using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 Inventory-B — Comms cluster. Cross-tenant writes (Messages DM recipient, class-discussion
/// classId, Calendar event classId, timetable classSubjectId) AND the cross-user (#5) class: posting
/// to a thread you're not a member of. Dual+ assertion: status AND no row written.
/// </summary>
[Collection("SecurityApi")]
public class CommsCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public CommsCrossTenantWriteTests(ApiFactory api) => _api = api;

    // ---- Messages -------------------------------------------------------------------------------

    [CrossTenantGuard(typeof(MessagesController), nameof(MessagesController.CreateDirectThread))]
    [CrossTenantGuard(typeof(MessagesController), nameof(MessagesController.SendMessage))]
    [CrossTenantGuard(typeof(MessagesController), nameof(MessagesController.CreateClassDiscussion))]
    [CrossTenantGuard(typeof(CalendarController), nameof(CalendarController.CreateEvent))]
    [CrossTenantGuard(typeof(CalendarController), nameof(CalendarController.AddTimetableSlot))]
    [CrossTenantGuard(typeof(AnnouncementsController), nameof(AnnouncementsController.UpdateAnnouncement))]
    [Fact]
    public async Task CreateDirectThread_ForeignRecipient_Returns404_AndCreatesNoThread()
    {
        var schoolA = Guid.NewGuid();
        var caller = await _api.MintTokenAsync(schoolA, "Staff");
        var foreignUser = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var u = AddUser(db, schoolB, "Teacher", "Staff");
            await db.SaveChangesAsync();
            return u;
        });

        var resp = await _api.ClientFor(caller).PostAsJsonAsync("/api/messages/threads/direct",
            new { recipientUserId = foreignUser, subject = "hi" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.MessageThreads.CountAsync(t => t.SchoolId == schoolA)));
    }

    [Fact]
    public async Task SendMessage_ToThreadCallerIsNotMemberOf_Returns403_AndCreatesNoMessage()
    {
        // Cross-user (#5 class): membership must gate posting — guessing a threadId you're not in must fail.
        var schoolA = Guid.NewGuid();
        var caller = await _api.MintTokenAsync(schoolA, "Staff");
        var threadId = await _api.WithScopeAsync(async db =>
        {
            var other = AddUser(db, schoolA, "Teacher", "Staff");   // a thread between OTHER users, same school
            var tid = Guid.NewGuid();
            db.MessageThreads.Add(new MessageThread
            {
                ThreadId = tid, SchoolId = schoolA, Type = "Direct", Subject = "private", CreatedAt = DateTime.UtcNow,
                Participants = new List<ThreadParticipant> { new() { ParticipantId = Guid.NewGuid(), UserId = other, JoinedAt = DateTime.UtcNow } }
            });
            await db.SaveChangesAsync();
            return tid;
        });

        var resp = await _api.ClientFor(caller).PostAsJsonAsync($"/api/messages/threads/{threadId}/messages",
            new { content = "I should not be able to post here" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.ChatMessages.CountAsync(m => m.ThreadId == threadId)));
    }

    [Fact]
    public async Task CreateClassDiscussion_ForeignClass_Returns404_AndCreatesNoThread()
    {
        var schoolA = Guid.NewGuid();
        var classTeacher = await _api.MintTokenAsync(schoolA, "Staff", "ClassTeacher");
        var foreignClass = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB);
            await db.SaveChangesAsync();
            return c;
        });

        var resp = await _api.ClientFor(classTeacher).PostAsJsonAsync($"/api/messages/threads/class/{foreignClass}",
            new { recipientUserId = Guid.Empty, subject = "Class chat" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.MessageThreads.CountAsync(t => t.ClassId == foreignClass)));
    }

    // ---- Calendar -------------------------------------------------------------------------------

    [Fact]
    public async Task CreateEvent_ForeignClass_Returns404_AndCreatesNoEvent()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignClass = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var c = AddClass(db, schoolB);
            await db.SaveChangesAsync();
            return c;
        });

        var resp = await _api.ClientFor(principal).PostAsJsonAsync("/api/calendar/events",
            new { title = "Trip", type = "Meeting", startAt = DateTime.UtcNow, endAt = DateTime.UtcNow.AddHours(1), allDay = false, classId = foreignClass });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.CalendarEvents.CountAsync(e => e.SchoolId == schoolA)));
    }

    [Fact]
    public async Task AddTimetableSlot_ForeignClassSubject_Returns404_AndCreatesNoSlot()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignCs = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var cs = AddClassSubject(db, schoolB);
            await db.SaveChangesAsync();
            return cs;
        });

        var resp = await _api.ClientFor(principal).PostAsJsonAsync("/api/calendar/timetable",
            new { classSubjectId = foreignCs, dayOfWeek = 1, startTime = "08:00:00", endTime = "09:00:00", room = "A1" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.TimetableSlots.CountAsync(s => s.SchoolId == schoolA)));
    }

    // ---- Announcements — guarded confirmation ---------------------------------------------------

    [Fact]
    public async Task UpdateAnnouncement_ForeignAnnouncement_Returns404_AndUnchanged()
    {
        var schoolA = Guid.NewGuid();
        var principal = await _api.MintTokenAsync(schoolA, "Staff", "Principal");
        var foreignAnn = await _api.WithScopeAsync(async db =>
        {
            var schoolB = AddSchool(db);
            var creator = AddUser(db, schoolB, "Teacher", "Staff");
            var id = Guid.NewGuid();
            db.Announcements.Add(new Announcement { AnnouncementId = id, SchoolId = schoolB, Title = "B-Notice", Content = "c", Audience = "All", CreatedByUserId = creator, CreatedAt = DateTime.UtcNow, IsActive = true });
            await db.SaveChangesAsync();
            return id;
        });

        var resp = await _api.ClientFor(principal).PutAsJsonAsync($"/api/announcements/{foreignAnn}",
            new { title = "Hijacked", content = "x", audience = "All", audienceValue = (string?)null, expiresAt = (DateTime?)null, isActive = true });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var title = await _api.WithScopeAsync(db => db.Announcements.Where(a => a.AnnouncementId == foreignAnn).Select(a => a.Title).SingleAsync());
        Assert.Equal("B-Notice", title);
    }

    // ---- seed helpers ---------------------------------------------------------------------------

    private static Guid AddSchool(SchoolPortalDbContext db)
    {
        var id = Guid.NewGuid();
        db.Schools.Add(new School { SchoolId = id, Name = "S" + id.ToString("N")[..6], IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddUser(SchoolPortalDbContext db, Guid schoolId, string role, string identity)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User { UserId = id, SchoolId = schoolId, Email = $"u_{id:N}@test.local", PasswordHash = "x", FirstName = "U", LastName = "X", Role = role, Identity = identity, IsActive = true, CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddClass(SchoolPortalDbContext db, Guid schoolId)
    {
        var id = Guid.NewGuid();
        db.Classes.Add(new Class { ClassId = id, SchoolId = schoolId, Name = "C" + id.ToString("N")[..4], CreatedAt = DateTime.UtcNow });
        return id;
    }

    private static Guid AddClassSubject(SchoolPortalDbContext db, Guid schoolId)
    {
        var classId = AddClass(db, schoolId);
        var subjectId = Guid.NewGuid();
        db.Subjects.Add(new Subject { SubjectId = subjectId, SchoolId = schoolId, Name = "Sub" + subjectId.ToString("N")[..4], Code = "S" + subjectId.ToString("N")[..3], CreatedAt = DateTime.UtcNow });
        var id = Guid.NewGuid();
        db.ClassSubjects.Add(new ClassSubject { ClassSubjectId = id, ClassId = classId, SubjectId = subjectId, SchoolId = schoolId, CreatedAt = DateTime.UtcNow });
        return id;
    }
}
