using FluentValidation;
using SchoolPortal.Server.Seeds;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using SchoolPortal.Data;
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

// Add DbContext — EnableDynamicJson() required for Npgsql 8 to deserialize jsonb columns into POCOs
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

builder.Services.AddAuthorization();

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

// Add HttpClient for Supabase Storage and Anthropic AI
builder.Services.AddHttpClient("supabase-storage");
builder.Services.AddHttpClient("anthropic");

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

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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

// Map Health Checks
app.MapHealthChecks("/health");

// Seed SuperAdmin on first startup if none exists
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<SchoolPortalDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    await PathwaysSeedData.SeedAsync(db, logger);
    await MatricHubSeedData.SeedAsync(db, logger);

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

Log.Information("School Portal API starting up...");

app.Run();

// Make Program class accessible to tests
public partial class Program { }
