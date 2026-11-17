// Backend of the csharp-openhab-test-suite test app: runs the testers of
// csharp-openhab-test-suite 2.0.0 on behalf of the browser.
//
//   GET  /              health check (wake-up on Render)
//   POST /api/connect   {url, token | username+password}
//   POST /api/test      {url, token, ..., tester, method, params: [...]}  ->  {result, output}
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenHABRestClient;
using OpenHABTestSuite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var app = builder.Build();
app.UseCors();

var json = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
IResult Json(JsonNode? v, int status = 200) =>
    Results.Text(v?.ToJsonString(json) ?? "null", "application/json", Encoding.UTF8, status);
IResult Error(string m, int status) => Json(new JsonObject { ["error"] = m }, status);

var testers = new[] { typeof(ItemTester), typeof(ThingTester), typeof(RuleTester), typeof(ChannelTester),
                      typeof(PersistenceTester), typeof(SitemapTester) }.ToDictionary(t => t.Name);
var gate = new SemaphoreSlim(1);   // tests change items: one after another

OpenHABClient Client(JsonObject b)
{
    string S(string k) => b.Str(k);
    return new OpenHABClient(S("url").Length == 0 ? "https://myopenhab.org" : S("url"),
        S("username").Length == 0 ? null : S("username"), S("password").Length == 0 ? null : S("password"),
        S("token").Length == 0 ? null : S("token"));
}

app.MapGet("/", () => Json(new JsonObject
{
    ["status"] = "ok", ["service"] = "csharp-openhab-test-suite-backend", ["version"] = "2.0.0",
}));

app.MapPost("/api/connect", async (HttpRequest req) =>
{
    if (await Body(req) is not JsonObject b) return Error("Invalid JSON body", 400);
    using var c = Client(b);
    var j = new JsonObject { ["loggedIn"] = c.IsLoggedIn, ["isCloud"] = c.IsCloud, ["url"] = c.Url };
    if (c.LastError.Length > 0) j["error"] = c.LastError;
    return Json(j);
});

app.MapPost("/api/test", async (HttpRequest req) =>
{
    if (await Body(req) is not JsonObject b) return Error("Invalid JSON body", 400);
    string testerName = b.Str("tester"), method = b.Str("method");
    if (!testers.TryGetValue(testerName, out var type)) return Error($"Unknown tester {testerName}", 400);
    var name = method.Length > 0 ? char.ToUpperInvariant(method[0]) + method[1..] : method;
    var mi = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
    if (mi == null) return Error($"Unknown method {testerName}.{method}", 400);
    var given = b["params"] as JsonArray ?? new JsonArray();
    var ps = mi.GetParameters();
    var vals = new object?[ps.Length];
    try
    {
        for (var i = 0; i < ps.Length; i++)
            vals[i] = i < given.Count ? Convert(given[i], ps[i]) :
                      ps[i].IsOptional ? ps[i].DefaultValue : throw new ArgumentException($"{ps[i].Name} is required");
    }
    catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
    {
        return Error("Invalid argument: " + e.Message, 400);
    }

    await gate.WaitAsync();
    var output = new StringBuilder();
    try
    {
        using var c = Client(b);
        TestLog.Handler = (level, text) => { lock (output) output.AppendLine(text); };
        var target = mi.IsStatic ? null : Activator.CreateInstance(type, c);
        var result = await Task.Run(() => mi.Invoke(target, vals));
        return Json(new JsonObject
        {
            ["result"] = result switch
            {
                null => null,
                JsonNode n => n.DeepClone(),
                bool x => x,
                string s => s,
                _ => JsonSerializer.SerializeToNode(result),
            },
            ["output"] = output.ToString().TrimEnd(),
        });
    }
    catch (TargetInvocationException e) { return Error(e.InnerException?.Message ?? e.Message, 500); }
    finally
    {
        TestLog.Handler = null;
        gate.Release();
    }
});

app.Run();

static object? Convert(JsonNode? v, ParameterInfo p)
{
    var t = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
    if (v == null) return null;
    var kind = v.GetValueKind();
    if (t == typeof(string)) return kind == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString();
    if (t == typeof(int)) return kind == JsonValueKind.String ? int.Parse(v.GetValue<string>()) : v.GetValue<int>();
    if (t == typeof(double)) return kind == JsonValueKind.String
        ? double.Parse(v.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture) : v.GetValue<double>();
    if (t == typeof(bool)) return kind == JsonValueKind.String ? bool.Parse(v.GetValue<string>()) : v.GetValue<bool>();
    if (t == typeof(ExpectedState))
        return v is JsonArray a ? new ExpectedState(a.Select(x => x?.ToString() ?? "")) :
               kind == JsonValueKind.String ? new ExpectedState(v.GetValue<string>()) : new ExpectedState(v.ToJsonString());
    return v.DeepClone();   // object? (contextData)
}

static async Task<JsonNode?> Body(HttpRequest req)
{
    try { return await JsonNode.ParseAsync(req.Body); }
    catch (JsonException) { return null; }
}
