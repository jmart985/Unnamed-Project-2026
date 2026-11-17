param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
dotnet build "$root/src/Simulation/Simulation.csproj" -c $Configuration -f netstandard2.1
if ($LASTEXITCODE -ne 0) { throw 'Portable simulation build failed.' }
$destination = "$root/unity/Assets/Plugins/Simulation"
New-Item -ItemType Directory -Force $destination, "$root/unity/Assets/StreamingAssets/Simulation" | Out-Null
Get-ChildItem "$root/src/Simulation/bin/$Configuration/netstandard2.1" -Filter '*.dll' |
    Copy-Item -Destination $destination -Force
Get-ChildItem "$root/data" -Filter '*.json' |
    Copy-Item -Destination "$root/unity/Assets/StreamingAssets/Simulation" -Force
Write-Host 'Unity domain DLLs and catalog data synchronized.'
