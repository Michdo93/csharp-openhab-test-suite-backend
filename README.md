# csharp-openhab-test-suite-backend

Stateless ASP.NET Core 8 backend for the
[csharp-openhab-test-suite](https://github.com/Michdo93/csharp-openhab-test-suite)
web frontend.

Every request carries credentials in the body — no session state is stored.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | Health check / wake-up |
| `POST` | `/api/connect` | Verify credentials → `{ loggedIn, isCloud }` |
| `POST` | `/api/test` | Run a tester method → `{ result, output }` |

### `POST /api/test`

```json
{
  "url":      "https://myopenhab.org",
  "username": "user@example.com",
  "password": "secret",
  "tester":   "ItemTester",
  "method":   "TestSwitch",
  "params":   ["MySwitch", "ON", "ON", 10]
}
```

Available testers: `ItemTester`, `ThingTester`, `RuleTester`,
`ChannelTester`, `PersistenceTester`, `SitemapTester`.

## NuGet Dependencies

- `CSharpOpenHABRestClient` 1.0.0 — NuGet
- `CSharpOpenHABTestSuite` 1.0.0 — NuGet

Both must be published to NuGet before building the Docker image.

## Local development

```bash
dotnet run
# → http://localhost:8080
```

## Docker

```bash
docker build -t csharp-openhab-test-suite-backend .
docker run -p 8080:8080 csharp-openhab-test-suite-backend
```

## Deploy on Render.com

1. Publish `CSharpOpenHABRestClient` and `CSharpOpenHABTestSuite` to NuGet.
2. Push this repository to GitHub.
3. **New → Web Service → Docker → Frankfurt → Free → PORT=8080 → Deploy**.

Live URL: `https://csharp-openhab-test-suite-backend.onrender.com`

## License

MIT License
