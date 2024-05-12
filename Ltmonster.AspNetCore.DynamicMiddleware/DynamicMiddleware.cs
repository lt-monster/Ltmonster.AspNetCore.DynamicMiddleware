using Microsoft.AspNetCore.Http;

namespace Ltmonster.AspNetCore.DynamicMiddleware;

/// <summary>
/// Dynamic middleware
/// </summary>
public sealed class DynamicMiddleware
{
    private readonly DynamicMiddlewareContainer _contriner;
    public DynamicMiddleware(RequestDelegate next, DynamicMiddlewareContainer contriner)
    {
        _contriner = contriner;
        _contriner.InitSourceDelegate(next);
    }

    public async Task InvokeAsync(HttpContext context) => await _contriner.Run(context);
}
