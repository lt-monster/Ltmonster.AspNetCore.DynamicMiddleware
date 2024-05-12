using Ltmonster.AspNetCore.DynamicMiddleware;

using Microsoft.AspNetCore.Mvc;

namespace WebApplicationTest.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class TestController(DynamicMiddlewareContainer container) : ControllerBase
{
    [HttpGet]
    public IActionResult Test()
    {
        return Ok(true);
    }

    [HttpGet]
    public IActionResult GetPlugins()
    {
        return Ok(container.GetPlugins());
    }

    [HttpGet]
    public IActionResult EnablePlugin(string pluginName)
    {
        container.EnablePlugin(pluginName);
        return Ok(true);
    }

    [HttpGet]
    public IActionResult DisablePlugin(string pluginName)
    {
        container.DisablePlugin(pluginName);
        return Ok(true);
    }
}