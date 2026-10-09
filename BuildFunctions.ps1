#Requires -Version 7.4

<#
.SYNOPSIS
    The helpers of build.ps1: log lines, native commands, the site emulator, file hashes.

.DESCRIPTION
    Dot-sourced by build.ps1. Nothing here runs on its own.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

function Write-Step {
    # A step of the build: "==> name".
    param([Parameter(Mandatory)] [string] $Name)
    Write-Host "==> $Name"
}

function Write-Pass {
    param([Parameter(Mandatory)] [string] $Text)
    Write-Host "PASS $Text"
}

function Stop-Build {
    # "FAIL text", then the build ends: a failed step is never followed by another.
    param([Parameter(Mandatory)] [string] $Text)
    Write-Host "FAIL $Text"
    throw $Text
}

function Assert-Tool {
    # A tool of the build is on the path, or the build says which one is missing and why it is needed.
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [string] $Purpose)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Stop-Build "$Name is not installed: $Purpose"
    }
}

function Get-FileSha256 {
    # The SHA-256 of a file, in lower-case hexadecimal.
    param([Parameter(Mandatory)] [string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-TextFile {
    # A text file in UTF-8 without a byte order mark, exactly as given.
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [AllowEmptyString()] [string] $Text)
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Get-FreeTcpPort {
    # A port nothing listens on, chosen by the operating system.
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

function Start-SiteEmulator {
    # Serves a folder with the Static Web Apps CLI emulator, which applies staticwebapp.config.json as Azure does
    # (routes, rewrites, headers, the navigation fallback), and waits until it answers. Returns the process and the
    # address.
    param(
        [Parameter(Mandatory)] [string] $SitePath,
        [Parameter(Mandatory)] [string] $RepositoryRoot,
        [Parameter(Mandatory)] [string] $LogPath
    )
    $swa = Join-Path $RepositoryRoot 'node_modules' '.bin' ($IsWindows ? 'swa.cmd' : 'swa')
    if (-not (Test-Path -LiteralPath $swa)) {
        Stop-Build "The Static Web Apps CLI is not in node_modules: run npm ci in $RepositoryRoot"
    }
    $port = Get-FreeTcpPort
    $address = "http://127.0.0.1:$port"
    $start = @{
        FilePath               = $swa
        ArgumentList           = @('start', $SitePath, '--host', '127.0.0.1', '--port', "$port")
        WorkingDirectory       = $SitePath
        RedirectStandardOutput = $LogPath
        RedirectStandardError  = "$LogPath.err"
        PassThru               = $true
    }
    $process = Start-Process @start
    $deadline = [datetime]::UtcNow.AddSeconds(90)
    while ([datetime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            Stop-Build "The emulator ended with exit code $($process.ExitCode); its output is in $LogPath and $LogPath.err"
        }
        try {
            $answer = Invoke-WebRequest -Uri "$address/" -TimeoutSec 5 -SkipHttpErrorCheck
            if ($answer.StatusCode -eq 200) {
                return @{ Process = $process; Address = $address }
            }
        }
        catch [System.Net.Http.HttpRequestException], [System.Threading.Tasks.TaskCanceledException] {
            Start-Sleep -Milliseconds 500
        }
    }
    Stop-SiteEmulator -Process $process
    Stop-Build "The emulator did not answer at $address within 90 seconds; its output is in $LogPath and $LogPath.err"
}

function Stop-SiteEmulator {
    # Ends the emulator and every process it started.
    param([Parameter(Mandatory)] [System.Diagnostics.Process] $Process)
    if (-not $Process.HasExited) {
        $Process.Kill($true)
        $Process.WaitForExit(15000) | Out-Null
    }
}
