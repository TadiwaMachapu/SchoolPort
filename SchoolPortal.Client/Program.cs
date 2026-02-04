using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SchoolPortal.Client;
using SchoolPortal.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7071") 
});

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Add Authentication
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// Check if we should use mock services (automatic in Development)
var useMockApi = builder.Configuration.GetValue<bool>("UseMockApi");

if (useMockApi)
{
    // Register mock services for UI development without backend
    builder.Services.AddScoped<IAuthService, MockAuthService>();
    builder.Services.AddScoped<IAssignmentService, AssignmentService>(); // TODO: Create MockAssignmentService
    builder.Services.AddScoped<IClassService, MockClassService>();
    builder.Services.AddScoped<ISubjectService, MockSubjectService>();
    builder.Services.AddScoped<ISubmissionService, MockSubmissionService>();
    builder.Services.AddScoped<IGradeService, MockGradeService>();
    builder.Services.AddScoped<IAttendanceService, MockAttendanceService>();
    builder.Services.AddScoped<IAnnouncementService, MockAnnouncementService>();
    builder.Services.AddScoped<IUserService, MockUserService>();
}
else
{
    // Register real HTTP-based services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IAssignmentService, AssignmentService>();
    builder.Services.AddScoped<IClassService, ClassService>();
    builder.Services.AddScoped<ISubjectService, SubjectService>();
    builder.Services.AddScoped<ISubmissionService, SubmissionService>();
    builder.Services.AddScoped<IGradeService, GradeService>();
    builder.Services.AddScoped<IAttendanceService, AttendanceService>();
    builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
    builder.Services.AddScoped<IUserService, UserService>();
}

await builder.Build().RunAsync();
