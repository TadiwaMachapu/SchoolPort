using FluentValidation;
using SchoolPortal.Server.Seeds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using SchoolPortal.Data;
using SchoolPortal.Server.Extensions;
using SchoolPortal.Server.Hubs;
using SchoolPortal.Server.Middleware;
using SchoolPortal.Server.Services;
using Serilog;
using System.Reflection;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/schoolportal-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog();

// Add DbContext — EnableDynamicJson() required for Npgsql to deserialize jsonb columns into POCOs
var npgsqlDataSource = new NpgsqlDataSourceBuilder(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    .EnableDynamicJson()
    .Build();

builder.Services.AddDbContext<SchoolPortalDbContext>(options =>
    options.UseNpgsql(npgsqlDataSource));

// Add Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };
        // SignalR sends the JWT in the query string for WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Deny by default (STEP 4): an endpoint carrying no authorization metadata at all is
    // closed, not open. Deliberate exposure is [AllowAnonymous] (+[AnonymousJustification]);
    // the controller-scan governance test enforces that every endpoint picks a path.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// STEP 4 permission enforcement: the policy provider turns [RequirePermission("key")] into a
// one-requirement policy on demand; the handler enforces it (JWT fast path vs DB authority
// path per PermissionResolver.RequiresDatabaseResolution). The handler is scoped because it
// depends on the scoped PermissionResolver / request DbContext.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, SchoolPortal.Server.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, SchoolPortal.Server.Authorization.PermissionAuthorizationHandler>();

// SSO — Google & Microsoft 365 (add to existing authentication)
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Sso:Google:ClientId"] ?? "CHANGE_ME";
        options.ClientSecret = builder.Configuration["Sso:Google:ClientSecret"] ?? "CHANGE_ME";
    })
    .AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Sso:Microsoft:ClientId"] ?? "CHANGE_ME";
        options.ClientSecret = builder.Configuration["Sso:Microsoft:ClientSecret"] ?? "CHANGE_ME";
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSPA", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true); // required for SignalR WebSocket
    });
});

// Add Controllers
builder.Services.AddControllers();

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add HttpClient for Supabase Storage and Google Gemini AI (free tier).
// (The former "anthropic" named client is gone — all AI features route through IGeminiService.)
builder.Services.AddHttpClient("supabase-storage");
builder.Services.AddHttpClient(SchoolPortal.Server.Services.GeminiService.HttpClientName);
builder.Services.AddScoped<SchoolPortal.Server.Services.IGeminiService, SchoolPortal.Server.Services.GeminiService>();

// Sprint 1.5.0 authorization — catalogue cache (immutable seed data, loaded at startup)
// and the permission resolver (stateless; JWT fast path + DB authority path).
builder.Services.AddSingleton<SchoolPortal.Server.Authorization.PermissionCatalogueCache>();
builder.Services.AddScoped<SchoolPortal.Server.Authorization.PermissionResolver>();
// Step 7 — Layer-3 scope enforcement (query filtering + IDOR checks).
builder.Services.AddScoped<SchoolPortal.Server.Authorization.IScopeService, SchoolPortal.Server.Authorization.ScopeService>();

// Add Services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IMarkCaptureService, MarkCaptureService>(); // Sprint 1.5.2.5
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IPathwaysService, PathwaysService>();
builder.Services.AddScoped<IAiGapAnalysisService, AiGapAnalysisService>();
builder.Services.AddScoped<IMatricHubService, MatricHubService>();
builder.Services.AddScoped<IMatricTutorService, MatricTutorService>();
builder.Services.AddScoped<IGr9AdvisorService, Gr9AdvisorService>();
builder.Services.AddScoped<ISmartReportsService, SmartReportsService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IStaffImportService, StaffImportService>();
builder.Services.AddScoped<IdentityBackfillService>();

// Add SignalR
builder.Services.AddSignalR();

// Add SuperAdmin Service
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();

// Add Notification Service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add ProblemDetails
builder.Services.AddProblemDetails();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "School Portal API",
        Version = "v1",
        Description = "School Portal Backend API with JWT Authentication"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Microsoft.OpenApi 2.x (Swashbuckle 10): scheme references are OpenApiSecuritySchemeReference,
    // not OpenApiSecurityScheme + OpenApiReference.
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>()
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgres",
        tags: new[] { "db", "postgres", "supabase" });

// Add Response Caching
builder.Services.AddResponseCaching();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "School Portal API V1");
    });
}

// Use Serilog Request Logging
app.UseSerilogRequestLogging();

// Use Exception Middleware
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowSPA");

// Use Response Caching
app.UseResponseCaching();

// Use Authentication
app.UseAuthentication();

// Use Tenant Middleware (after auth so JWT claims are populated)
app.UseMiddleware<TenantMiddleware>();

// Use Authorization
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Map SignalR Hub
app.MapHub<NotificationHub>("/hubs/notifications");

// Map Health Checks — must stay anonymous (load balancers / CI probe it without a token);
// otherwise the deny-by-default fallback policy would lock it behind authentication.
app.MapHealthChecks("/health").AllowAnonymous();

// HTTP QUERY smoke endpoint — Testing environment ONLY, never mapped in prod/dev.
// Proves the QUERY method flows through the full pipeline (routing → deny-by-default
// fallback policy → handler) ahead of the real MapQuery filter endpoints in Phase 1.5.
if (app.Environment.IsEnvironment("Testing"))
{
    app.MapQuery("/api/_smoke/query", () => Results.Ok(new { method = "QUERY", ok = true }));
}

// Read-only backfill modes (report, verify) must make NO database changes — skip migrate/seed.
var isReadonlyBackfillReport = args.Length > 0
    && args[0].Equals("backfill", StringComparison.OrdinalIgnoreCase)
    && (args.Length < 2
        || args[1].Equals("report", StringComparison.OrdinalIgnoreCase)
        || args[1].Equals("verify", StringComparison.OrdinalIgnoreCase));

// Apply migrations + seed on startup (and before `backfill apply`); never for the read-only report.
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<SchoolPortalDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (isReadonlyBackfillReport)
    {
        logger.LogInformation("Backfill report (read-only): skipping migrate/seed; no database changes will be made.");
    }
    else
    {
    // Step 10 test harness: the migration chain cannot replay from scratch (documented in
    // CLAUDE.md — InitialCreate/super_admins gaps), so WebApplicationFactory integration tests
    // build the schema from the current model. Production and dev migrate normally.
    if (app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await PathwaysSeedData.SeedAsync(db, logger);
    await MatricHubSeedData.SeedAsync(db, logger);
    await PositionsSeedData.SeedAsync(db, logger);

    // Load the immutable position→permission map into the in-process cache (Step 3):
    // request-time permission unions are pure memory — zero DB hits on the JWT path.
    var catalogue = scope.ServiceProvider
        .GetRequiredService<SchoolPortal.Server.Authorization.PermissionCatalogueCache>();
    await catalogue.LoadAsync(db);

    if (!await db.SuperAdmins.AnyAsync())
    {
        var seed = config.GetSection("SuperAdminSeed");
        var email     = seed["Email"]     ?? "admin@schoolportal.dev";
        var password  = seed["Password"]  ?? "Admin@1234!";
        var firstName = seed["FirstName"] ?? "Super";
        var lastName  = seed["LastName"]  ?? "Admin";

        db.SuperAdmins.Add(new SchoolPortal.Data.Entities.SuperAdmin
        {
            SuperAdminId = Guid.NewGuid(),
            Email        = email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName    = firstName,
            LastName     = lastName,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("SuperAdmin seeded: {Email}", email);
    }
    }
}

// Sprint 1.5.0 identity backfill — CLI entrypoint (no HTTP attack surface):
//   dotnet run --project SchoolPortal.Server -- backfill report   (DRY RUN → writes report file, no DB writes)
//   dotnet run --project SchoolPortal.Server -- backfill apply    (writes Identity + positions; run ONLY after report approved)
if (args.Length > 0 && args[0].Equals("backfill", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var backfill = scope.ServiceProvider.GetRequiredService<IdentityBackfillService>();
    var mode = args.Length > 1 ? args[1].ToLowerInvariant() : "report";

    if (mode == "apply")
    {
        var changes = await backfill.ApplyAsync();
        Log.Information("Identity backfill APPLY complete: {Changes} changes written.", changes);
    }
    else if (mode == "verify")
    {
        var v = await backfill.VerifyAsync();
        Log.Information("VERIFY: {Total} users, {RolePop} with Role populated, {IdSet} with Identity set.",
            v.TotalUsers, v.RolePopulated, v.IdentitySet);
        Log.Information("VERIFY identities: {Breakdown}", string.Join(", ", v.IdentityBreakdown.Select(kv => $"{kv.Key}={kv.Value}")));
        Log.Information("VERIFY positions: {Count} total — {Breakdown}", v.UserPositions, string.Join(", ", v.PositionBreakdown.Select(kv => $"{kv.Key}={kv.Value}")));
    }
    else
    {
        var plan = await backfill.BuildPlanAsync();
        var markdown = backfill.RenderMarkdown(plan);
        var reportPath = Path.Combine(app.Environment.ContentRootPath, "backfill-identity-report.md");
        await File.WriteAllTextAsync(reportPath, markdown);
        Log.Information("Identity backfill DRY RUN report written to {Path} ({Users} users, no DB writes).",
            reportPath, plan.Users.Count);
    }
    return; // do not start the web host
}

Log.Information("School Portal API starting up...");

app.Run();

// Make Program class accessible to tests
public partial class Program { }
