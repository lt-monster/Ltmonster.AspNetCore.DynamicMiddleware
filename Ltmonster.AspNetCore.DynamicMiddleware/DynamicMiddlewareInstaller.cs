using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Builder;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ltmonster.AspNetCore.DynamicMiddleware;

/// <summary>
/// Dynamic middleware installer
/// </summary>
public sealed class DynamicMiddlewareInstaller(DynamicMiddlewareContainer contriner, IServiceProvider serviceProvider)
{
    private const string InvokeMethodName = "Invoke";
    private const string InvokeAsyncMethodName = "InvokeAsync";
    private static readonly MethodInfo GetServiceInfo = typeof(UseMiddlewareExtensions).GetMethod(nameof(GetService), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Func<T, HttpContext, IServiceProvider, Task> CompileExpression<T>(MethodInfo methodInfo, ParameterInfo[] parameters)
    {
        var middleware = typeof(T);

        var httpContextArg = Expression.Parameter(typeof(HttpContext), "httpContext");
        var providerArg = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        var instanceArg = Expression.Parameter(middleware, "middleware");

        var methodArguments = new Expression[parameters.Length];
        methodArguments[0] = httpContextArg;
        for (var i = 1; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException("ref or out parameters are not supported.");
            }

            var parameterTypeExpression = new Expression[]
            {
                providerArg,
                Expression.Constant(parameterType, typeof(Type)),
                Expression.Constant(methodInfo.DeclaringType, typeof(Type))
            };

            var getServiceCall = Expression.Call(GetServiceInfo, parameterTypeExpression);
            methodArguments[i] = Expression.Convert(getServiceCall, parameterType);
        }

        Expression middlewareInstanceArg = instanceArg;
        if (methodInfo.DeclaringType != null && methodInfo.DeclaringType != typeof(T))
        {
            middlewareInstanceArg = Expression.Convert(middlewareInstanceArg, methodInfo.DeclaringType);
        }

        var body = Expression.Call(middlewareInstanceArg, methodInfo, methodArguments);

        var lambda = Expression.Lambda<Func<T, HttpContext, IServiceProvider, Task>>(body, instanceArg, httpContextArg, providerArg);

        return lambda.Compile();
    }

    private static Func<T, HttpContext, IServiceProvider, Task> ReflectionFallback<T>(MethodInfo methodInfo, ParameterInfo[] parameters)
    {
        Debug.Assert(!RuntimeFeature.IsDynamicCodeSupported, "Use reflection fallback when dynamic code is not supported.");

        for (var i = 1; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            if (parameterType.IsByRef)
            {
                throw new NotSupportedException("ref or out parameters are not supported.");
            }
        }

        return (middleware, context, serviceProvider) =>
        {
            var methodArguments = new object[parameters.Length];
            methodArguments[0] = context;
            for (var i = 1; i < parameters.Length; i++)
            {
                methodArguments[i] = GetService(serviceProvider, parameters[i].ParameterType, methodInfo.DeclaringType!);
            }

            return (Task)methodInfo.Invoke(middleware, BindingFlags.DoNotWrapExceptions, binder: null, methodArguments, culture: null)!;
        };
    }

    private static object GetService(IServiceProvider sp, Type type, Type middleware)
    {
        var service = sp.GetService(type);
        return service ?? throw new InvalidOperationException($"The {type.Name} service does not exist in the container.");
    }

    private sealed class InterfaceMiddlewareBinder
    {
        private readonly Type _middlewareType;

        public InterfaceMiddlewareBinder(Type middlewareType)
        {
            _middlewareType = middlewareType;
        }

        // The CreateMiddleware method name is used by ApplicationBuilder to resolve the middleware type.
        public RequestDelegate CreateMiddleware(RequestDelegate next)
        {
            return async context =>
            {
                var middlewareFactory = (IMiddlewareFactory?)context.RequestServices.GetService(typeof(IMiddlewareFactory));
                if (middlewareFactory == null)
                {
                    // No middleware factory
                    throw new InvalidOperationException("No middleware factory");
                }

                var middleware = middlewareFactory.Create(_middlewareType);
                if (middleware == null)
                {
                    // The factory returned null, it's a broken implementation
                    throw new InvalidOperationException("The factory returned null, it's a broken implementation");
                }

                try
                {
                    await middleware.InvokeAsync(context, next);
                }
                finally
                {
                    middlewareFactory.Release(middleware);
                }
            };
        }

        public override string ToString() => _middlewareType.ToString();
    }

    /// <summary>
    /// Installing middleware plugin
    /// </summary>
    /// <typeparam name="DynamicMiddlewareType">Type of middleware</typeparam>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="pluginDescription">Plugin description</param>
    /// <param name="args">Parameters when the middleware is instantiated (cannot be included when using strongly typed middleware that is, the way the middleware factory is created)</param>
    /// <returns></returns>
    public DynamicMiddlewareInstaller InstallPlugin<DynamicMiddlewareType>(string? pluginName = "", string? pluginDescription = "", params object?[] args)
    {
        return InstallPlugin(typeof(DynamicMiddlewareType), pluginName, pluginDescription, args);
    }

    /// <summary>
    /// Installing middleware plugin
    /// </summary>
    /// <param name="middlewarePluginType">Type of middleware</param>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="pluginDescription">Plugin description</param>
    /// <param name="args">Parameters when the middleware is instantiated (cannot be included when using strongly typed middleware that is, the way the middleware factory is created)</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public DynamicMiddlewareInstaller InstallPlugin(Type middlewarePluginType, string? pluginName = "", string? pluginDescription = "", params object?[] args)
    {
        var pluginConfig = new DynamicMiddlewareConfig
        {
            Name = string.IsNullOrWhiteSpace(pluginName) ? middlewarePluginType.Name : pluginName,
            Description = pluginDescription,
            Order = contriner.Count + 1,
            Enable = true,
            PluginType = middlewarePluginType,
            Container = contriner
        };

        if (typeof(IMiddleware).IsAssignableFrom(middlewarePluginType))
        {
            // IMiddleware doesn't support passing args directly since it's
            // activated from the container
            if (args.Length > 0)
            {
                throw new NotSupportedException("The middleware that implements the IMiddleware interface does not support parameters.");
            }

            var interfaceBinder = new InterfaceMiddlewareBinder(middlewarePluginType);
            pluginConfig.Plugin = interfaceBinder.CreateMiddleware;

            contriner.AddPlugin(pluginConfig);
            return this;
        }

        var methods = middlewarePluginType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        MethodInfo? invokeMethod = null;
        foreach (var method in methods)
        {
            if (string.Equals(method.Name, InvokeMethodName, StringComparison.Ordinal) || string.Equals(method.Name, InvokeAsyncMethodName, StringComparison.Ordinal))
            {
                if (invokeMethod is not null)
                {
                    throw new InvalidOperationException($"{middlewarePluginType.Name} cannot contain multiple {InvokeMethodName} or {InvokeAsyncMethodName} method.");
                }

                invokeMethod = method;
            }
        }
        if (invokeMethod is null)
        {
            throw new InvalidOperationException($"{middlewarePluginType.Name} does not exist {InvokeMethodName} or {InvokeAsyncMethodName} method.");
        }

        if (!typeof(Task).IsAssignableFrom(invokeMethod.ReturnType))
        {
            throw new InvalidOperationException($"{middlewarePluginType.Name} {InvokeMethodName} or {InvokeAsyncMethodName} method return value is not a Task or Task derived type.");
        }

        var parameters = invokeMethod.GetParameters();
        if (parameters.Length == 0 || parameters[0].ParameterType != typeof(HttpContext))
        {
            throw new InvalidOperationException($"{middlewarePluginType.Name} {InvokeMethodName} or {InvokeAsyncMethodName} the first argument must be a {nameof(HttpContext)}.");
        }

        pluginConfig.Plugin = rd =>
        {
            var ctorArgs = new object[args.Length + 1];
            ctorArgs[0] = rd;
            Array.Copy(args, 0, ctorArgs, 1, args.Length);
            var instance = ActivatorUtilities.CreateInstance(serviceProvider, middlewarePluginType, ctorArgs);
            if (parameters.Length == 1)
            {
                return (RequestDelegate)invokeMethod.CreateDelegate(typeof(RequestDelegate), instance);
            }

            var factory = RuntimeFeature.IsDynamicCodeCompiled
                ? CompileExpression<object>(invokeMethod, parameters)
                : ReflectionFallback<object>(invokeMethod, parameters);

            return context => factory(instance, context, serviceProvider);
        };

        contriner.AddPlugin(pluginConfig);
        return this;
    }

    /// <summary>
    /// Installing middleware plugin
    /// </summary>
    /// <param name="middleware">middleware</param>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="pluginDescription">Plugin description</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public DynamicMiddlewareInstaller InstallPlugin(Func<HttpContext, RequestDelegate, Task> middleware, string pluginName = "", string? pluginDescription = "")
    {
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("插件名称不能为空！");
        }
        var pluginConfig = new DynamicMiddlewareConfig
        {
            Name = pluginName,
            Description = pluginDescription,
            Order = contriner.Count + 1,
            Enable = true,
            PluginType = null,
            Plugin = next => context => middleware(context, next),
            Container = contriner
        };

        contriner.AddPlugin(pluginConfig);
        return this;
    }

    /// <summary>
    /// Installing middleware plugin
    /// </summary>
    /// <param name="middleware">middleware</param>
    /// <param name="pluginName">Plugin name</param>
    /// <param name="pluginDescription">Plugin description</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public DynamicMiddlewareInstaller InstallPlugin(Func<RequestDelegate, RequestDelegate> middleware, string pluginName = "", string? pluginDescription = "")
    {
        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("The plug-in name cannot be empty.");
        }
        var pluginConfig = new DynamicMiddlewareConfig
        {
            Name = pluginName,
            Description = pluginDescription,
            Order = contriner.Count + 1,
            Enable = true,
            PluginType = null,
            Plugin = middleware,
            Container = contriner
        };

        contriner.AddPlugin(pluginConfig);
        return this;
    }
}