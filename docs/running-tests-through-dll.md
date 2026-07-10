# Running MaxioApiTests through the built Release DLL

`MaxioApiTests` can be run either the normal way (`dotnet test` from source) or by
building a standalone Release DLL once and invoking it directly with `dotnet vstest`. The DLL
route is useful when you want to run the suite without a full `dotnet build`/restore each time,
or hand the built output to something that only runs pre-built assemblies (CI artifact, etc).

## Run the suite against the DLL

Assumes `MaxioApiTests/build/` already has a Release build (`.\build.ps1`) and that
the mock and target PublicApi (Direct or Plugin) are already running per root `CLAUDE.md`'s
end-to-end recipe.

From `MaxioApiTests/build/`, using `dotnet vstest` (not `dotnet test` — there's no
project/solution here, just the built assembly):

**PowerShell:**
```powershell
cd D:\work\eshop-integration\eshopOnWeb\MaxioApiTests\build
$env:PUBLICAPI_BASEURL = "http://localhost:5199"
dotnet vstest MaxioApiTests.dll --logger:"junit;LogFilePath=../../docs/test-logs.xml"
```

**Bash:**
```bash
cd "D:/work/eshop-integration/eshopOnWeb/MaxioApiTests/build"
PUBLICAPI_BASEURL=http://localhost:5199 dotnet vstest MaxioApiTests.dll \
  --logger:"junit;LogFilePath=../../docs/test-logs.xml"
```

Notes:
- `LogFilePath` is resolved relative to the working directory you run the command from, so the
  path above (`../../docs/test-logs.xml`) only works when run from `MaxioApiTests/build`.
  Use an absolute path if running from elsewhere.
- Set `RECORD_USAGE_PATH_TEMPLATE=/api/maxio/subscriptions/{subscriptionId}/usages` as an extra
  env var when targeting **Direct** (its usage route shape differs from Plugin's default).
- Both `junit` and `trx` loggers are bundled in `build/` — swap the logger name/arg to switch
  formats, e.g. `--logger:"trx;LogFileName=maxio-results.trx"`.
- `--ListTests` enumerates all tests without running them; `--TestCaseFilter:"FullyQualifiedName~PauseSubscriptionTests"`
  runs a subset.
