namespace WebApplicationTest.DynamicMiddlewares;

public class Plugin2 : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        Console.WriteLine("start plugin2");
        await next(context);
        Console.WriteLine("end plugin2");
    }
}
