using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Synapse.Blocks.Api.Auth;

public sealed class ActiveMfaRequirement : IAuthorizationRequirement;

public sealed class ActiveMfaHandler(UserManager<ApplicationUser> users)
    : AuthorizationHandler<ActiveMfaRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveMfaRequirement requirement)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is { IsActive: true, TwoFactorEnabled: true })
            context.Succeed(requirement);
    }
}
