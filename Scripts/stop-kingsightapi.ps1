# Stops leftover local API processes that lock bin\Debug\net8.0\kingsightapi.dll
$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoPattern = [regex]::Escape($repoRoot)

function Stop-ProcessTree {
    param([int]$ProcessId)

    if ($ProcessId -le 0) {
        return
    }

    Get-CimInstance Win32_Process |
        Where-Object { $_.ParentProcessId -eq $ProcessId } |
        ForEach-Object { Stop-ProcessTree -ProcessId $_.ProcessId }

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

function Stop-ListenersOnPort {
    param([int]$Port)

    $pids = @()

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($connections) {
        $pids += $connections | Select-Object -ExpandProperty OwningProcess -Unique
    }

    $netstat = netstat -ano -p tcp 2>$null | Select-String ":$Port\s"
    foreach ($line in $netstat) {
        if ($line -match '\s(\d+)\s*$') {
            $pids += [int]$Matches[1]
        }
    }

    foreach ($processId in ($pids | Select-Object -Unique)) {
        Stop-ProcessTree -ProcessId $processId
    }
}

foreach ($port in @(7140, 5181, 10188)) {
    Stop-ListenersOnPort -Port $port
}

Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object {
        $line = $_.CommandLine
        if ([string]::IsNullOrWhiteSpace($line)) {
            return $false
        }

        if ($line -match 'kingsightapi') {
            return $true
        }

        if ($line -match $repoPattern) {
            return $true
        }

        return $false
    } |
    ForEach-Object { Stop-ProcessTree -ProcessId $_.ProcessId }

Start-Sleep -Milliseconds 750
Write-Host "Stopped leftover kingsightapi processes. You can run dotnet build / dotnet run."
