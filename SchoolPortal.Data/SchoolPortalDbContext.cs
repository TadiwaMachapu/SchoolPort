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

    // Course / LMS
    public DbSet<Course> Courses { get; set; }
    public DbSet<CourseModule> CourseModules { get; set; }
    public DbSet<Lesson> Lessons { get; set; }

    // Gradebook categories
    public DbSet<GradeCategory> GradeCategories { get; set; }

    // Calendar & Timetable
    public DbSet<CalendarEvent> CalendarEvents { get; set; }
    public DbSet<TimetableSlot> TimetableSlots { get; set; }

    // Learning Paths & Progress
    public DbSet<LessonProgress> LessonProgress { get; set; }
    public DbSet<LearningPath> LearningPaths { get; set; }
    public DbSet<LearningPathCourse> LearningPathCourses { get; set; }

    // Billing
    public DbSet<Subscription> Subscriptions { get; set; }

    // Plugin System
    public DbSet<Plugin> Plugins { get; set; }
    public DbSet<PluginInstallation> PluginInstallations { get; set; }

    // Messaging
    public DbSet<MessageThread> MessageThreads { get; set; }
    public DbSet<ThreadParticipant> ThreadParticipants { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    // Quiz Engine
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }
    public DbSet<QuizOption> QuizOptions { get; set; }
    public DbSet<QuizAttempt> QuizAttempts { get; set; }
    public DbSet<QuizAnswer> QuizAnswers { get; set; }

    // View entities
    public DbSet<AttendanceSummaryView> AttendanceSummaryView { get; set; }
    public DbSet<GradebookSimpleView> GradebookSimpleView { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply snake_case naming convention for all entities
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = ToSnakeCase(entity.GetTableName() ?? entity.ClrType.Name);
            entity.SetTableName(tableName);

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? $"pk_{tableName}"));
            }

            foreach (var fk in entity.GetForeignKeys())
            {
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName() ?? $"fk_{tableName}"));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? $"ix_{tableName}"));
            }
        }

        // School
        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("schools");
            entity.HasKey(e => e.SchoolId);
            entity.Property(e => e.SchoolId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Domain).HasMaxLength(100);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.Domain).IsUnique().HasFilter("domain IS NOT NULL");
            entity.Property(e => e.Features)
                  .HasColumnType("jsonb")
                  .HasColumnName("features")
                  .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.Theme)
                  .HasColumnType("jsonb")
                  .HasColumnName("theme")
                  .HasDefaultValueSql("'{}'::jsonb");
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => new { e.SchoolId, e.Email }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.School).WithMany(s => s.Users).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Student
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.SchoolId, e.StudentNumber }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ParentUserId);
            entity.HasOne(e => e.User).WithOne(u => u.Student).HasForeignKey<Student>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentUser).WithMany().HasForeignKey(e => e.ParentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Teacher
        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("teachers");
            entity.HasKey(e => e.TeacherId);
            entity.Property(e => e.TeacherId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.EmployeeNumber).HasMaxLength(50);
            entity.Property(e => e.Specialization).HasMaxLength(200);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User).WithOne(u => u.Teacher).HasForeignKey<Teacher>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Class
        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes");
            entity.HasKey(e => e.ClassId);
            entity.Property(e => e.ClassId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.TeacherId);
            entity.HasOne(e => e.School).WithMany(s => s.Classes).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher).WithMany(t => t.Classes).HasForeignKey(e => e.TeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        // Subject
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("subjects");
            entity.HasKey(e => e.SubjectId);
            entity.Property(e => e.SubjectId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.SchoolId, e.Code }).IsUnique().HasFilter("code IS NOT NULL");
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.School).WithMany(s => s.Subjects).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // ClassSubject
        modelBuilder.Entity<ClassSubject>(entity =>
        {
            entity.ToTable("class_subjects");
            entity.HasKey(e => e.ClassSubjectId);
            entity.Property(e => e.ClassSubjectId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.ClassId, e.SubjectId }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.ClassId);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.TeacherId);
            entity.HasOne(e => e.Class).WithMany(c => c.ClassSubjects).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Subject).WithMany(s => s.ClassSubjects).HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher).WithMany(t => t.ClassSubjects).HasForeignKey(e => e.TeacherId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Enrollment
        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments");
            entity.HasKey(e => e.EnrollmentId);
            entity.Property(e => e.EnrollmentId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.ClassId, e.StudentId }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.ClassId);
            entity.HasIndex(e => e.StudentId);
            entity.HasOne(e => e.Class).WithMany(c => c.Enrollments).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Enrollments).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Assignment
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.HasKey(e => e.AssignmentId);
            entity.Property(e => e.AssignmentId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MaxMarks).HasPrecision(10, 2);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.ClassSubjectId);
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasOne(e => e.ClassSubject).WithMany(cs => cs.Assignments).HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Submission
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.ToTable("submissions");
            entity.HasKey(e => e.SubmissionId);
            entity.Property(e => e.SubmissionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.FileUrl).HasMaxLength(500);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.AssignmentId, e.StudentId }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.AssignmentId);
            entity.HasIndex(e => e.StudentId);
            entity.HasOne(e => e.Assignment).WithMany(a => a.Submissions).HasForeignKey(e => e.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Submissions).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Grade
        modelBuilder.Entity<Grade>(entity =>
        {
            entity.ToTable("grades");
            entity.HasKey(e => e.GradeId);
            entity.Property(e => e.GradeId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Score).HasPrecision(10, 2);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SubmissionId).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.GradedByUserId);
            entity.HasOne(e => e.Submission).WithOne(s => s.Grade).HasForeignKey<Grade>(e => e.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GradedByUser).WithMany().HasForeignKey(e => e.GradedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Attendance
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("attendances");
            entity.HasKey(e => e.AttendanceId);
            entity.Property(e => e.AttendanceId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => new { e.SchoolId, e.ClassId, e.StudentId, e.Date }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.ClassId);
            entity.HasIndex(e => e.StudentId);
            entity.HasOne(e => e.Class).WithMany(c => c.Attendances).HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany(s => s.Attendances).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Announcement
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("announcements");
            entity.HasKey(e => e.AnnouncementId);
            entity.Property(e => e.AnnouncementId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Audience).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AudienceValue).HasMaxLength(100);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasOne(e => e.School).WithMany(s => s.Announcements).HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.AuditLogId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.SchoolId, e.UserId });
        });

        // Course
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses_lms");
            entity.HasKey(e => e.CourseId);
            entity.Property(e => e.CourseId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.ClassSubjectId);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ClassSubject).WithMany().HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // CourseModule
        modelBuilder.Entity<CourseModule>(entity =>
        {
            entity.ToTable("course_modules");
            entity.HasKey(e => e.ModuleId);
            entity.Property(e => e.ModuleId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.CourseId);
            entity.HasOne(e => e.Course).WithMany(c => c.Modules).HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Cascade);
        });

        // Lesson
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.ToTable("lessons");
            entity.HasKey(e => e.LessonId);
            entity.Property(e => e.LessonId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.ModuleId);
            entity.HasOne(e => e.Module).WithMany(m => m.Lessons).HasForeignKey(e => e.ModuleId).OnDelete(DeleteBehavior.Cascade);
        });

        // GradeCategory
        modelBuilder.Entity<GradeCategory>(entity =>
        {
            entity.ToTable("grade_categories");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Weight).HasPrecision(5, 2);
            entity.HasOne(e => e.ClassSubject).WithMany().HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // CalendarEvent
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.ToTable("calendar_events");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.SchoolId);
            entity.HasIndex(e => e.StartAt);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Class).WithMany().HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        // TimetableSlot
        modelBuilder.Entity<TimetableSlot>(entity =>
        {
            entity.ToTable("timetable_slots");
            entity.HasKey(e => e.SlotId);
            entity.Property(e => e.SlotId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Room).HasMaxLength(100);
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.ClassSubject).WithMany().HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // Plugin
        modelBuilder.Entity<Plugin>(entity =>
        {
            entity.ToTable("plugins");
            entity.HasKey(e => e.PluginId);
            entity.Property(e => e.PluginId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DeveloperName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DeveloperEmail).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<PluginInstallation>(entity =>
        {
            entity.ToTable("plugin_installations");
            entity.HasKey(e => e.InstallationId);
            entity.Property(e => e.InstallationId).HasDefaultValueSql("gen_random_uuid()");
            entity.HasIndex(e => new { e.PluginId, e.SchoolId }).IsUnique();
            entity.HasOne(e => e.Plugin).WithMany(p => p.Installations).HasForeignKey(e => e.PluginId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // Subscription
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(e => e.SubscriptionId);
            entity.Property(e => e.SubscriptionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Plan).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StripeCustomerId).HasMaxLength(100);
            entity.Property(e => e.StripeSubscriptionId).HasMaxLength(100);
            entity.HasIndex(e => e.SchoolId).IsUnique();
            entity.HasIndex(e => e.StripeSubscriptionId);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // LessonProgress
        modelBuilder.Entity<LessonProgress>(entity =>
        {
            entity.ToTable("lesson_progress");
            entity.HasKey(e => e.ProgressId);
            entity.Property(e => e.ProgressId).HasDefaultValueSql("gen_random_uuid()");
            entity.HasIndex(e => new { e.LessonId, e.StudentId }).IsUnique();
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.Lesson).WithMany().HasForeignKey(e => e.LessonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        // LearningPath
        modelBuilder.Entity<LearningPath>(entity =>
        {
            entity.ToTable("learning_paths");
            entity.HasKey(e => e.PathId);
            entity.Property(e => e.PathId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        // LearningPathCourse
        modelBuilder.Entity<LearningPathCourse>(entity =>
        {
            entity.ToTable("learning_path_courses");
            entity.HasKey(e => e.PathCourseId);
            entity.Property(e => e.PathCourseId).HasDefaultValueSql("gen_random_uuid()");
            entity.HasOne(e => e.Path).WithMany(p => p.Courses).HasForeignKey(e => e.PathId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        // Messaging
        modelBuilder.Entity<MessageThread>(entity =>
        {
            entity.ToTable("message_threads");
            entity.HasKey(e => e.ThreadId);
            entity.Property(e => e.ThreadId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Class).WithMany().HasForeignKey(e => e.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ThreadParticipant>(entity =>
        {
            entity.ToTable("thread_participants");
            entity.HasKey(e => e.ParticipantId);
            entity.Property(e => e.ParticipantId).HasDefaultValueSql("gen_random_uuid()");
            entity.HasIndex(e => new { e.ThreadId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Thread).WithMany(t => t.Participants).HasForeignKey(e => e.ThreadId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => e.ThreadId);
            entity.HasOne(e => e.Thread).WithMany(t => t.Messages).HasForeignKey(e => e.ThreadId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Sender).WithMany().HasForeignKey(e => e.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Quiz
        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.ToTable("quizzes");
            entity.HasKey(e => e.QuizId);
            entity.Property(e => e.QuizId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
            entity.HasIndex(e => e.SchoolId);
            entity.HasOne(e => e.School).WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ClassSubject).WithMany().HasForeignKey(e => e.ClassSubjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuizQuestion>(entity =>
        {
            entity.ToTable("quiz_questions");
            entity.HasKey(e => e.QuestionId);
            entity.Property(e => e.QuestionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Marks).HasPrecision(10, 2);
            entity.HasIndex(e => e.QuizId);
            entity.HasOne(e => e.Quiz).WithMany(q => q.Questions).HasForeignKey(e => e.QuizId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizOption>(entity =>
        {
            entity.ToTable("quiz_options");
            entity.HasKey(e => e.OptionId);
            entity.Property(e => e.OptionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Text).IsRequired();
            entity.HasOne(e => e.Question).WithMany(q => q.Options).HasForeignKey(e => e.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuizAttempt>(entity =>
        {
            entity.ToTable("quiz_attempts");
            entity.HasKey(e => e.AttemptId);
            entity.Property(e => e.AttemptId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Score).HasPrecision(10, 2);
            entity.Property(e => e.MaxScore).HasPrecision(10, 2);
            entity.HasIndex(e => new { e.QuizId, e.StudentId });
            entity.HasOne(e => e.Quiz).WithMany(q => q.Attempts).HasForeignKey(e => e.QuizId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QuizAnswer>(entity =>
        {
            entity.ToTable("quiz_answers");
            entity.HasKey(e => e.AnswerId);
            entity.Property(e => e.AnswerId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.MarksAwarded).HasPrecision(10, 2);
            entity.HasOne(e => e.Attempt).WithMany(a => a.Answers).HasForeignKey(e => e.AttemptId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Question).WithMany(q => q.Answers).HasForeignKey(e => e.QuestionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SelectedOption).WithMany().HasForeignKey(e => e.SelectedOptionId).OnDelete(DeleteBehavior.Restrict);
        });

        // Configure views (read-only)
        modelBuilder.Entity<AttendanceSummaryView>(entity =>
        {
            entity.ToView("vw_attendance_summary");
            entity.HasNoKey();
        });

        modelBuilder.Entity<GradebookSimpleView>(entity =>
        {
            entity.ToView("vw_gradebook_simple");
            entity.HasNoKey();
        });
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(name[i - 1]))
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
