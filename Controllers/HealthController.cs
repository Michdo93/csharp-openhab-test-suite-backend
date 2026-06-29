using Microsoft.AspNetCore.Mvc;

namespace OpenHABTestSuiteBackend.Controllers;

/// <summary>Wake-up / health check used by the frontend.</summary>
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Health() =>
        Ok(new { status = "ok", service = "csharp-openhab-test-suite-backend" });
}
