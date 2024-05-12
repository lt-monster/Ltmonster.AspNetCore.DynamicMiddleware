using Microsoft.AspNetCore.Http;

namespace Ltmonster.AspNetCore.DynamicMiddleware;

/// <summary>
/// Dynamic middleware container
/// Control the middleware at run time.
/// </summary>
public sealed class DynamicMiddlewareContainer
{
    private readonly HashSet<DynamicMiddlewareConfig> plugins = [];
    private RequestDelegate? sourceDelegate = null;
    private RequestDelegate? pluginRequestDelegate;

    public int Count => plugins.Count;

    public void InitSourceDelegate(RequestDelegate next)
    {
        sourceDelegate = next;
        InitPluginRequestDelegate();
    }

    public void InitPluginRequestDelegate()
    {
        if (sourceDelegate is not null)
        {
            RequestDelegate tempDelegate = sourceDelegate;
            foreach (DynamicMiddlewareConfig plugin in plugins.Where(p => p.Enable).OrderDescending().ToArray())
            {
                tempDelegate = plugin.Plugin!(tempDelegate!);
            }
            pluginRequestDelegate = tempDelegate;
        }
    }

    public Task Run(HttpContext context)
    {
        return pluginRequestDelegate!(context);
    }

    public HashSet<DynamicMiddlewareConfig> GetPlugins() => plugins;

    /// <summary>
    /// Add plugin
    /// </summary>
    /// <param name="plugin"></param>
    public void AddPlugin(DynamicMiddlewareConfig plugin)
    {
        plugins.Add(plugin);
        InitPluginRequestDelegate();
    }

    /// <summary>
    /// Remove plugin
    /// </summary>
    /// <param name="pluginName"></param>
    public void RemovePlugin(string pluginName)
    {
        plugins.RemoveWhere(p => p.Name == pluginName);
        InitPluginRequestDelegate();
    }

    /// <summary>
    /// Enable plugin
    /// </summary>
    /// <param name="pluginName"></param>
    public void EnablePlugin(string pluginName)
    {
        DynamicMiddlewareConfig? plugin = plugins.FirstOrDefault(p => p.Name == pluginName);
        if (plugin != null)
        {
            plugin.Enable = true;
        }
    }

    /// <summary>
    /// Disable plugin
    /// </summary>
    /// <param name="pluginName"></param>
    public void DisablePlugin(string pluginName)
    {
        DynamicMiddlewareConfig? plugin = plugins.FirstOrDefault(p => p.Name == pluginName);
        if (plugin != null)
        {
            plugin.Enable = false;
        }
    }
}
