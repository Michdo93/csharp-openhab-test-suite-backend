# csharp-openhab-test-suite-backend

HTTP backend for the [csharp-openhab-test-suite test app](https://github.com/Michdo93/csharp-openhab-test-suite-test-app):
a browser cannot run .NET, so this small ASP.NET Core server runs the testers of
[csharp-openhab-test-suite](https://github.com/Michdo93/csharp-openhab-test-suite) 2.0.1 on its behalf.
All tester methods are available (found by reflection).

| Method | Path | Body | Answer |
|---|---|---|---|
| `GET` | `/` | – | `{"status":"ok","service":"csharp-openhab-test-suite-backend","version":"2.0.1"}` |
| `POST` | `/api/connect` | `{url, token}` or `{url, username, password}` | `{loggedIn, isCloud, url}` |
| `POST` | `/api/test` | `{url, token, tester, method, params: [...]}` | `{result, output}` |

```json
{
  "url": "http://openhab:8080", "token": "oh.xxx",
  "tester": "ItemTester", "method": "TestDimmer",
  "params": ["Wohnzimmer_Dimmer", "30", ["30", "30.0"], 5]
}
```

```json
{"result": true, "output": "Success: 'Wohnzimmer_Dimmer' reached state '30'."}
```

`params` are positional; method names in C# (`TestDimmer`) or Python spelling (`testDimmer`).
The `Test…` methods take `[itemName, command, expectedState, timeout]`; `expectedState` may be
a list of accepted states (`ExpectedState`). `result` is `true`/`false`, for
`GetGroupMembers`, `GetLinksForItem` and `GetRuleStatus` JSON, for `GetThingStatus` a string.
`output` holds the messages of the testers (`TestLog.Handler`).

Testers: `ItemTester`, `ThingTester`, `RuleTester`, `ChannelTester`, `PersistenceTester`,
`SitemapTester`. The backend keeps no credentials; they come with every request. Tests run one
after another.

## Build and run

```bash
dotnet run -c Release                  # PORT=8080 by default
```

Requirements: .NET 10 SDK. Docker: `docker build -t csharp-openhab-test-suite-backend .` –
`render.yaml` deploys it on Render (Frankfurt, free plan):
`https://csharp-openhab-test-suite-backend.onrender.com`.

## License

MIT – Michael Christian Dörflinger
