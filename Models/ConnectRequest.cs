namespace OpenHABTestSuiteBackend.Models;

/// <summary>Credentials sent to <c>POST /api/connect</c>.</summary>
public class ConnectRequest
{
    public string  Url      { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token    { get; set; }
}
