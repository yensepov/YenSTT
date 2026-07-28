[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipRuntime
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $projectRoot "YenSTT.csproj"
$publish = Join-Path $projectRoot "publish"
$release = Join-Path $projectRoot "artifacts\release"
$worker = Join-Path $projectRoot "artifacts\faster-whisper-runtime\faster-whisper-worker\faster-whisper-worker.exe"

if (-not $SkipRuntime -or -not (Test-Path -LiteralPath $worker)) {
    & (Join-Path $PSScriptRoot "build-runtime.ps1")
}

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:Version=$Version `
    -p:FileVersion="$Version.0" -p:AssemblyVersion="$Version.0" -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

New-Item -ItemType Directory -Path $release -Force | Out-Null
$portable = Join-Path $release "YenSTT-v$Version-win-x64-portable.zip"
if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Force }

$portableFiles = Get-ChildItem -LiteralPath $publish -Force |
    Where-Object { $_.Extension -ne ".pdb" }
Compress-Archive -Path $portableFiles.FullName -DestinationPath $portable -CompressionLevel Optimal

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup with winget."
}

& $iscc "/DMyAppVersion=$Version" (Join-Path $projectRoot "installer\YenSTT.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

$assets = Get-ChildItem -LiteralPath $release -File |
    Where-Object { $_.Extension -in ".exe", ".zip" }
$checksums = foreach ($asset in $assets) {
    $hash = Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($asset.Name)"
}
$checksumsPath = Join-Path $release "SHA256SUMS.txt"
[System.IO.File]::WriteAllLines($checksumsPath, $checksums, [System.Text.UTF8Encoding]::new($false))

Write-Host "Release assets are ready in $release"
$assets | Select-Object Name, Length | Format-Table -AutoSize
