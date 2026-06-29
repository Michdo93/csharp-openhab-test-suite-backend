using Microsoft.AspNetCore.Mvc;
using OpenHABTestSuiteBackend.Models;
using OpenHABTestSuiteBackend.Services;

namespace OpenHABTestSuiteBackend.Controllers;

/// <summary>Verifies openHAB credentials.</summary>
[ApiController]
[Route("api/connect")]
public class ConnectController : ControllerBase
{
    private readonly TesterDispatcher _dispatcher;
    private readonly ILogger<ConnectController> _log;

    public ConnectController(TesterDispatcher dispatcher,
                             ILogger<ConnectController> log)
    {
        _dispatcher = dispatcher;
        _log        = log;
    }

    /// <summary>
    /// POST /api/connect — try to connect and return login status.
    ///
    /// Response: <c>{ loggedIn: bool, isCloud: bool }</c>
    /// </summary>
    [HttpPost]
    public IActionResult Connect([FromBody] ConnectRequest req)
    {
        try
        {
            var client = _dispatcher.BuildClient(req);
            return Ok(new { loggedIn = client.IsLoggedIn, isCloud = client.IsCloud });
        }
        catch (Exception e)
        {
            _log.LogWarning("Connect failed: {Msg}", e.Message);
            return Ok(new { loggedIn = false, isCloud = false, error = e.Message });
        }
    }
}
