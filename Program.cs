using OpenHABTestSuiteBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSingleton<TesterDispatcher>();

// ── CORS – allow all origins (frontend on GitHub Pages) ───────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Port from Render.com env var ──────────────────────────────────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();
