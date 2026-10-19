using Microsoft.AspNetCore.Identity;

namespace Synapse.Blocks.Api.Auth;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? TeacherDescription { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? StarterLevelsSeededAt { get; set; }
}
