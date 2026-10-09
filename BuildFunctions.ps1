#Requires -Version 7.4

<#
.SYNOPSIS
    The helpers of build.ps1: log lines, tools, file hashes, the site's host as a process, the image as a container.

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

function Wait-Site {
    # Asks an address until it answers 200, or until the time is up. True when it answered.
    param([Parameter(Mandatory)] [string] $Address, [Parameter(Mandatory)] [int] $Seconds, [scriptblock] $Ended = { $false })
    $deadline = [datetime]::UtcNow.AddSeconds($Seconds)
    while ([datetime]::UtcNow -lt $deadline) {
        if (& $Ended) { return $false }
        try {
            $answer = Invoke-WebRequest -Uri $Address -TimeoutSec 5 -SkipHttpErrorCheck
            if ($answer.StatusCode -eq 200) { return $true }
        }
        catch [System.Net.Http.HttpRequestException], [System.Threading.Tasks.TaskCanceledException] {
            Write-Verbose "No answer from $Address yet: $($_.Exception.Message)"
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Start-SiteHost {
    # Starts the published host (dotnet AdamEve.Host.dll) on a free port of this machine, from its own folder, which
    # is its content root, and waits until it answers. Returns the process and the address.
    param(
        [Parameter(Mandatory)] [string] $PublishPath,
        [Parameter(Mandatory)] [string] $LogPath
    )
    $port = Get-FreeTcpPort
    $address = "http://127.0.0.1:$port"
    $start = @{
        FilePath               = 'dotnet'
        ArgumentList           = @('AdamEve.Host.dll', '--urls', $address)
        WorkingDirectory       = $PublishPath
        RedirectStandardOutput = $LogPath
        RedirectStandardError  = "$LogPath.err"
        PassThru               = $true
    }
    $process = Start-Process @start
    if (Wait-Site -Address "$address/_healthcheck" -Seconds 60 -Ended { $process.HasExited }) {
        return @{ Process = $process; Address = $address }
    }
    if ($process.HasExited) {
        Stop-Build "The host ended with exit code $($process.ExitCode); its output is in $LogPath and $LogPath.err"
    }
    Stop-SiteHost -Process $process
    Stop-Build "The host did not answer Healthy at $address/_healthcheck within 60 seconds; its output is in $LogPath and $LogPath.err"
}

function Stop-SiteHost {
    # Ends the host and every process it started.
    param([Parameter(Mandatory)] [System.Diagnostics.Process] $Process)
    if (-not $Process.HasExited) {
        $Process.Kill($true)
        $Process.WaitForExit(15000) | Out-Null
    }
}

function Test-DockerDaemon {
    # True when a Docker daemon answers this account.
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return $false }
    $PSNativeCommandUseErrorActionPreference = $false
    docker info *> $null
    return $LASTEXITCODE -eq 0
}

function Test-ContainerImage {
    # Loads an image archive as the release does (gunzip, docker load), runs the image and asks the container what a
    # deployment asks: its version, its health, its build facts. Stops the build when one of them is not as built.
    param(
        [Parameter(Mandatory)] [string] $ArchivePath,
        [Parameter(Mandatory)] [string] $Image,
        [Parameter(Mandatory)] [string] $Version,
        [Parameter(Mandatory)] [string] $LogPath
    )
    $PSNativeCommandUseErrorActionPreference = $false
    $loaded = @(docker load --input $ArchivePath 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { Stop-Build "docker load of $ArchivePath ended with exit code $($LASTEXITCODE): $($loaded -join ' ')" }
    docker image inspect $Image *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Build "The archive holds no image $($Image): docker load said $($loaded -join ' ')" }

    $port = Get-FreeTcpPort
    $container = "$(docker run --detach --rm --publish "127.0.0.1:$($port):8080" $Image 2>&1)".Trim()
    if ($LASTEXITCODE -ne 0) { Stop-Build "docker run of $Image ended with exit code $($LASTEXITCODE): $container" }
    $address = "http://127.0.0.1:$port"
    $problem = ''
    try {
        if (-not (Wait-Site -Address "$address/_healthcheck" -Seconds 60)) {
            $problem = "the container did not answer $address/_healthcheck within 60 seconds"
        }
        else {
            $health = "$(Invoke-RestMethod -Uri "$address/_healthcheck" -TimeoutSec 10)".Trim()
            $served = [string] (Invoke-RestMethod -Uri "$address/_version" -TimeoutSec 10).version
            $facts = Invoke-RestMethod -Uri "$address/_build" -TimeoutSec 10
            $front = Invoke-WebRequest -Uri "$address/" -TimeoutSec 10
            if ($health -ne 'Healthy') { $problem = "/_healthcheck answers `"$health`"" }
            elseif ($served -ne $Version) { $problem = "/_version answers `"$served`", not $Version" }
            elseif ([string] $facts.version -ne $Version -or $null -eq $facts.tests) { $problem = '/_build does not answer the facts of this build with its tests' }
            elseif ($front.StatusCode -ne 200 -or "$($front.Headers['Content-Security-Policy'])" -notmatch "'sha256-") { $problem = '/ does not answer the page with the import map allowed by hash' }
        }
    }
    finally {
        docker logs $container *> $LogPath
        docker stop $container *> $null
    }
    if ($problem) { Stop-Build "The image $Image is not as built: $problem; the container's output is in $LogPath" }
}
