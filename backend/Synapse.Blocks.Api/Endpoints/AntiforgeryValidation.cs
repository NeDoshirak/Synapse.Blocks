using Microsoft.AspNetCore.Antiforgery;

namespace Synapse.Blocks.Api.Endpoints;

internal static class AntiforgeryValidation
{
    public static async Task<bool> IsValidAsync(IAntiforgery antiforgery, HttpContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
