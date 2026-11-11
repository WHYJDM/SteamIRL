using Microsoft.AspNetCore.Antiforgery;

namespace SteamIRL.Api.Features.Accounts;

public sealed class CsrfFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return await next(context);
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "Обнови страницу и повтори действие.",
                extensions: new Dictionary<string, object?> { ["code"] = "csrf_invalid" });
        }
        return await next(context);
    }
}
