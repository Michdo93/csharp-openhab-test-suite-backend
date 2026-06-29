namespace OpenHABTestSuiteBackend.Models;

/// <summary>
/// Full request body for <c>POST /api/test</c>.
/// Credentials + tester dispatch parameters.
/// </summary>
public class TestRequest
{
    // ── Connection credentials ─────────────────────────────────────────────
    public string  Url      { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token    { get; set; }

    // ── Test parameters ────────────────────────────────────────────────────
    /// <summary>e.g. <c>"ItemTester"</c></summary>
    public string Tester { get; set; } = "";

    /// <summary>e.g. <c>"TestSwitch"</c></summary>
    public string Method { get; set; } = "";

    /// <summary>Ordered list of method arguments as raw JSON values.</summary>
    public List<object?> Params { get; set; } = [];
}
