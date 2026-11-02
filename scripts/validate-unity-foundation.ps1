param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
& "$PSScriptRoot/sync-unity-domain.ps1"
dotnet test "$root/tests/Simulation.Tests/Simulation.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Original framework tests failed.' }
dotnet test "$root/tests/Simulation.UnityCompatibility.Tests/Simulation.UnityCompatibility.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Portable assembly regression tests failed.' }
New-Item -ItemType Directory -Force "$root/work" | Out-Null
function Invoke-CheckedProcess([string]$Executable, [string[]]$ProcessArguments, [string]$Log, [string]$Marker) {
    $process = Start-Process -FilePath $Executable -ArgumentList $ProcessArguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(300000)) {
        Stop-Process -Id $process.Id
        throw "Validation timed out. See $Log"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $Log -SimpleMatch $Marker -Quiet)) {
        throw "Validation failed (exit $($process.ExitCode)). See $Log"
    }
}
$editorLog = "$root/work/unity-validate.log"
Invoke-CheckedProcess $UnityEditor @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$root/unity`"",
    '-executeMethod', 'SimulationFoundationSetup.ValidateAndBuild', '-logFile', "`"$editorLog`"") $editorLog 'UNITY SIMULATION FOUNDATION BUILD PASSED'
$playerLog = "$root/work/unity-player-smoke.log"
Invoke-CheckedProcess "$root/unity/Builds/Windows/SimulationFoundation.exe" @('-batchmode', '-nographics',
    '--simulation-smoke', '-logFile', "`"$playerLog`"") $playerLog 'UNITY SIMULATION PLAYER SMOKE PASSED'
Write-Host 'Unity foundation validation passed: both domain test runs, Editor smoke, Windows build and player smoke.'
