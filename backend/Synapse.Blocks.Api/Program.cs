using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Auth;
using Synapse.Blocks.Api.Cases;
using Synapse.Blocks.Api.Data;
using Synapse.Blocks.Api.Endpoints;
using Synapse.Blocks.Api.Levels;
using Synapse.Blocks.Api.Operations;
using Synapse.Blocks.Api.Reports;
using Synapse.Blocks.Api.Health;
using Synapse.Blocks.Api.Students;
using Synapse.Blocks.Services;

var builder = WebApplication.CreateBuilder(args);
var secureCookies = builder.Configuration.GetValue<bool?>("Cookie:Secure") ?? builder.Environment.IsProduction();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "synapse.teacher";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.LoginPath = "/api/auth/sign-in";
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "synapse.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddScoped<InvitationService>();
builder.Services.AddSingleton<LevelDefinitionValidator>();
builder.Services.AddScoped<LevelService>();
builder.Services.AddScoped<CaseService>();
builder.Services.AddScoped<ShareLinkService>();
builder.Services.AddScoped<AttemptService>();
builder.Services.AddScoped<ProgramEvaluationService>();
builder.Services.AddScoped<CaseArchiveService>();
builder.Services.AddScoped<CaseReportService>();
builder.Services.AddSingleton<BlockProgramRunner>();
builder.Services.AddHostedService<BootstrapPlatformAdmin>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "postgres")
    .AddCheck<DatabaseMigrationHealthCheck>("migrations");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name is "postgres" or "migrations"
});
app.MapAuthEndpoints();
app.MapPlatformAdminEndpoints();
app.MapTeacherLevelEndpoints();
app.MapTeacherCaseEndpoints();
app.MapStudentCaseEndpoints();
app.MapStudentAttemptEndpoints();
app.MapTeacherReportEndpoints();

app.Run();

public partial class Program;
