# Development

## Build

```powershell
dotnet build src\Tinycast.Core\Tinycast.Core.csproj
dotnet build src\Tinycast\Tinycast.csproj -p:Platform=x64
```

The WinUI project is unpackaged and Windows App SDK self-contained, so `dotnet run` does not need
MSIX or Developer Mode:

```powershell
dotnet run --project src\Tinycast\Tinycast.csproj -p:Platform=x64
```

## Tests

```powershell
.\scripts\run-tests.ps1
```

The harness is a console program that compiles `Tinycast.Core` and asserts palette motions,
backup coverage, and that Core has no WinUI/P/Invoke.

## Settings location

| Channel | Root |
| --- | --- |
| Debug | `%APPDATA%\Tinycast\com.tinycast.windows.dev\` |
| Release | `%APPDATA%\Tinycast\com.tinycast.windows\` |

## Spec

`reference/` is a clone of https://github.com/abue-ammar/tinycast. Treat it as documentation.
Do not paste Swift into `src/`.
