![Ltmonster.AspNetCore.DynamicMiddleware](https://raw.githubusercontent.com/lt-monster/Ltmonster.AspNetCore.DynamicMiddleware/f164cc155c7379dfa95d79925d2ed89cb195d2a6/logo.svg)

## 🚩 Ltmonster.AspNetCore.DynamicMiddleware

[![Nuget downloads](https://img.shields.io/nuget/v/fluentresults.svg)](https://www.nuget.org/packages/Ltmonster.AspNetCore.DynamicMiddleware/)
[![Nuget](https://img.shields.io/nuget/dt/fluentresults)](https://www.nuget.org/packages/Ltmonster.AspNetCore.DynamicMiddleware/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/lt-monster/Ltmonster.AspNetCore.DynamicMiddleware/blob/main/LICENSE)

This is a plugin that supports the dynamic manipulation of middleware while the application is running.
The middleware is created the same way as the original, but registered differently.

## 🔍 Installation
First, install NuGet. Then, install Ltmonster.AspNetCore.DynamicMiddleware from the package manager console:
```
PM> Install-Package Ltmonster.AspNetCore.DynamicMiddleware
```
Or from the .NET CLI as:
```
dotnet add package Ltmonster.AspNetCore.DynamicMiddleware
```

## 🚀 Basic usage
First, write your middleware. As follows:
```csharp
public sealed class Plugin1(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine("start plugin1");
        await next(context);
        Console.WriteLine("end plugin1");
    }
}
```

Then, register in Program.cs
```csharp
builder.Services.AddDynamicMiddleware();

app.UseDynamicMiddleware()
    .InstallPlugin<Plugin1>("Plugin1", "This is the first plugin");
```

Finally, to control the plugin by DynamicMiddlewareContainer
```csharp
var container = app.ApplicationServices.GetRequiredService<DynamicMiddlewareContainer>();

container.EnablePlugin("Plugin1");
container.DisablePlugin("Plugin1");
```