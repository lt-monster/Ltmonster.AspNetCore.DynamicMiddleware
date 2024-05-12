using Ltmonster.AspNetCore.DynamicMiddleware;

using WebApplicationTest.DynamicMiddlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddScoped<Plugin2>();
builder.Services.AddDynamicMiddleware();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseDynamicMiddleware()
    .InstallPlugin<Plugin1>("插件1", "这是第1个插件")
    .InstallPlugin<Plugin2>("插件2", "这是第2个插件")
    .InstallPlugin(async (context, next) =>
    {
        Console.WriteLine("start plugin3");
        await next(context);
        Console.WriteLine("end plugin3");
    }, "插件3", "第3个插件");

app.MapControllers();

app.Run();
