$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceRoot = Split-Path -Parent $projectRoot

$toolCandidates = @(
    (Join-Path $workspaceRoot "Tools"),
    (Join-Path $workspaceRoot "My project\Tools"),
    (Join-Path $projectRoot "Tools")
)

$toolRoot = $toolCandidates | Where-Object {
    Test-Path -LiteralPath (Join-Path $_ "Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe")
} | Select-Object -First 1

if (-not $toolRoot) {
    throw "Tool folder not found. Expected Godot under one of: $($toolCandidates -join ', ')"
}

$godotRoot = Join-Path $toolRoot "Godot_v4.6.3-stable_mono_win64"
$dotnetRoot = Join-Path $toolRoot ".dotnet8"
$godotExe = Join-Path $godotRoot "Godot_v4.6.3-stable_mono_win64.exe"

if (-not (Test-Path -LiteralPath $godotExe)) {
    throw "Godot executable not found: $godotExe"
}

if (-not (Test-Path -LiteralPath (Join-Path $dotnetRoot "dotnet.exe"))) {
    throw ".NET 8 SDK not found: $dotnetRoot"
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

& $godotExe --path $projectRoot
