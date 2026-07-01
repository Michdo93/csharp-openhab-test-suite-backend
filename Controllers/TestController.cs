using Microsoft.AspNetCore.Mvc;
using OpenHABRestClient;
using OpenHABTestSuiteBackend.Models;
using OpenHABTestSuiteBackend.Services;

namespace OpenHABTestSuiteBackend.Controllers;

/// <summary>Runs a single test-suite method and returns the result.</summary>
[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly TesterDispatcher _dispatcher;
    private readonly ILogger<TestController> _log;

    public TestController(TesterDispatcher dispatcher,
                          ILogger<TestController> log)
    {
        _dispatcher = dispatcher;
        _log        = log;
    }

    /// <summary>
    /// POST /api/test
    ///
    /// Request:
    /// <code>
    /// { url, username?, password?, token?,
    ///   tester: "ItemTester", method: "TestSwitch",
    ///   params: ["MySwitch","ON","ON",10] }
    /// </code>
    ///
    /// Response:
    /// <code>{ result: bool|any, output: string }</code>
    /// </summary>
    [HttpPost]
    public IActionResult RunTest([FromBody] TestRequest req)
    {
        // ── Validate ──────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(req.Tester))
            return BadRequest(new { error = "tester is required" });
        if (string.IsNullOrWhiteSpace(req.Method))
            return BadRequest(new { error = "method is required" });

        // ── Build client ──────────────────────────────────────────────────────
        OpenHABClient client;
        try
        {
            client = _dispatcher.BuildClient(req);
            if (!client.IsLoggedIn)
                return StatusCode(401,
                    new { error = "Could not connect to openHAB — check credentials" });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = e.Message });
        }
        catch (Exception e)
        {
            _log.LogWarning("Client creation failed: {Msg}", e.Message);
            return StatusCode(502, new { error = $"Connection failed: {e.Message}" });
        }

        // ── Instantiate tester ────────────────────────────────────────────────
        object tester;
        try { tester = _dispatcher.BuildTester(req.Tester, client); }
        catch (ArgumentException e)
        { return BadRequest(new { error = e.Message }); }

        // ── Execute ───────────────────────────────────────────────────────────
        try
        {
            var (result, output) = _dispatcher.Invoke(tester, req.Method, req.Params);
            _log.LogInformation("{Tester}.{Method}({Params}) → {Result}",
                req.Tester, req.Method, req.Params, result);
            return Ok(new { result = result ?? (object)false, output });
        }
        catch (MissingMethodException)
        {
            return BadRequest(
                new { error = $"Method '{req.Method}' (or '{char.ToUpperInvariant(req.Method[0])}{req.Method[1..]}') not found on {req.Tester}" });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { error = $"Wrong arguments: {e.Message}" });
        }
        catch (OpenHABException e)
        {
            _log.LogWarning("{Tester}.{Method}() openHAB error: {Msg}",
                req.Tester, req.Method, e.Message);
            return StatusCode(502, new { error = $"openHAB error: {e.Message}" });
        }
        catch (Exception e)
        {
            var inner = e.InnerException?.Message ?? e.Message;
            _log.LogError(e, "{Tester}.{Method}() failed", req.Tester, req.Method);
            return StatusCode(500, new { error = inner });
        }
    }
}