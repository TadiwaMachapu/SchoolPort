using Microsoft.EntityFrameworkCore;
using SchoolPortal.Data.Entities;

namespace SchoolPortal.Data;

public class SchoolPortalDbContext : DbContext
{
    public SchoolPortalDbContext(DbContextOptions<SchoolPortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<School> Schools { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<ClassSubject> ClassSubjects { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<Submission> Submissions { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // School
        modelBuilder.Entity<School>(entity =>
        {
            entity.HasKey(e => e.SchoolId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Domain).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.Domain).IsUnique().HasFilter("[Domain] IS NOT NULL");
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.SchoolId, e.Email }).IsUnique();
            entity.HasOne(e => e.School).WithMany(s => s.Users).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Student
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.SchoolId, e.StudentNumber }).IsUnique();
            entity.HasOne(e => e.User).WithOne(u => u.Student).HasForeignKey<Student>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentUser).WithMany().HasForeignKey(e => e.ParentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Teacher
        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.TeacherId);
            entity.Property(e => e.EmployeeNumber).HasMaxLength(50);
            entity.Property(e => e.Specialization).HasMaxLength(200);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.User).WithOne(u => u.Teacher).HasForeignKey<Teacher>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Class
        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.School).WithMany(s => s.Classes).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher).WithMany(t => t.Classes).HasForeignKey(e => e.TeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // Subject
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.SchoolId, e.Code }).IsUnique().HasFilter("[Code] IS NOT NULL");
            entity.HasOne(e => e.School).WithMany(s => s.Subjects).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // ClassSubject
        modelBuilder.Entity<ClassSubject>(entity =>
        {
            entity.HasKey(e => e.ClassSubjectId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.ClassId, e.SubjectId }).IsUnique();
            entity.HasOne(e => e.Class).WithMany(c => c.ClassSubjects).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Subject).WithMany(s => s.ClassSubjects).HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher).WithMany(t => t.ClassSubjects).HasForeignKey(e => e.TeacherId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Enrollment
        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.ClassId, e.StudentId }).IsUnique();
            entity.HasOne(e => e.Class).WithMany(c => c.Enrollments).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Enrollments).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Assignment
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MaxMarks).HasPrecision(10, 2);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.ClassSubject).WithMany(cs => cs.Assignments).HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Submission
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId);
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.AssignmentId, e.StudentId }).IsUnique();
            entity.HasOne(e => e.Assignment).WithMany(a => a.Submissions).HasForeignKey(e => e.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Submissions).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Grade
        modelBuilder.Entity<Grade>(entity =>
        {
            entity.HasKey(e => e.GradeId);
            entity.Property(e => e.Score).HasPrecision(10, 2);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.SubmissionId).IsUnique();
            entity.HasOne(e => e.Submission).WithOne(s => s.Grade).HasForeignKey<Grade>(e => e.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GradedByUser).WithMany().HasForeignKey(e => e.GradedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Attendance
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.ClassId, e.StudentId, e.Date }).IsUnique();
            entity.HasOne(e => e.Class).WithMany(c => c.Attendances).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Attendances).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Announcement
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.AnnouncementId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Audience).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AudienceValue).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.School).WithMany(s => s.Announcements).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.SchoolId, e.UserId });
        });
    }
}
