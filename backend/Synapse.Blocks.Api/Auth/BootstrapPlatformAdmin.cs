using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Synapse.Blocks.Api.Data;

namespace Synapse.Blocks.Api.Auth;

public sealed class BootstrapPlatformAdmin(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<BootstrapPlatformAdmin> logger) : IHostedService
{
    public const string PlatformAdminRole = "PlatformAdmin";
    public const string TeacherRole = "Teacher";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (environment.IsProduction() && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)))
            throw new InvalidOperationException("BootstrapAdmin__Email and BootstrapAdmin__Password are required in production.");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { PlatformAdminRole, TeacherRole })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = email };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
        if (!await users.IsInRoleAsync(admin, PlatformAdminRole)) await users.AddToRoleAsync(admin, PlatformAdminRole);
        logger.LogInformation("Platform administrator account is ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
