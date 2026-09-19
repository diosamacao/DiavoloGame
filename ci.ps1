param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [string]$DedicatedExe = $env:ACTGAME_DEDICATED_EXE,
    [switch]$SkipDedicatedSmoke
)

# Local quality gate. Close any Unity Editor using this project before running.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = $PSScriptRoot
$artifacts = Join-Path $repo "Temp\CI"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = "D:\UnityEngine\Engine\2022.3.62f3c1\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity Editor not found at '$UnityPath'. Set UNITY_EDITOR_PATH."
}

function Invoke-UnityGate {
    param(
        [string]$Name,
        [string[]]$Arguments,
        [string]$LogFile
    )

    Write-Host "== $Name =="
    & $UnityPath @Arguments -logFile $LogFile
    $exitCode = $LASTEXITCODE
    $log = if (Test-Path -LiteralPath $LogFile) {
        Get-Content -LiteralPath $LogFile -Raw
    } else {
        ""
    }
    if (($exitCode -ne 0) -or
        ($log -match "Aborting batchmode") -or
        ($log -match "Scripts have compiler errors") -or
        ($log -match "Compilation failed")) {
        Write-Host $log
        throw "$Name failed. exit=$exitCode log=$LogFile"
    }
}

$common = @("-batchmode", "-quit", "-projectPath", $repo)
Invoke-UnityGate `
    -Name "Structure + Content Audit" `
    -Arguments ($common + @("-executeMethod", "StructureValidationBatch.RunAll")) `
    -LogFile (Join-Path $artifacts "structure.log")

$results = Join-Path $artifacts "editmode-results.xml"
Invoke-UnityGate `
    -Name "EditMode Tests" `
    -Arguments ($common + @(
        "-runTests",
        "-testPlatform", "EditMode",
        "-testResults", $results
    )) `
    -LogFile (Join-Path $artifacts "editmode.log")

if (-not $SkipDedicatedSmoke) {
    if ([string]::IsNullOrWhiteSpace($DedicatedExe)) {
        Write-Host "Dedicated smoke skipped: ACTGAME_DEDICATED_EXE is not set."
    } else {
        & (Join-Path $repo "tools\dedicated\smoke-ready.ps1") -Exe $DedicatedExe
        if ($LASTEXITCODE -ne 0) {
            throw "Dedicated smoke failed. exit=$LASTEXITCODE"
        }
    }
}

Write-Host "ACTGame CI gates passed."
