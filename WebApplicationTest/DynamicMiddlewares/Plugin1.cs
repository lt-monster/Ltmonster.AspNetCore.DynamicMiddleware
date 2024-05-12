namespace WebApplicationTest.DynamicMiddlewares;

public sealed class Plugin1(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine("start plugin1");
        await next(context);
        Console.WriteLine("end plugin1");
    }
}
