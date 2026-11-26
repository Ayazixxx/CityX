<#
.SYNOPSIS
    Times the city plan in the game's own runtime: Unity's Mono with the Boehm collector, as a built game runs it.

.DESCRIPTION
    tools/cityplan answers in .NET's CoreCLR, which runs the plan several times faster than the game does (2026-11-26: 1.4 s against
    11.6 s for the game's city), so its timings say little about the loading screen. This compiles the very same files (the ones
    tools/cityplan's project lists) with Unity's own compiler and runs them in Unity's own Mono, stage by stage (tools/cityplan/WorldTime.cs).
    Nothing here touches the Unity project, so the editor may stay open. docs/PERFORMANCE.md, 2026-11-26.

.PARAMETER Seed
    The world seed (default: the game's, CitySeed.Default).
#>
param([int]$Seed = 48398181)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $root 'CityX'
$versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:' } |
    Select-Object -First 1
$editorVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
$data = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Data"
$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
$mono = Join-Path $data 'MonoBleedingEdge\bin\mono-bdwgc.exe'
$api = Join-Path $data 'MonoBleedingEdge\lib\mono\4.7.1-api'

# The files tools/cityplan compiles, read from its project so the two never differ.
$toolDir = Join-Path $root 'tools\cityplan'
[xml]$project = Get-Content (Join-Path $toolDir 'cityplan.csproj')
$sources = @()
foreach ($item in $project.Project.ItemGroup.Compile) {
    $sources += Get-ChildItem (Join-Path $toolDir $item.Include) | ForEach-Object { $_.FullName }
}
$sources += Join-Path $toolDir 'WindowGridStub.cs'
$sources += Join-Path $toolDir 'WorldTime.cs'
$sources += Join-Path $PSScriptRoot 'cityplan_mono\Main.cs'

$out = Join-Path ([System.IO.Path]::GetTempPath()) 'cityx-plan-mono'
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out 'plan.exe'
$rsp = Join-Path $out 'build.rsp'
$lines = @('-nologo', '-optimize+', '-target:exe', '-langversion:9.0', '-nostdlib+', '-nowarn:CS0169,CS0649,CS0414,CS8632,CS0219',
    "-r:`"$api\mscorlib.dll`"", "-r:`"$api\System.dll`"", "-r:`"$api\System.Core.dll`"", "-out:`"$exe`"")
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding ascii
& $dotnet $csc "@$rsp" | Where-Object { $_ -notmatch 'warning' }
if ($LASTEXITCODE -ne 0) { exit 1 }
& $mono $exe $Seed
