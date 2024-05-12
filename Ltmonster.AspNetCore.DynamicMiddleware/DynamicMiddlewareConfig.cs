using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http;

namespace Ltmonster.AspNetCore.DynamicMiddleware;

/// <summary>
/// Configuration information about the plug-in
/// </summary>
public sealed class DynamicMiddlewareConfig : IComparable<DynamicMiddlewareConfig>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    private int order;
    public int Order
    {
        get => order;
        set
        {
            order = value;

            //触发事件
            Container?.InitPluginRequestDelegate();
        }
    }
    private bool enable;
    public bool Enable
    {
        get => enable;
        set
        {
            if (enable != value)
            {
                enable = value;

                //触发事件
                Container?.InitPluginRequestDelegate();
            }
        }
    }
    [JsonIgnore]
    public Type? PluginType { get; set; }
    [JsonIgnore]
    public Func<RequestDelegate, RequestDelegate>? Plugin { get; set; }

    internal DynamicMiddlewareContainer? Container { get; set; }

    public int CompareTo(DynamicMiddlewareConfig? other) => Order - (other?.Order ?? Order);

    public override bool Equals(object? obj)
    {
        return obj is DynamicMiddlewareConfig config &&
               Name == config.Name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name);
    }

}
