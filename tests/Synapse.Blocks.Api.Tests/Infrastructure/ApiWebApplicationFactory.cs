using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Synapse.Blocks.Api.Tests.Infrastructure;

public sealed class ApiWebApplicationFactory(
    string connectionString,
    string bootstrapEmail = "",
    string bootstrapPassword = "") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("BootstrapAdmin:Email", bootstrapEmail);
        builder.UseSetting("BootstrapAdmin:Password", bootstrapPassword);
    }
}
