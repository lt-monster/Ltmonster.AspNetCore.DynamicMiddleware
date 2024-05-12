using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ltmonster.AspNetCore.DynamicMiddleware;

/// <summary>
/// Dynamic middleware register
/// </summary>
public static class DynamicMiddlewareRegister
{
    public static IServiceCollection AddDynamicMiddleware(this IServiceCollection services)
    {
        services.AddSingleton<DynamicMiddlewareContainer>();
        services.AddSingleton<DynamicMiddlewareInstaller>();

        return services;
    }

    public static DynamicMiddlewareInstaller UseDynamicMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<DynamicMiddleware>();

        DynamicMiddlewareInstaller? installer = app.ApplicationServices.GetService<DynamicMiddlewareInstaller>()
            ?? throw new InvalidOperationException($"There is no {nameof(DynamicMiddlewareInstaller)} service in the container, please use AddDynamicMiddleware to register.");

        return installer;
    }
}
