using Microsoft.AspNetCore.Identity;

namespace Synapse.Blocks.Api.Auth;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
}
