using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Server.Controllers;
using SchoolPortal.Tests.Security.Infrastructure;
using Xunit;

namespace SchoolPortal.Tests.Security.CrossTenantWriteTests;

/// <summary>
/// Step 10 burn-down — Courses cluster (10 endpoints). Found 3 cross-tenant gaps (CreateCourse
/// ClassSubjectId; ReorderModules courseId; ReorderLessons moduleId — all FIXED), the other 7 were
/// already scoped by id+SchoolId. Every case: foreign id → 404, no row mutated. Caller holds
/// courses.manage (SubjectTeacher).
/// </summary>
[Collection("SecurityApi")]
public class CoursesCrossTenantWriteTests
{
    private readonly ApiFactory _api;
    public CoursesCrossTenantWriteTests(ApiFactory api) => _api = api;

    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.CreateCourse))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.PublishCourse))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.DeleteCourse))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.AddModule))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.DeleteModule))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.ReorderModules))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.AddLesson))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.UpdateLesson))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.DeleteLesson))]
    [CrossTenantGuard(typeof(CoursesController), nameof(CoursesController.ReorderLessons))]
    [Fact]
    public async Task CreateCourse_ForeignClassSubject_Returns404_AndCreatesNoCourse()
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreignCs = await _api.WithScopeAsync(async db => { var b = Seed.School(db); var cs = Seed.ClassSubject(db, b); await db.SaveChangesAsync(); return cs; });

        var resp = await _api.ClientFor(teacher).PostAsJsonAsync("/api/courses", new { classSubjectId = foreignCs, title = "X" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Courses.CountAsync(c => c.SchoolId == schoolA)));
    }

    [Fact]
    public async Task PublishCourse_ForeignCourse_Returns404_AndStaysUnpublished()
    {
        var (teacher, foreignCourse) = await OwnerAndForeign(async (db, b) => Seed.Course(db, b));
        var resp = await _api.ClientFor(teacher).PutAsync($"/api/courses/{foreignCourse}/publish?publish=true", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.False(await _api.WithScopeAsync(db => db.Courses.Where(c => c.CourseId == foreignCourse).Select(c => c.IsPublished).SingleAsync()));
    }

    [Fact]
    public async Task DeleteCourse_ForeignCourse_Returns404_AndStillExists()
    {
        var (teacher, foreignCourse) = await OwnerAndForeign(async (db, b) => Seed.Course(db, b));
        var resp = await _api.ClientFor(teacher).DeleteAsync($"/api/courses/{foreignCourse}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Courses.AnyAsync(c => c.CourseId == foreignCourse)));
    }

    [Fact]
    public async Task AddModule_ForeignCourse_Returns404_AndNoModule()
    {
        var (teacher, foreignCourse) = await OwnerAndForeign(async (db, b) => Seed.Course(db, b));
        var resp = await _api.ClientFor(teacher).PostAsJsonAsync($"/api/courses/{foreignCourse}/modules", new { title = "M", order = 0 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.CourseModules.CountAsync(m => m.CourseId == foreignCourse)));
    }

    [Fact]
    public async Task DeleteModule_ForeignModule_Returns404_AndStillExists()
    {
        var (teacher, foreignModule) = await OwnerAndForeign(async (db, b) => Seed.Module(db, Seed.Course(db, b)));
        var resp = await _api.ClientFor(teacher).DeleteAsync($"/api/courses/modules/{foreignModule}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.CourseModules.AnyAsync(m => m.ModuleId == foreignModule)));
    }

    [Fact]
    public async Task ReorderModules_ForeignCourse_Returns404_AndOrderUnchanged()
    {
        Guid foreignModule = Guid.Empty;
        var (teacher, foreignCourse) = await OwnerAndForeign(async (db, b) => { var c = Seed.Course(db, b); foreignModule = Seed.Module(db, c, order: 7); return c; });
        var resp = await _api.ClientFor(teacher).PutAsJsonAsync($"/api/courses/{foreignCourse}/modules/reorder", new[] { foreignModule });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(7, await _api.WithScopeAsync(db => db.CourseModules.Where(m => m.ModuleId == foreignModule).Select(m => m.Order).SingleAsync()));
    }

    [Fact]
    public async Task AddLesson_ForeignModule_Returns404_AndNoLesson()
    {
        var (teacher, foreignModule) = await OwnerAndForeign(async (db, b) => Seed.Module(db, Seed.Course(db, b)));
        var resp = await _api.ClientFor(teacher).PostAsJsonAsync($"/api/courses/modules/{foreignModule}/lessons", new { title = "L", type = "Text", order = 0 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(0, await _api.WithScopeAsync(db => db.Lessons.CountAsync(l => l.ModuleId == foreignModule)));
    }

    [Fact]
    public async Task UpdateLesson_ForeignLesson_Returns404_AndUnchanged()
    {
        var (teacher, foreignLesson) = await OwnerAndForeign(async (db, b) => Seed.Lesson(db, Seed.Module(db, Seed.Course(db, b))));
        var resp = await _api.ClientFor(teacher).PutAsJsonAsync($"/api/courses/lessons/{foreignLesson}", new { title = "Hijacked", type = "Text", order = 0 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("Lesson", await _api.WithScopeAsync(db => db.Lessons.Where(l => l.LessonId == foreignLesson).Select(l => l.Title).SingleAsync()));
    }

    [Fact]
    public async Task DeleteLesson_ForeignLesson_Returns404_AndStillExists()
    {
        var (teacher, foreignLesson) = await OwnerAndForeign(async (db, b) => Seed.Lesson(db, Seed.Module(db, Seed.Course(db, b))));
        var resp = await _api.ClientFor(teacher).DeleteAsync($"/api/courses/lessons/{foreignLesson}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(await _api.WithScopeAsync(db => db.Lessons.AnyAsync(l => l.LessonId == foreignLesson)));
    }

    [Fact]
    public async Task ReorderLessons_ForeignModule_Returns404_AndOrderUnchanged()
    {
        Guid foreignLesson = Guid.Empty;
        var (teacher, foreignModule) = await OwnerAndForeign(async (db, b) => { var m = Seed.Module(db, Seed.Course(db, b)); foreignLesson = Seed.Lesson(db, m, order: 9); return m; });
        var resp = await _api.ClientFor(teacher).PutAsJsonAsync($"/api/courses/modules/{foreignModule}/lessons/reorder", new[] { foreignLesson });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal(9, await _api.WithScopeAsync(db => db.Lessons.Where(l => l.LessonId == foreignLesson).Select(l => l.Order).SingleAsync()));
    }

    // Mints a SubjectTeacher in school A and seeds a foreign resource in a fresh school B.
    private async Task<(SeededUser Teacher, Guid Foreign)> OwnerAndForeign(Func<SchoolPortalDbContext, Guid, Task<Guid>> seedForeign)
    {
        var schoolA = Guid.NewGuid();
        var teacher = await _api.MintTokenAsync(schoolA, "Staff", "SubjectTeacher");
        var foreign = await _api.WithScopeAsync(async db =>
        {
            var b = Seed.School(db);
            var id = await seedForeign(db, b);
            await db.SaveChangesAsync();
            return id;
        });
        return (teacher, foreign);
    }
}
