<#
.SYNOPSIS
    Runs the CityX Unity test suite headlessly and reports in the same shape
    the Godot suite does.

.DESCRIPTION
    This is the Unity replacement for

        ./tools/Godot_v4.7.1-stable_win64_console.exe --headless --path . tests/X.tscn

    and it exists for the same reason: nothing in this project gets reported
    as working until something ran it. It prints "PASS: <name>" / "FAIL: <name>"
    per test and "OVERALL: PASS|FAIL" at the end, and exits 0 or 1, so it can
    be read the same way the Godot output has been all along.

    Two things it deliberately does NOT do quietly:

      * If the Unity editor is already open on this project, it stops with a
        clear message instead of hanging. Unity takes an exclusive lock on
        Library/, and a second instance will block forever rather than fail.
      * If the run produced no results XML at all -- which almost always means
        a C# compile error, not a test failure -- it says so and dumps the
        relevant part of the log, rather than reporting a vacuous pass.

.PARAMETER Platform
    EditMode (fast, no scene load), PlayMode (real frames), or All.

.PARAMETER Filter
    NUnit test name filter, e.g. CityX.Tests.LaunchArgsTests

.EXAMPLE
    ./tools/unity_tests.ps1
    ./tools/unity_tests.ps1 -Platform PlayMode
    ./tools/unity_tests.ps1 -Filter CityX.Tests.GameModeTests
#>
[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode', 'All')]
    [string]$Platform = 'EditMode',

    [string]$Filter = '',

    [switch]$Graphics,

    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'CityX'

if (-not (Test-Path (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'))) {
    Write-Host "FAIL: no Unity project at $projectPath" -ForegroundColor Red
    exit 1
}

# --- Resolve the editor the project was actually created with. Running a
# --- different version silently upgrades the project files, which is a
# --- messy thing to discover from a test run.
$versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:' } |
    Select-Object -First 1
$editorVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()

$editorExe = Join-Path "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor" 'Unity.exe'
if (-not (Test-Path $editorExe)) {
    Write-Host "FAIL: Unity $editorVersion not found at $editorExe" -ForegroundColor Red
    Write-Host "      Install it from Unity Hub, or edit this script if your Hub lives elsewhere."
    exit 1
}

# --- Exclusive-lock guard. Without this the run just hangs.
$lockFile = Join-Path $projectPath 'Temp/UnityLockfile'
if (Test-Path $lockFile) {
    $stillOpen = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue
    if ($stillOpen) {
        Write-Host "FAIL: the Unity editor is open on this project." -ForegroundColor Red
        Write-Host "      Only one process can hold Library/, so a batch run would hang, not fail."
        Write-Host "      Close the editor and re-run."
        exit 1
    }
    # Lockfile with no live process = a previous crash. Safe to continue.
}

$resultsDir = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$resultsFile = Join-Path $resultsDir "test-results-$Platform-$stamp.xml"
$logFile = Join-Path $resultsDir "test-run-$Platform-$stamp.log"

# Every path below has to be quoted. Start-Process joins -ArgumentList with
# spaces and does NOT quote for you, and this repo lives in a folder with a
# space in its name ("the game"), so an unquoted -projectPath arrives at
# Unity split in half.
function Quote([string]$s) { return '"' + $s + '"' }

$unityArgs = @(
    '-batchmode'
    '-projectPath', (Quote $projectPath)
    '-runTests'
    '-testPlatform', $Platform
    '-testResults', (Quote $resultsFile)
    '-logFile', (Quote $logFile)
)
if (-not $Graphics) { $unityArgs += '-nographics' }
if ($Filter -ne '') { $unityArgs += @('-testFilter', (Quote $Filter)) }

Write-Host "Running $Platform tests on Unity $editorVersion ..." -ForegroundColor Cyan

$proc = Start-Process -FilePath $editorExe -ArgumentList $unityArgs -PassThru -NoNewWindow
# Touching .Handle caches it; without this, .ExitCode below can throw
# "process has not exited" even after a successful wait.
$null = $proc.Handle
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    try { $proc.Kill() } catch {}
    Write-Host "FAIL: the run exceeded $TimeoutMinutes minutes and was killed." -ForegroundColor Red
    Write-Host "      Log: $logFile"
    exit 1
}
$unityExit = $proc.ExitCode

# --- No XML means it never got as far as running tests. Usually a C# compile
# --- error, but NOT always -- Unity also aborts before starting for bad args
# --- or a bad project path, and reporting that as "compile error" sends you
# --- looking in the wrong place. Diagnose before naming a cause.
if (-not (Test-Path $resultsFile)) {
    Write-Host "FAIL: the run produced no results file -- tests never executed." -ForegroundColor Red
    Write-Host "      Unity exit code: $unityExit"

    if (-not (Test-Path $logFile)) {
        Write-Host "      No log was written either, so Unity aborted before it started:" -ForegroundColor Yellow
        Write-Host "      bad arguments, a bad -projectPath, or the editor could not launch."
        exit 1
    }

    $compileErrors = Select-String -Path $logFile -Pattern 'error CS\d+|Compilation failed' |
        Select-Object -First 25
    if ($compileErrors) {
        Write-Host ""
        Write-Host "  --- compiler errors ---" -ForegroundColor Yellow
        foreach ($e in $compileErrors) { Write-Host "  $($e.Line.Trim())" }
    }
    else {
        # No compile errors found, so don't claim there were any. Show what
        # actually happened instead.
        Write-Host "      No compile errors in the log. Last 30 lines:" -ForegroundColor Yellow
        Write-Host ""
        foreach ($line in (Get-Content $logFile -Tail 30)) { Write-Host "  $line" }
    }
    Write-Host ""
    Write-Host "      Full log: $logFile"
    exit 1
}

[xml]$results = Get-Content $resultsFile
$run = $results.'test-run'

$cases = $results.SelectNodes('//test-case')
foreach ($case in $cases) {
    $name = $case.fullname
    if (-not $name) { $name = $case.name }
    switch ($case.result) {
        'Passed'  { Write-Host "PASS: $name" -ForegroundColor Green }
        'Skipped' { Write-Host "SKIP: $name" -ForegroundColor DarkGray }
        default {
            Write-Host "FAIL: $name" -ForegroundColor Red
            $message = $case.SelectSingleNode('failure/message')
            if ($message -and $message.InnerText) {
                foreach ($line in ($message.InnerText -split "`n")) {
                    if ($line.Trim() -ne '') { Write-Host "      $($line.Trim())" -ForegroundColor Red }
                }
            }
            $stack = $case.SelectSingleNode('failure/stack-trace')
            if ($stack -and $stack.InnerText) {
                $firstFrame = ($stack.InnerText -split "`n" | Where-Object { $_.Trim() -ne '' } | Select-Object -First 1)
                if ($firstFrame) { Write-Host "      at $($firstFrame.Trim())" -ForegroundColor DarkRed }
            }
        }
    }
}

$total = [int]$run.total
$passed = [int]$run.passed
$failed = [int]$run.failed
$skipped = [int]$run.skipped

Write-Host ""
Write-Host "$total test(s): $passed passed, $failed failed, $skipped skipped"
Write-Host "Results: $resultsFile"

# A green run that ran nothing is not a green run.
if ($total -eq 0) {
    Write-Host "OVERALL: FAIL (no tests were found -- check the filter and the asmdef)" -ForegroundColor Red
    exit 1
}
if ($failed -gt 0) {
    Write-Host "OVERALL: FAIL" -ForegroundColor Red
    exit 1
}
Write-Host "OVERALL: PASS" -ForegroundColor Green
exit 0
