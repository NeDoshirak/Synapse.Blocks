using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Synapse.Blocks.Api.Tests.Infrastructure;

public sealed class ApiWebApplicationFactory(
    string connectionString,
    string bootstrapEmail = "admin@example.test",
    string bootstrapPassword = "Admin-password-123!") : WebApplicationFactory<Program>
{
    public ConcurrentQueue<string> CapturedLogs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(CapturedLogs)));
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("BootstrapAdmin:Email", bootstrapEmail);
        builder.UseSetting("BootstrapAdmin:Password", bootstrapPassword);
    }
}
