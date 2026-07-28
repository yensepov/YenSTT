[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$requirements = Join-Path $projectRoot "Python\requirements-build.txt"
$spec = Join-Path $projectRoot "packaging\faster-whisper-worker.spec"
$dist = Join-Path $projectRoot "artifacts\faster-whisper-runtime"
$work = Join-Path $projectRoot "artifacts\pyinstaller-build"

$pythonArguments = @()
if ($env:YENSTT_PYTHON) {
    $python = $env:YENSTT_PYTHON
} elseif (Get-Command py -ErrorAction SilentlyContinue) {
    $python = "py"
    $pythonArguments = @("-3")
} else {
    $python = "python"
}

& $python @pythonArguments -m pip install --upgrade -r $requirements
if ($LASTEXITCODE -ne 0) { throw "Failed to install Python build dependencies." }

& $python @pythonArguments -m PyInstaller --noconfirm --clean --distpath $dist --workpath $work $spec
if ($LASTEXITCODE -ne 0) { throw "Failed to build faster-whisper runtime." }

$worker = Join-Path $dist "faster-whisper-worker\faster-whisper-worker.exe"
if (-not (Test-Path -LiteralPath $worker)) { throw "Worker executable was not produced." }

$ping = '{"command":"ping"}' | & $worker
if ($LASTEXITCODE -ne 0 -or $ping -notmatch '"ok":true') {
    throw "Bundled faster-whisper worker failed its health check: $ping"
}

Write-Host "Bundled faster-whisper runtime is ready: $worker"
