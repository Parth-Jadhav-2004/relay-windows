# Development

## Build

```powershell
dotnet build src\Relay.Core\Relay.Core.csproj
dotnet build src\Relay\Relay.csproj -p:Platform=x64
```

The WinUI project is unpackaged and Windows App SDK self-contained, so `dotnet run` does not need
MSIX or Developer Mode:

```powershell
dotnet run --project src\Relay\Relay.csproj -p:Platform=x64
```

## Tests

```powershell
.\scripts\run-tests.ps1
```

The harness is a console program that compiles `Relay.Core` and asserts palette motions,
backup coverage, and that Core has no WinUI/P/Invoke.

## Settings location

| Channel | Root |
| --- | --- |
| Debug | `%APPDATA%\Relay\com.relay.windows.dev\` |
| Release | `%APPDATA%\Relay\com.relay.windows\` |

GitHub Releases for this private repo need `RELAY_GITHUB_TOKEN` in that folder’s `.env`
(see `.env.example`). Settings → About only checks for updates; it does not store the token.

## Spec

`reference/` is a clone of https://github.com/abue-ammar/relay. Treat it as documentation.
Do not paste Swift into `src/`.
