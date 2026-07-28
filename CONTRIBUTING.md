# Contributing to YenSTT

Contributions are welcome through GitHub issues and pull requests.

## Development setup

Requirements:

- Windows 10 or 11 x64
- .NET 8 SDK
- Python 3.13 with the Python launcher (`py`)
- Inno Setup 6 for installer builds

```powershell
py -3 -m pip install -r Python\requirements-build.txt
dotnet restore YenSTT.csproj
dotnet build YenSTT.csproj -c Release
```

Run `scripts\build-runtime.ps1` to build the bundled faster-whisper worker. Run `scripts\build-release.ps1` to produce the installer, portable ZIP, and checksums.

## Pull requests

1. Create a focused branch from `main`.
2. Keep changes scoped and explain user-visible behavior.
3. Build in Release mode and test the affected workflow.
4. Do not commit model files, logs, recordings, credentials, `bin`, `obj`, `publish`, or `artifacts`.
5. Update README or CHANGELOG when behavior changes.

By contributing, you agree that your contribution is licensed under the MIT License used by this repository.
