# 在已有 Dedicated 玩家构建上做 READY 烟测。不负责 Unity 出包。
# 用法：$env:ACTGAME_DEDICATED_EXE = "D:\Builds\Dedicated\ACTGameServer.exe"; .\tools\dedicated\smoke-ready.ps1

param(
    [string]$Exe = $env:ACTGAME_DEDICATED_EXE,
    [int]$Port = 17777,
    [int]$TimeoutSec = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Exe) -or -not (Test-Path -LiteralPath $Exe)) {
    Write-Error "未找到 Dedicated 可执行文件。请设置 ACTGAME_DEDICATED_EXE 或传入 -Exe。当前值: '$Exe'"
    exit 2
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("actgame-ds-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
$logFile = Join-Path $work "server.log"
$readyPattern = "DedicatedServerBootstrap: READY port="

$process = Start-Process -FilePath $Exe -ArgumentList @(
    "-batchmode",
    "-nographics",
    "-logFile", $logFile,
    "-actgame-port", "$Port",
    "-actgame-empty-lobby-ms", "15000",
    "-actgame-exit-on-match-end", "1"
) -PassThru -WindowStyle Hidden

$deadline = [datetime]::UtcNow.AddSeconds($TimeoutSec)
$ready = $false
$failed = $false
try {
    while ([datetime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $logFile) {
            $text = Get-Content -LiteralPath $logFile -Raw -ErrorAction SilentlyContinue
            if ($text -match [regex]::Escape($readyPattern)) {
                $ready = $true
                break
            }
            if ($text -match "DedicatedServerBootstrap: 启动失败 exit=") {
                $failed = $true
                break
            }
        }

        if ($process.HasExited) {
            break
        }

        Start-Sleep -Milliseconds 400
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
    }
}

if ($ready) {
    Write-Host "READY ok. log=$logFile"
    exit 0
}

Write-Host "烟测失败 ready=$ready failed=$failed exitCode=$($process.ExitCode) log=$logFile"
if (Test-Path -LiteralPath $logFile) {
    Get-Content -LiteralPath $logFile -Tail 40
}
exit 1
