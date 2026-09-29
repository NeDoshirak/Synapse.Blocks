using Microsoft.AspNetCore.Antiforgery;

namespace Synapse.Blocks.Api.Endpoints;

public sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return await next(context);
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await AntiforgeryValidation.IsValidAsync(antiforgery, context.HttpContext)) return Results.BadRequest();
        return await next(context);
    }
}
