[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$TestPlatform = 'EditMode',

    [string]$TestFilter,

    [string]$AssemblyNames,

    [string]$UnityEditorPath,

    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

if (-not $ProjectPath) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Resolve-UnityEditorPath {
    param(
        [string]$ExplicitPath,
        [string]$UnityProjectPath
    )

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "Unity Editor was not found at the supplied path: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    if ($env:UNITY_EDITOR_PATH) {
        if (-not (Test-Path -LiteralPath $env:UNITY_EDITOR_PATH -PathType Leaf)) {
            throw "UNITY_EDITOR_PATH points to a missing file: $env:UNITY_EDITOR_PATH"
        }

        return (Resolve-Path -LiteralPath $env:UNITY_EDITOR_PATH).Path
    }

    $versionFile = Join-Path $UnityProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "ProjectVersion.txt was not found under: $UnityProjectPath"
    }

    $versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
    $editorVersion = ($versionLine -split ':', 2)[1].Trim()
    if (-not $editorVersion) {
        throw "Unable to read the Unity editor version from: $versionFile"
    }

    $candidateRoots = @(
        (Join-Path $env:ProgramFiles 'Unity\Hub\Editor'),
        (Join-Path ${env:ProgramFiles(x86)} 'Unity\Hub\Editor')
    )

    foreach ($drive in Get-PSDrive -PSProvider FileSystem) {
        $candidateRoots += (Join-Path $drive.Root 'Unity\Hub\Editor')
        $candidateRoots += (Join-Path $drive.Root 'Program Files\Unity\Hub\Editor')
        $candidateRoots += (Join-Path $drive.Root 'UnityEngine\Engine')
    }

    foreach ($root in $candidateRoots | Select-Object -Unique) {
        if (-not $root) {
            continue
        }

        $candidate = Join-Path $root "$editorVersion\Editor\Unity.exe"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Unity $editorVersion was not found automatically. Pass -UnityEditorPath or set UNITY_EDITOR_PATH to Unity.exe."
}

$resolvedProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$resolvedUnityPath = Resolve-UnityEditorPath -ExplicitPath $UnityEditorPath -UnityProjectPath $resolvedProjectPath

$runningProjectEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($resolvedProjectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 }

if ($runningProjectEditor) {
    throw "This project appears to be open in Unity Editor. Close that Editor instance before running batch-mode tests for the same path."
}

$resultDirectory = Join-Path $resolvedProjectPath 'Logs\Codex\TestResults'
$logDirectory = Join-Path $resolvedProjectPath 'Logs\Codex'
New-Item -ItemType Directory -Force -Path $resultDirectory, $logDirectory | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$resultPath = Join-Path $resultDirectory "$TestPlatform-$timestamp.xml"
$logPath = Join-Path $logDirectory "Codex-$TestPlatform-$timestamp.log"

$unityArguments = @(
    '-batchmode',
    '-runTests',
    '-projectPath', $resolvedProjectPath,
    '-testPlatform', $TestPlatform,
    '-testResults', $resultPath,
    '-logFile', $logPath
)

if ($TestFilter) {
    $unityArguments += @('-testFilter', $TestFilter)
}

if ($AssemblyNames) {
    $unityArguments += @('-assemblyNames', $AssemblyNames)
}

Write-Host "Unity:   $resolvedUnityPath"
Write-Host "Project: $resolvedProjectPath"
Write-Host "Results: $resultPath"
Write-Host "Log:     $logPath"

& $resolvedUnityPath @unityArguments
$unityExitCode = $LASTEXITCODE

if (Test-Path -LiteralPath $logPath) {
    Get-Content -LiteralPath $logPath -Tail 80
}

if ($unityExitCode -ne 0) {
    throw "Unity exited with code $unityExitCode. Inspect: $logPath"
}

if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw "Unity exited without producing a test result file. Inspect: $logPath"
}

Write-Host "Unity $TestPlatform tests completed. Results: $resultPath"
