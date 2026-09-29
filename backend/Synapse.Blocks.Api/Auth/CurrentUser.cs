using System.Security.Claims;

namespace Synapse.Blocks.Api.Auth;

public interface ICurrentUser
{
    string UserId { get; }
}

public sealed class CurrentUser(IHttpContextAccessor contextAccessor) : ICurrentUser
{
    public string UserId => contextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("An authenticated user is required.");
}
