$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceRoot = Split-Path -Parent $projectRoot

$toolCandidates = @(
    (Join-Path $workspaceRoot "Tools"),
    (Join-Path $workspaceRoot "My project\Tools"),
    (Join-Path $projectRoot "Tools")
)

$toolRoot = $toolCandidates | Where-Object {
    Test-Path -LiteralPath (Join-Path $_ ".dotnet8\dotnet.exe")
} | Select-Object -First 1

if (-not $toolRoot) {
    throw "Tool folder not found. Expected .NET under one of: $($toolCandidates -join ', ')"
}

$dotnetRoot = Join-Path $toolRoot ".dotnet8"
$dotnetExe = Join-Path $dotnetRoot "dotnet.exe"

if (-not (Test-Path -LiteralPath $dotnetExe)) {
    throw ".NET 8 SDK not found: $dotnetRoot"
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

& $dotnetExe restore $projectRoot
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $dotnetExe build $projectRoot -c Debug
exit $LASTEXITCODE
