using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data;
using SchoolPortal.Data.Entities;
using SchoolPortal.Shared.DTOs.Schools;

namespace SchoolPortal.Server.Controllers;

[ApiController]
[Route("api/dev")]
public class DevSeedController : ControllerBase
{
    private readonly SchoolPortalDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DevSeedController(SchoolPortalDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [AllowAnonymous]
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        if (!_env.IsDevelopment())
            return Forbid();

        if (await _db.Schools.AnyAsync(s => s.Name == "Greendale High School"))
            return Ok(new { message = "Already seeded. Delete school 'Greendale High School' to re-seed." });

        var now = DateTime.UtcNow;
        var hash = (string pw) => BCrypt.Net.BCrypt.HashPassword(pw);

        // ── IDs ─────────────────────────────────────────────────────────────────
        var schoolId     = Guid.NewGuid();
        var adminId      = Guid.NewGuid();
        var teacher1Id   = Guid.NewGuid(); // user id
        var teacher2Id   = Guid.NewGuid();
        var t1Id         = Guid.NewGuid(); // teacher record id
        var t2Id         = Guid.NewGuid();
        var stu1UserId   = Guid.NewGuid(); var stu1Id = Guid.NewGuid(); // Lethabo  Gr12
        var stu2UserId   = Guid.NewGuid(); var stu2Id = Guid.NewGuid(); // Amahle   Gr12
        var stu3UserId   = Guid.NewGuid(); var stu3Id = Guid.NewGuid(); // Sipho    Gr12
        var stu4UserId   = Guid.NewGuid(); var stu4Id = Guid.NewGuid(); // Zara     Gr9
        var stu5UserId   = Guid.NewGuid(); var stu5Id = Guid.NewGuid(); // Keanu    Gr9
        var par1UserId   = Guid.NewGuid(); // Nomsa   (parent of Lethabo + Amahle)
        var par2UserId   = Guid.NewGuid(); // Bongani (parent of Sipho  + Zara)
        var subMathFet   = Guid.NewGuid();
        var subEngFet    = Guid.NewGuid();
        var subPhysFet   = Guid.NewGuid();
        var subLifeFet   = Guid.NewGuid();
        var subAccFet    = Guid.NewGuid();
        var subMath9     = Guid.NewGuid();
        var subEng9      = Guid.NewGuid();
        var subNat9      = Guid.NewGuid();
        var class12Id    = Guid.NewGuid();
        var class9Id     = Guid.NewGuid();
        var cs12Math     = Guid.NewGuid(); var cs12Eng  = Guid.NewGuid();
        var cs12Phys     = Guid.NewGuid(); var cs12Life = Guid.NewGuid();
        var cs12Acc      = Guid.NewGuid();
        var cs9Math      = Guid.NewGuid(); var cs9Eng   = Guid.NewGuid();
        var cs9Nat       = Guid.NewGuid();
        var ayId         = Guid.NewGuid();
        var term1Id      = Guid.NewGuid(); var term2Id  = Guid.NewGuid();

        // ── School (all features ON) ─────────────────────────────────────────
        var school = new School
        {
            SchoolId = schoolId, Name = "Greendale High School",
            Domain = "greendale.edu", IsActive = true, CreatedAt = now,
            Features = new SchoolFeatures
            {
                Gradebook = true, VirtualClassroom = true, SmartReports = true,
                SaSamsExport = true, SkillsProfile = true, Pathways = true,
                MatricHub = true, SportsCulture = true, SchoolPay = true,
                SchoolChat = true, WhatsApp = true, PopiaCentre = true,
            },
            Settings = new SchoolSettings { Timezone = "Africa/Johannesburg", Locale = "en-ZA" },
        };
        _db.Schools.Add(school);

        // ── Users ───────────────────────────────────────────────────────────
        var users = new List<User>
        {
            new() { UserId=adminId,    SchoolId=schoolId, Role="Admin",   Email="admin@greendale.edu",            PasswordHash=hash("Admin@1234!"),   FirstName="Sarah",   LastName="Mthembu",   PhoneNumber="+27831234567", IsActive=true, CreatedAt=now },
            new() { UserId=teacher1Id, SchoolId=schoolId, Role="Teacher", Email="james.dlamini@greendale.edu",    PasswordHash=hash("Teacher@1234!"), FirstName="James",   LastName="Dlamini",   PhoneNumber="+27829876543", IsActive=true, CreatedAt=now },
            new() { UserId=teacher2Id, SchoolId=schoolId, Role="Teacher", Email="priya.naidoo@greendale.edu",     PasswordHash=hash("Teacher@1234!"), FirstName="Priya",   LastName="Naidoo",    PhoneNumber="+27821234321", IsActive=true, CreatedAt=now },
            new() { UserId=stu1UserId, SchoolId=schoolId, Role="Student", Email="lethabo.sithole@greendale.edu",  PasswordHash=hash("Student@1234!"), FirstName="Lethabo", LastName="Sithole",   PhoneNumber="+27839876543", IsActive=true, CreatedAt=now },
            new() { UserId=stu2UserId, SchoolId=schoolId, Role="Student", Email="amahle.dube@greendale.edu",      PasswordHash=hash("Student@1234!"), FirstName="Amahle",  LastName="Dube",      PhoneNumber="+27831112233", IsActive=true, CreatedAt=now },
            new() { UserId=stu3UserId, SchoolId=schoolId, Role="Student", Email="sipho.nkosi@greendale.edu",      PasswordHash=hash("Student@1234!"), FirstName="Sipho",   LastName="Nkosi",     PhoneNumber="+27833334455", IsActive=true, CreatedAt=now },
            new() { UserId=stu4UserId, SchoolId=schoolId, Role="Student", Email="zara.mokoena@greendale.edu",     PasswordHash=hash("Student@1234!"), FirstName="Zara",    LastName="Mokoena",   IsActive=true, CreatedAt=now },
            new() { UserId=stu5UserId, SchoolId=schoolId, Role="Student", Email="keanu.jansen@greendale.edu",     PasswordHash=hash("Student@1234!"), FirstName="Keanu",   LastName="Jansen",    IsActive=true, CreatedAt=now },
            new() { UserId=par1UserId, SchoolId=schoolId, Role="Parent",  Email="nomsa.sithole@parent.edu",       PasswordHash=hash("Parent@1234!"),  FirstName="Nomsa",   LastName="Sithole",   PhoneNumber="+27845556677", IsActive=true, CreatedAt=now },
            new() { UserId=par2UserId, SchoolId=schoolId, Role="Parent",  Email="bongani.mokoena@parent.edu",     PasswordHash=hash("Parent@1234!"),  FirstName="Bongani", LastName="Mokoena",   PhoneNumber="+27847778899", IsActive=true, CreatedAt=now },
        };
        _db.Users.AddRange(users);

        // ── Teachers ────────────────────────────────────────────────────────
        _db.Teachers.AddRange(
            new Teacher { TeacherId=t1Id, UserId=teacher1Id, SchoolId=schoolId, EmployeeNumber="EMP001", Specialization="Mathematics & Sciences", CreatedAt=now },
            new Teacher { TeacherId=t2Id, UserId=teacher2Id, SchoolId=schoolId, EmployeeNumber="EMP002", Specialization="English & Languages",     CreatedAt=now }
        );

        // ── Students ────────────────────────────────────────────────────────
        _db.Students.AddRange(
            new Student { StudentId=stu1Id, UserId=stu1UserId, SchoolId=schoolId, StudentNumber="STU2026-1001", GradeLevel=12, DateOfBirth=new DateTime(2008,3,15,0,0,0,DateTimeKind.Utc),  ParentUserId=par1UserId, CreatedAt=now },
            new Student { StudentId=stu2Id, UserId=stu2UserId, SchoolId=schoolId, StudentNumber="STU2026-1002", GradeLevel=12, DateOfBirth=new DateTime(2007,11,22,0,0,0,DateTimeKind.Utc), ParentUserId=par1UserId, CreatedAt=now },
            new Student { StudentId=stu3Id, UserId=stu3UserId, SchoolId=schoolId, StudentNumber="STU2026-1003", GradeLevel=12, DateOfBirth=new DateTime(2008,5,10,0,0,0,DateTimeKind.Utc),  ParentUserId=par2UserId, CreatedAt=now },
            new Student { StudentId=stu4Id, UserId=stu4UserId, SchoolId=schoolId, StudentNumber="STU2026-1004", GradeLevel=9,  DateOfBirth=new DateTime(2011,7,30,0,0,0,DateTimeKind.Utc),  ParentUserId=par2UserId, CreatedAt=now },
            new Student { StudentId=stu5Id, UserId=stu5UserId, SchoolId=schoolId, StudentNumber="STU2026-1005", GradeLevel=9,  DateOfBirth=new DateTime(2011,2,14,0,0,0,DateTimeKind.Utc),  CreatedAt=now }
        );

        // ── Subjects ────────────────────────────────────────────────────────
        _db.Subjects.AddRange(
            new Subject { SubjectId=subMathFet,  SchoolId=schoolId, Name="Mathematics",                  Code="MAT", CapsPhase="FET",         CreatedAt=now },
            new Subject { SubjectId=subEngFet,   SchoolId=schoolId, Name="English Home Language",        Code="EHL", CapsPhase="FET",         CreatedAt=now },
            new Subject { SubjectId=subPhysFet,  SchoolId=schoolId, Name="Physical Sciences",            Code="PHY", CapsPhase="FET",         CreatedAt=now },
            new Subject { SubjectId=subLifeFet,  SchoolId=schoolId, Name="Life Sciences",                Code="LSC", CapsPhase="FET",         CreatedAt=now },
            new Subject { SubjectId=subAccFet,   SchoolId=schoolId, Name="Accounting",                   Code="ACC", CapsPhase="FET",         CreatedAt=now },
            new Subject { SubjectId=subMath9,    SchoolId=schoolId, Name="Mathematics",                  Code="MAT9",CapsPhase="SeniorPhase", CreatedAt=now },
            new Subject { SubjectId=subEng9,     SchoolId=schoolId, Name="English First Additional",     Code="EFA", CapsPhase="SeniorPhase", CreatedAt=now },
            new Subject { SubjectId=subNat9,     SchoolId=schoolId, Name="Natural Sciences",             Code="NSC", CapsPhase="SeniorPhase", CreatedAt=now }
        );

        // ── Classes ─────────────────────────────────────────────────────────
        _db.Classes.AddRange(
            new Class { ClassId=class12Id, SchoolId=schoolId, Name="Grade 12A", GradeLevel=12, AcademicYear=2026, TeacherId=t1Id, MaxCapacity=35, CreatedAt=now },
            new Class { ClassId=class9Id,  SchoolId=schoolId, Name="Grade 9B",  GradeLevel=9,  AcademicYear=2026, TeacherId=t2Id, MaxCapacity=35, CreatedAt=now }
        );

        // ── ClassSubjects ────────────────────────────────────────────────────
        _db.ClassSubjects.AddRange(
            new ClassSubject { ClassSubjectId=cs12Math, ClassId=class12Id, SubjectId=subMathFet, TeacherId=t1Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs12Eng,  ClassId=class12Id, SubjectId=subEngFet,  TeacherId=t1Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs12Phys, ClassId=class12Id, SubjectId=subPhysFet, TeacherId=t1Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs12Life, ClassId=class12Id, SubjectId=subLifeFet, TeacherId=t1Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs12Acc,  ClassId=class12Id, SubjectId=subAccFet,  TeacherId=t1Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs9Math,  ClassId=class9Id,  SubjectId=subMath9,   TeacherId=t2Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs9Eng,   ClassId=class9Id,  SubjectId=subEng9,    TeacherId=t2Id, SchoolId=schoolId, CreatedAt=now },
            new ClassSubject { ClassSubjectId=cs9Nat,   ClassId=class9Id,  SubjectId=subNat9,    TeacherId=t2Id, SchoolId=schoolId, CreatedAt=now }
        );

        // ── Enrollments ──────────────────────────────────────────────────────
        _db.Enrollments.AddRange(
            new Enrollment { EnrollmentId=Guid.NewGuid(), ClassId=class12Id, StudentId=stu1Id, SchoolId=schoolId, EnrolledAt=now, IsActive=true },
            new Enrollment { EnrollmentId=Guid.NewGuid(), ClassId=class12Id, StudentId=stu2Id, SchoolId=schoolId, EnrolledAt=now, IsActive=true },
            new Enrollment { EnrollmentId=Guid.NewGuid(), ClassId=class12Id, StudentId=stu3Id, SchoolId=schoolId, EnrolledAt=now, IsActive=true },
            new Enrollment { EnrollmentId=Guid.NewGuid(), ClassId=class9Id,  StudentId=stu4Id, SchoolId=schoolId, EnrolledAt=now, IsActive=true },
            new Enrollment { EnrollmentId=Guid.NewGuid(), ClassId=class9Id,  StudentId=stu5Id, SchoolId=schoolId, EnrolledAt=now, IsActive=true }
        );

        // ── Academic Year + Terms ────────────────────────────────────────────
        var ay = new AcademicYear
        {
            AcademicYearId = ayId, SchoolId = schoolId, Year = 2026,
            StartDate = new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc),
            EndDate   = new DateTime(2026,11,30,0,0,0,DateTimeKind.Utc),
            CreatedAt = now,
        };
        _db.AcademicYears.Add(ay);
        _db.Terms.AddRange(
            new Term { TermId=term1Id, AcademicYearId=ayId, SchoolId=schoolId, TermNumber=1, StartDate=new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc), EndDate=new DateTime(2026,3,28,0,0,0,DateTimeKind.Utc), IsCurrent=true,  CreatedAt=now },
            new Term { TermId=term2Id, AcademicYearId=ayId, SchoolId=schoolId, TermNumber=2, StartDate=new DateTime(2026,4,7,0,0,0,DateTimeKind.Utc),  EndDate=new DateTime(2026,6,20,0,0,0,DateTimeKind.Utc), IsCurrent=false, CreatedAt=now }
        );

        // ── LearnerSubjects (Pathways) ───────────────────────────────────────
        // Lethabo + Amahle: all 5 FET subjects; Sipho: 4 (no Life Sciences)
        var fetSubjects = new[] { subMathFet, subEngFet, subPhysFet, subLifeFet, subAccFet };
        foreach (var sub in fetSubjects)
        {
            _db.LearnerSubjects.Add(new LearnerSubject { LearnerSubjectId=Guid.NewGuid(), StudentId=stu1Id, SubjectId=sub, AcademicYearId=ayId, SchoolId=schoolId, EnrolledAt=now });
            _db.LearnerSubjects.Add(new LearnerSubject { LearnerSubjectId=Guid.NewGuid(), StudentId=stu2Id, SubjectId=sub, AcademicYearId=ayId, SchoolId=schoolId, EnrolledAt=now });
        }
        foreach (var sub in new[] { subMathFet, subEngFet, subPhysFet, subAccFet })
            _db.LearnerSubjects.Add(new LearnerSubject { LearnerSubjectId=Guid.NewGuid(), StudentId=stu3Id, SubjectId=sub, AcademicYearId=ayId, SchoolId=schoolId, EnrolledAt=now });
        foreach (var sub in new[] { subMath9, subEng9, subNat9 })
        {
            _db.LearnerSubjects.Add(new LearnerSubject { LearnerSubjectId=Guid.NewGuid(), StudentId=stu4Id, SubjectId=sub, AcademicYearId=ayId, SchoolId=schoolId, EnrolledAt=now });
            _db.LearnerSubjects.Add(new LearnerSubject { LearnerSubjectId=Guid.NewGuid(), StudentId=stu5Id, SubjectId=sub, AcademicYearId=ayId, SchoolId=schoolId, EnrolledAt=now });
        }

        // ── Assignments + Submissions + Grades ───────────────────────────────
        // Helper: create assignment → submission → grade for a student
        var due = now.AddDays(-14);
        Assignment MakeAssignment(Guid csId, string title, decimal maxMarks) => new()
        {
            AssignmentId=Guid.NewGuid(), ClassSubjectId=csId, SchoolId=schoolId,
            Title=title, MaxMarks=maxMarks, DueAt=due, CreatedByUserId=teacher1Id, CreatedAt=now.AddDays(-21)
        };

        var assignments = new List<Assignment>
        {
            MakeAssignment(cs12Math, "Algebra Test",          100),
            MakeAssignment(cs12Math, "Geometry Task",          50),
            MakeAssignment(cs12Eng,  "Essay Writing",          80),
            MakeAssignment(cs12Eng,  "Literature Review",     100),
            MakeAssignment(cs12Phys, "Forces Lab Report",     100),
            MakeAssignment(cs12Phys, "Energy Quiz",            50),
            MakeAssignment(cs12Life, "Genetics Test",         100),
            MakeAssignment(cs12Acc,  "Balance Sheet",         100),
            MakeAssignment(cs9Math,  "Algebra Basics",        100),
            MakeAssignment(cs9Nat,   "Ecosystems Project",    100),
        };
        _db.Assignments.AddRange(assignments);
        await _db.SaveChangesAsync(); // flush so IDs exist for FKs

        // Grades per learner per assignment [score out of maxMarks]
        var gradeMap = new Dictionary<(int aIdx, Guid stuId), decimal>
        {
            // Lethabo (should be mostly Pass ≥ 40%)
            {(0,stu1Id),72}, {(1,stu1Id),42}, {(2,stu1Id),68}, {(3,stu1Id),85},
            {(4,stu1Id),65}, {(5,stu1Id),38}, {(6,stu1Id),78}, {(7,stu1Id),55},
            // Amahle (at-risk, mixed)
            {(0,stu2Id),35}, {(1,stu2Id),20}, {(2,stu2Id),52}, {(3,stu2Id),60},
            {(4,stu2Id),28}, {(5,stu2Id),15}, {(6,stu2Id),38}, {(7,stu2Id),42},
            // Sipho (mostly fail <30%)
            {(0,stu3Id),22}, {(1,stu3Id),10}, {(2,stu3Id),45}, {(3,stu3Id),50},
            {(4,stu3Id),18}, {(5,stu3Id),8},                   {(7,stu3Id),25},
            // Gr9 students
            {(8,stu4Id),88}, {(8,stu5Id),65},
            {(9,stu4Id),90}, {(9,stu5Id),70},
        };

        var subList = new List<Submission>();
        var gradeList = new List<Grade>();
        for (int i = 0; i < assignments.Count; i++)
        {
            var a = assignments[i];
            var stuIds = i < 8 ? new[] { stu1Id, stu2Id, stu3Id } : new[] { stu4Id, stu5Id };
            foreach (var sid in stuIds)
            {
                if (!gradeMap.TryGetValue((i, sid), out var score)) continue;
                var sub = new Submission
                {
                    SubmissionId=Guid.NewGuid(), AssignmentId=a.AssignmentId,
                    StudentId=sid, SchoolId=schoolId,
                    SubmittedAt=now.AddDays(-10), Comments="Submitted"
                };
                subList.Add(sub);
                gradeList.Add(new Grade
                {
                    GradeId=Guid.NewGuid(), SubmissionId=sub.SubmissionId,
                    SchoolId=schoolId, Score=score, Feedback="Well done",
                    GradedByUserId=teacher1Id, GradedAt=now.AddDays(-7)
                });
            }
        }
        _db.Submissions.AddRange(subList);
        _db.Grades.AddRange(gradeList);

        // ── Attendance (past 10 school days) ─────────────────────────────────
        var attendanceRecords = new List<Attendance>();
        var days = Enumerable.Range(1, 14)
            .Select(d => now.Date.AddDays(-d))
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            .Take(10).ToList();

        // Status: 1=Present, 0=Absent, 2=Late
        var stu12 = new[] { stu1Id, stu2Id, stu3Id };
        var stu9  = new[] { stu4Id, stu5Id };
        for (int di = 0; di < days.Count; di++)
        {
            var d = DateTime.SpecifyKind(days[di], DateTimeKind.Utc);
            foreach (var sid in stu12)
            {
                int status = 1;
                if (sid == stu3Id && di == 2) status = 0; // Sipho absent day 3
                if (sid == stu3Id && di == 5) status = 0; // Sipho absent day 6
                if (sid == stu3Id && di == 7) status = 2; // Sipho late day 8
                if (sid == stu1Id && di == 4) status = 0; // Lethabo absent day 5
                attendanceRecords.Add(new Attendance { AttendanceId=Guid.NewGuid(), ClassId=class12Id, StudentId=sid, SchoolId=schoolId, Date=d, Status=status, CreatedAt=d });
            }
            foreach (var sid in stu9)
            {
                int status = 1;
                if (sid == stu5Id && di == 1) status = 2; // Keanu late day 2
                attendanceRecords.Add(new Attendance { AttendanceId=Guid.NewGuid(), ClassId=class9Id, StudentId=sid, SchoolId=schoolId, Date=d, Status=status, CreatedAt=d });
            }
        }
        _db.Attendances.AddRange(attendanceRecords);

        // ── Fees + Payments ──────────────────────────────────────────────────
        var fee1Id = Guid.NewGuid(); var fee2Id = Guid.NewGuid();
        _db.Fees.AddRange(
            new Fee { FeeId=fee1Id, SchoolId=schoolId, TermId=term1Id, Name="School Fees Term 1 2026", Description="Annual school fee for Term 1", AmountZar=5000m, DueDate=new DateTime(2026,2,1,0,0,0,DateTimeKind.Utc), CreatedAt=now },
            new Fee { FeeId=fee2Id, SchoolId=schoolId, TermId=term1Id, Name="Activity Levy 2026",      Description="Sports, culture and activity levy",    AmountZar=500m,  DueDate=new DateTime(2026,3,1,0,0,0,DateTimeKind.Utc), CreatedAt=now }
        );
        _db.FeePayments.AddRange(
            new FeePayment { FeePaymentId=Guid.NewGuid(), FeeId=fee1Id, StudentId=stu1Id, SchoolId=schoolId, AmountPaidZar=5000m, PaidAt=now.AddDays(-20), RecordedByUserId=adminId, Notes="Paid in full",    CreatedAt=now.AddDays(-20) },
            new FeePayment { FeePaymentId=Guid.NewGuid(), FeeId=fee1Id, StudentId=stu2Id, SchoolId=schoolId, AmountPaidZar=2500m, PaidAt=now.AddDays(-15), RecordedByUserId=adminId, Notes="Partial payment", CreatedAt=now.AddDays(-15) },
            new FeePayment { FeePaymentId=Guid.NewGuid(), FeeId=fee1Id, StudentId=stu4Id, SchoolId=schoolId, AmountPaidZar=5000m, PaidAt=now.AddDays(-18), RecordedByUserId=adminId, Notes="Paid in full",    CreatedAt=now.AddDays(-18) },
            new FeePayment { FeePaymentId=Guid.NewGuid(), FeeId=fee2Id, StudentId=stu4Id, SchoolId=schoolId, AmountPaidZar=500m,  PaidAt=now.AddDays(-17), RecordedByUserId=adminId, Notes="Activity levy",  CreatedAt=now.AddDays(-17) },
            new FeePayment { FeePaymentId=Guid.NewGuid(), FeeId=fee1Id, StudentId=stu5Id, SchoolId=schoolId, AmountPaidZar=5000m, PaidAt=now.AddDays(-10), RecordedByUserId=adminId, Notes="Paid in full",    CreatedAt=now.AddDays(-10) }
        );

        // ── Skills ───────────────────────────────────────────────────────────
        _db.SkillEntries.AddRange(
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu1Id, SchoolId=schoolId, Title="Maths Olympiad 2025",     Category="Academic",        Description="Placed 3rd in provincial Maths Olympiad",     Date=new DateTime(2025,10,5,0,0,0,DateTimeKind.Utc),  EndorsedByUserId=teacher1Id, EndorsedAt=now.AddDays(-30), CreatedAt=now.AddDays(-40) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu1Id, SchoolId=schoolId, Title="Coding Club Leader",      Category="Technology",      Description="Led weekly coding sessions for Gr 10 learners", Date=new DateTime(2026,2,1,0,0,0,DateTimeKind.Utc),   CreatedAt=now.AddDays(-20) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu2Id, SchoolId=schoolId, Title="Choir Soloist",           Category="Arts & Culture",  Description="Lead soprano in school choir",                Date=new DateTime(2026,1,20,0,0,0,DateTimeKind.Utc),  EndorsedByUserId=teacher2Id, EndorsedAt=now.AddDays(-25), CreatedAt=now.AddDays(-35) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu2Id, SchoolId=schoolId, Title="Prefect",                 Category="Leadership",      Description="School Prefect 2026",                          Date=new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc),  CreatedAt=now.AddDays(-60) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu3Id, SchoolId=schoolId, Title="Football Captain",        Category="Sport",           Description="Captain of the school first team",             Date=new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc),  EndorsedByUserId=teacher1Id, EndorsedAt=now.AddDays(-10), CreatedAt=now.AddDays(-50) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu4Id, SchoolId=schoolId, Title="Science Fair Winner",     Category="Academic",        Description="First prize at district science fair",         Date=new DateTime(2025,9,15,0,0,0,DateTimeKind.Utc),  CreatedAt=now.AddDays(-45) },
            new SkillEntry { SkillEntryId=Guid.NewGuid(), StudentId=stu5Id, SchoolId=schoolId, Title="Community Garden Project", Category="Community",       Description="Organised community garden at local church",   Date=new DateTime(2026,2,10,0,0,0,DateTimeKind.Utc),  CreatedAt=now.AddDays(-22) }
        );

        // ── Activities ───────────────────────────────────────────────────────
        var act1Id = Guid.NewGuid(); var act2Id = Guid.NewGuid(); var act3Id = Guid.NewGuid();
        _db.Activities.AddRange(
            new Activity { ActivityId=act1Id, SchoolId=schoolId, Name="Inter-School Rugby Tournament", ActivityType="Sport",     Description="Provincial Under-18 Rugby Tournament in Pretoria", Date=new DateTime(2026,3,15,0,0,0,DateTimeKind.Utc), CreatedAt=now.AddDays(-30) },
            new Activity { ActivityId=act2Id, SchoolId=schoolId, Name="Drama Production: Hamlet",      ActivityType="Cultural",  Description="End-of-term school play production",              Date=new DateTime(2026,3,22,0,0,0,DateTimeKind.Utc), CreatedAt=now.AddDays(-28) },
            new Activity { ActivityId=act3Id, SchoolId=schoolId, Name="Community Clean-Up Drive",      ActivityType="Community", Description="Monthly community service initiative",             Date=new DateTime(2026,2,20,0,0,0,DateTimeKind.Utc), CreatedAt=now.AddDays(-60) }
        );
        _db.ActivityParticipants.AddRange(
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act1Id, StudentId=stu3Id, SchoolId=schoolId, Notes="First-team prop",       CreatedAt=now.AddDays(-25) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act1Id, StudentId=stu5Id, SchoolId=schoolId, Notes="Reserve flanker",        CreatedAt=now.AddDays(-25) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act2Id, StudentId=stu2Id, SchoolId=schoolId, Notes="Lead role (Ophelia)",    CreatedAt=now.AddDays(-20) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act2Id, StudentId=stu4Id, SchoolId=schoolId, Notes="Stage crew",             CreatedAt=now.AddDays(-20) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act3Id, StudentId=stu1Id, SchoolId=schoolId, Notes="Team leader",            CreatedAt=now.AddDays(-55) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act3Id, StudentId=stu2Id, SchoolId=schoolId, Notes="Participant",            CreatedAt=now.AddDays(-55) },
            new ActivityParticipant { ActivityParticipantId=Guid.NewGuid(), ActivityId=act3Id, StudentId=stu3Id, SchoolId=schoolId, Notes="Participant",            CreatedAt=now.AddDays(-55) }
        );

        // ── Announcements ────────────────────────────────────────────────────
        _db.Announcements.AddRange(
            new Announcement { AnnouncementId=Guid.NewGuid(), SchoolId=schoolId, Audience="All", IsActive=true, Title="Welcome to Term 1 2026",          Content="Welcome back learners and staff! Term 1 begins on 15 January. Please ensure all fees are paid by 1 February.",                                                                               CreatedByUserId=adminId,    CreatedAt=now.AddDays(-40) },
            new Announcement { AnnouncementId=Guid.NewGuid(), SchoolId=schoolId, Audience="All", IsActive=true, Title="Parent Information Evening",      Content="Parents are invited to the Term 1 information evening on 15 February at 18:00 in the school hall. Attendance is strongly encouraged.",                                                       CreatedByUserId=adminId,    CreatedAt=now.AddDays(-30) },
            new Announcement { AnnouncementId=Guid.NewGuid(), SchoolId=schoolId, Audience="All", IsActive=true, Title="Grade 12 Study Camp Reminder",    Content="The Grade 12 study camp will take place from 21-23 March. All Grade 12 learners must submit consent forms by 10 March. Contact Mr Dlamini for more information.",                           CreatedByUserId=teacher1Id, CreatedAt=now.AddDays(-20) },
            new Announcement { AnnouncementId=Guid.NewGuid(), SchoolId=schoolId, Audience="All", IsActive=true, Title="School Fees Reminder",            Content="A reminder that school fees for Term 1 are due by 1 February. Learners with outstanding fees should contact the school admin office. Bursary applications are available at reception.",     CreatedByUserId=adminId,    CreatedAt=now.AddDays(-10) }
        );

        // ── WhatsApp Logs ────────────────────────────────────────────────────
        _db.WhatsAppLogs.AddRange(
            new WhatsAppLog { WhatsAppLogId=Guid.NewGuid(), SchoolId=schoolId, RecipientName="Bongani Mokoena", RecipientPhone="+27847778899", TriggerType="Absence",    MessageBody="Hi Bongani, Sipho Nkosi was marked absent on 20 May 2026. Please contact the school if this is incorrect.", Status="Simulated", CreatedAt=now.AddDays(-6) },
            new WhatsAppLog { WhatsAppLogId=Guid.NewGuid(), SchoolId=schoolId, RecipientName="Nomsa Sithole",   RecipientPhone="+27845556677", TriggerType="FeeReminder", MessageBody="Hi Nomsa, a reminder that School Fees Term 1 2026 of R2500 is outstanding for Amahle Dube. Please contact admin.", Status="Simulated", CreatedAt=now.AddDays(-7) },
            new WhatsAppLog { WhatsAppLogId=Guid.NewGuid(), SchoolId=schoolId, RecipientName="Test Recipient",  RecipientPhone="+27800000001", TriggerType="Manual",     MessageBody="This is a test message from Greendale High School WhatsApp integration.", Status="Simulated", CreatedAt=now.AddDays(-8) }
        );

        // ── POPIA Consents ───────────────────────────────────────────────────
        _db.ConsentRecords.AddRange(
            new ConsentRecord { ConsentRecordId=Guid.NewGuid(), SchoolId=schoolId, UserId=stu1UserId, DataProcessing=true,  MarketingCommunications=true,  ThirdPartySharing=false, Photography=true,  UpdatedAt=now.AddDays(-30) },
            new ConsentRecord { ConsentRecordId=Guid.NewGuid(), SchoolId=schoolId, UserId=stu2UserId, DataProcessing=true,  MarketingCommunications=false, ThirdPartySharing=false, Photography=false, UpdatedAt=now.AddDays(-25) },
            new ConsentRecord { ConsentRecordId=Guid.NewGuid(), SchoolId=schoolId, UserId=par1UserId, DataProcessing=true,  MarketingCommunications=true,  ThirdPartySharing=false, Photography=true,  UpdatedAt=now.AddDays(-30) },
            new ConsentRecord { ConsentRecordId=Guid.NewGuid(), SchoolId=schoolId, UserId=par2UserId, DataProcessing=true,  MarketingCommunications=false, ThirdPartySharing=false, Photography=false, UpdatedAt=now.AddDays(-20) }
        );

        // ── Data Subject Requests ────────────────────────────────────────────
        _db.DataSubjectRequests.AddRange(
            new DataSubjectRequest { RequestId=Guid.NewGuid(), SchoolId=schoolId, UserId=stu2UserId, RequestType="Access",   Description="I would like to see all personal data the school holds about me.", Status="Pending",   CreatedAt=now.AddDays(-5) },
            new DataSubjectRequest { RequestId=Guid.NewGuid(), SchoolId=schoolId, UserId=stu3UserId, RequestType="Deletion", Description="Please delete my marketing preferences and opt-out data.",        Status="Completed", AdminNotes="Marketing data removed from CRM on 20 May 2026.", CreatedAt=now.AddDays(-15), ResolvedAt=now.AddDays(-10) }
        );

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Seed complete",
            school  = "Greendale High School",
            logins  = new[]
            {
                new { role="Admin",   email="admin@greendale.edu",           password="Admin@1234!",   note="" },
                new { role="Teacher", email="james.dlamini@greendale.edu",   password="Teacher@1234!", note="" },
                new { role="Teacher", email="priya.naidoo@greendale.edu",    password="Teacher@1234!", note="" },
                new { role="Student", email="lethabo.sithole@greendale.edu", password="Student@1234!", note="Grade 12 — mostly passing" },
                new { role="Student", email="amahle.dube@greendale.edu",     password="Student@1234!", note="Grade 12 — at risk" },
                new { role="Student", email="sipho.nkosi@greendale.edu",     password="Student@1234!", note="Grade 12 — failing" },
                new { role="Student", email="zara.mokoena@greendale.edu",    password="Student@1234!", note="Grade 9" },
                new { role="Student", email="keanu.jansen@greendale.edu",    password="Student@1234!", note="Grade 9" },
                new { role="Parent",  email="nomsa.sithole@parent.edu",      password="Parent@1234!",  note="Parent of Lethabo + Amahle" },
                new { role="Parent",  email="bongani.mokoena@parent.edu",    password="Parent@1234!",  note="Parent of Sipho + Zara" },
            }
        });
    }
}
