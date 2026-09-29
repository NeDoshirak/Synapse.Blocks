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
        await BaselineLegacyDevelopmentSchemaAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
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

    private static async Task BaselineLegacyDevelopmentSchemaAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT to_regclass('public.\"AspNetUsers\"') IS NOT NULL AND to_regclass('public.\"__EFMigrationsHistory\"') IS NULL";
            if (await check.ExecuteScalarAsync(cancellationToken) is not true) return;

            await using var baseline = connection.CreateCommand();
            baseline.CommandText = """
                ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "StarterLevelsSeededAt" timestamp with time zone NULL;
                ALTER TABLE level_versions ADD COLUMN IF NOT EXISTS "Title" character varying(200) NOT NULL DEFAULT '';
                CREATE TABLE IF NOT EXISTS teacher_levels (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "OwnerId" text NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT,
                    "Title" character varying(200) NOT NULL,
                    "CurrentVersionId" uuid NULL REFERENCES level_versions("Id") ON DELETE RESTRICT,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_teacher_levels_OwnerId" ON teacher_levels ("OwnerId");
                CREATE INDEX IF NOT EXISTS "IX_teacher_levels_CurrentVersionId" ON teacher_levels ("CurrentVersionId");
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_level_versions_teacher_levels_LevelId') THEN
                        ALTER TABLE level_versions ADD CONSTRAINT "FK_level_versions_teacher_levels_LevelId"
                            FOREIGN KEY ("LevelId") REFERENCES teacher_levels("Id") ON DELETE RESTRICT;
                    END IF;
                END $$;
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                    "ProductVersion" character varying(32) NOT NULL
                );
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20260929202020_InitialPlatform', '10.0.9');
                """;
            await baseline.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
