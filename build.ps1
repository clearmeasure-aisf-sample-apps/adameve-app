#Requires -Version 7.4

<#
.SYNOPSIS
    The steps of the build: Init, Analyze, Compile, UnitTests, Publish, PayloadBudget, StaticFiles,
    IntegrationTests, AcceptanceTests, BuildFacts, ContainerImage, DeployPackage; and Build, which runs them in that
    order.

.DESCRIPTION
    Dot-sourced by PrivateBuild.ps1, which is the command to run. A step can be run alone after the ones before it:
        . ./build.ps1 ; Init ; Compile ; UnitTests

    What the build leaves behind:
        build/publish            the published host (src/AdamEve.Host) with the published client as its wwwroot,
                                 files.json and build-facts.json: the content of the image, and what the tests ask
        build/container-image    container-image.tar.gz: the image container-image:<version>, as "docker load" reads
                                 it. The release pushes it to the system's registry
        build/deploy-package     the package: deploy.ps1, verify.ps1, settings.json, infra/main.bicep
        TestResults              trx files, coverage, the host's log, Playwright's traces

    The image is made after every test has passed against the very files it holds, and it carries what the tests
    measured (build-facts.json). A build that stops early leaves no image to release.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. (Join-Path $PSScriptRoot 'BuildFunctions.ps1')

$script:baseDir = $PSScriptRoot
$script:solution = Join-Path $baseDir 'AdamEve.slnx'
$script:hostProject = Join-Path $baseDir 'src' 'AdamEve.Host'
$script:unitTestProject = Join-Path $baseDir 'tests' 'AdamEve.UnitTests'
$script:integrationTestProject = Join-Path $baseDir 'tests' 'AdamEve.IntegrationTests'
$script:acceptanceTestProject = Join-Path $baseDir 'tests' 'AdamEve.AcceptanceTests'
$script:buildDir = Join-Path $baseDir 'build'
$script:publishDir = Join-Path $buildDir 'publish'
$script:siteDir = Join-Path $publishDir 'wwwroot'
$script:imageDir = Join-Path $buildDir 'container-image'
$script:packageDir = Join-Path $buildDir 'deploy-package'
$script:testResultsDir = Join-Path $baseDir 'TestResults'
$script:configuration = 'Release'
$script:version = '1.0.0'
$script:ranBy = 'private'
$script:site = $null

# The first load of the game on a phone: 3.0 MB, measured over the brotli files the publish step writes.
$script:payloadBudgetBytes = 3.0 * 1024 * 1024
$script:title = 'Adam and woman in the garden of Eden'

function Init {
    Write-Step 'Init'
    Assert-Tool -Name 'dotnet' -Purpose 'the .NET 10 SDK compiles, tests and publishes the solution'
    Assert-Tool -Name 'git' -Purpose 'the build facts count the files Git tracks'
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Stop-Build 'The module PSScriptAnalyzer is not installed: Install-Module PSScriptAnalyzer -Scope CurrentUser'
    }

    foreach ($directory in $buildDir, $testResultsDir) {
        if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory -Recurse -Force }
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $env:DOTNET_NOLOGO = 'true'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
    dotnet restore $solution
    Write-Pass "tools found, build/ and TestResults/ empty, packages restored (version $version, run by the $ranBy build)"
}

function Analyze {
    Write-Step 'Analyze'
    $files = @(Get-ChildItem -LiteralPath $baseDir -Recurse -File -Include '*.ps1' |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj|build|TestResults)[\\/]' })
    $settings = Join-Path $baseDir 'PSScriptAnalyzerSettings.psd1'
    $findings = @($files | ForEach-Object { Invoke-ScriptAnalyzer -Path $_.FullName -Settings $settings })
    foreach ($finding in $findings) {
        Write-Host "  $([System.IO.Path]::GetRelativePath($baseDir, $finding.ScriptPath)):$($finding.Line) $($finding.RuleName): $($finding.Message)"
    }
    if ($findings.Count -gt 0) { Stop-Build "PSScriptAnalyzer: $($findings.Count) finding(s) in $($files.Count) scripts; a warning is an error" }
    Write-Pass "PSScriptAnalyzer: no finding in $($files.Count) scripts"

    # The application's own infrastructure code must compile without a diagnostic before it is released. The Azure
    # CLI brings Bicep; the integration build always has it.
    $template = Join-Path $baseDir 'deploy' 'infra' 'main.bicep'
    if (Get-Command az -ErrorAction SilentlyContinue) {
        $PSNativeCommandUseErrorActionPreference = $false
        $said = @(az bicep build --file $template --stdout 2>&1 | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
        $code = $LASTEXITCODE
        $PSNativeCommandUseErrorActionPreference = $true
        $diagnostics = @($said | Where-Object { $_ -match ' : (Warning|Error) ' })
        foreach ($line in $diagnostics) { Write-Host "  $line" }
        if ($code -ne 0 -or $diagnostics.Count -gt 0) {
            if ($diagnostics.Count -eq 0) { foreach ($line in $said) { Write-Host "  $line" } }
            Stop-Build "deploy/infra/main.bicep does not compile without a diagnostic (exit code $code)"
        }
        Write-Pass 'deploy/infra/main.bicep compiles without a diagnostic'
    }
    else {
        Write-Host 'SKIP deploy/infra/main.bicep is not compiled: the Azure CLI is not installed (the integration build has it)'
    }
}

function Compile {
    Write-Step 'Compile'
    dotnet build $solution --configuration $configuration --no-restore "-p:Version=$version"
    Write-Pass 'the solution compiles without a warning'
}

function Invoke-TestProject {
    # dotnet test of one project, with its trx file and its coverage under TestResults/<kind>.
    param([Parameter(Mandatory)] [string] $Project, [Parameter(Mandatory)] [string] $Kind)
    $results = Join-Path $testResultsDir $Kind
    dotnet test $Project --configuration $configuration --no-build --logger "trx;LogFileName=$Kind.trx" --results-directory $results --collect 'XPlat Code Coverage'
    Write-Pass "$Kind tests"
}

function UnitTests {
    Write-Step 'UnitTests'
    Invoke-TestProject -Project $unitTestProject -Kind 'unit'
}

function Publish {
    Write-Step 'Publish'
    # The host's publish publishes the client too and takes its wwwroot (AdamEve.Host.csproj, target PublishClient).
    dotnet publish $hostProject --configuration $configuration --output $publishDir "-p:Version=$version"
    foreach ($file in 'index.html', '404.html') {
        if (-not (Test-Path -LiteralPath (Join-Path $siteDir $file))) { Stop-Build "The published site has no $file" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'AdamEve.Host.dll'))) { Stop-Build 'The published host has no AdamEve.Host.dll' }
    Write-Pass "the host is published to $([System.IO.Path]::GetRelativePath($baseDir, $publishDir)), the client is its wwwroot"
}

function PayloadBudget {
    # What a first visit downloads: every file of the site but the ones a first visit never asks for, each at the
    # size of its brotli file where the publish step wrote one (the host answers with that file). Of the three ICU data files the runtime loads one,
    # by the browser's language: the largest counts.
    Write-Step 'PayloadBudget'
    $neverAsked = '^404\.html$'
    $total = 0L
    $largestIcu = 0L
    $count = 0
    foreach ($file in Get-ChildItem -LiteralPath $siteDir -Recurse -File | Where-Object { $_.Extension -notin '.br', '.gz' }) {
        $path = [System.IO.Path]::GetRelativePath($siteDir, $file.FullName).Replace('\', '/')
        if ($path -match $neverAsked) { continue }
        $brotli = "$($file.FullName).br"
        $size = if (Test-Path -LiteralPath $brotli) { (Get-Item -LiteralPath $brotli).Length } else { $file.Length }
        if ($file.Name -match '^icudt.*\.dat$') {
            if ($size -gt $largestIcu) { $largestIcu = $size }
            continue
        }
        $total += $size
        $count++
    }
    $total += $largestIcu
    $megabytes = [Math]::Round($total / 1MB, 2)
    $budget = [Math]::Round($payloadBudgetBytes / 1MB, 1)
    if ($total -gt $payloadBudgetBytes) {
        Stop-Build "The first load is $megabytes MB ($count files and one ICU data file, brotli): over the budget of $budget MB"
    }
    Write-Pass "the first load is $megabytes MB ($count files and one ICU data file, brotli), within the budget of $budget MB"
}

function StaticFiles {
    # What the host is to serve, checked before anything asks it: a fingerprint in the name of every file a browser
    # may keep for a year, the title, the import map; and files.json, the list of every file with its size and
    # SHA-256, which the tests and verify.ps1 compare the answers with.
    Write-Step 'StaticFiles'
    $immutable = '_framework', 'assets'
    $unmarked = [System.Collections.Generic.List[string]]::new()
    foreach ($folder in $immutable) {
        $path = Join-Path $siteDir $folder
        if (-not (Test-Path -LiteralPath $path)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File) {
            $name = $file.Name -replace '\.(br|gz)$', ''
            if ($name -cnotmatch '\.[a-z0-9]{10}\.[A-Za-z0-9]+$') { $unmarked.Add([System.IO.Path]::GetRelativePath($siteDir, $file.FullName)) }
        }
    }
    if ($unmarked.Count -gt 0) {
        Stop-Build "Served as immutable without a fingerprint in the name: $($unmarked -join ', ')"
    }
    Write-Pass "every file under $(($immutable | ForEach-Object { "/$_/" }) -join ', ') has a fingerprint in its name"

    $index = Get-Content -LiteralPath (Join-Path $siteDir 'index.html') -Raw
    if ($index -notmatch [regex]::Escape("<title>$title</title>")) { Stop-Build "index.html does not have the title `"$title`"" }

    # The publish step writes the import map into index.html: the fingerprinted name of every script of the runtime.
    # It is an inline script, which the content security policy refuses (script-src 'self'), and without it the
    # runtime does not start. The host names this one import map by its SHA-256 in the policy it sends
    # (src/AdamEve.Host/ContentSecurityPolicy.cs): no other inline script runs. The integration tests compare the
    # header with the page, and the full-system tests fail on any error a browser reports.
    $importMap = [regex]::Match($index, '(?s)<script type="importmap">(.*?)</script>')
    if ($importMap.Success -and $importMap.Groups[1].Value.Trim()) {
        Write-Pass 'index.html has its import map: the host allows it by its hash, and no other inline script'
    }
    else {
        Write-Host 'SKIP index.html has no import map: the content security policy allows no inline script at all'
    }

    $files = @(Get-ChildItem -LiteralPath $siteDir -Recurse -File |
            Where-Object { $_.Extension -notin '.br', '.gz' } |
            Sort-Object -Property FullName |
            ForEach-Object {
                [ordered]@{
                    path   = [System.IO.Path]::GetRelativePath($siteDir, $_.FullName).Replace('\', '/')
                    size   = $_.Length
                    sha256 = Get-FileSha256 -Path $_.FullName
                }
            })
    Write-TextFile -Path (Join-Path $publishDir 'files.json') -Text ((@{ version = $version; files = $files } | ConvertTo-Json -Depth 4) + "`n")
    Write-Pass "files.json lists $($files.Count) files"
}

function Start-Site {
    # The published host as a process, once for the integration tests and the full-system tests: the same files the
    # image gets, served by the same code.
    if ($null -ne $script:site) { return }
    $script:site = Start-SiteHost -PublishPath $publishDir -LogPath (Join-Path $testResultsDir 'host.log')
    $env:ADAMEVE_BASE_URL = $script:site.Address
    $env:ADAMEVE_VERSION = $version
    Write-Host "  the host serves the site at $($script:site.Address)"
}

function Stop-Site {
    if ($null -eq $script:site) { return }
    Stop-SiteHost -Process $script:site.Process
    $script:site = $null
}

function IntegrationTests {
    Write-Step 'IntegrationTests'
    Start-Site
    Invoke-TestProject -Project $integrationTestProject -Kind 'integration'
}

function AcceptanceTests {
    Write-Step 'AcceptanceTests'
    $playwright = Join-Path $acceptanceTestProject 'bin' $configuration 'net10.0' 'playwright.ps1'
    if (-not (Test-Path -LiteralPath $playwright)) { Stop-Build "$playwright is missing: Compile first" }
    # The integration build installs the browsers with the system packages they need; a desk has those already.
    $browsers = if ($ranBy -eq 'integration') { @('install', '--with-deps', 'chromium', 'webkit') } else { @('install', 'chromium', 'webkit') }
    & $playwright @browsers
    if ($LASTEXITCODE -ne 0) { Stop-Build "playwright $($browsers -join ' ') ended with exit code $LASTEXITCODE" }
    Start-Site
    Invoke-TestProject -Project $acceptanceTestProject -Kind 'acceptance'
}

function BuildFacts {
    # After the last test: the facts count the tests. The file goes beside the host, which answers it at /_build.
    Write-Step 'BuildFacts'
    $facts = @{
        OutputPath      = Join-Path $publishDir 'build-facts.json'
        RepoRoot        = $baseDir
        Version         = $version
        TestResultsPath = $testResultsDir
        RanBy           = $ranBy
    }
    & (Join-Path $baseDir 'scripts' 'Write-BuildFacts.ps1') @facts
    if ($LASTEXITCODE -ne 0) { Stop-Build "Write-BuildFacts.ps1 ended with exit code $LASTEXITCODE" }
    Write-Pass 'build-facts.json is written beside the host'
}

function ContainerImage {
    # The image, from the published folder the tests asked, with the facts just written: the .NET SDK builds it
    # (no Dockerfile, no Docker daemon) on the ASP.NET Core runtime image and writes it as an archive in the format
    # of "docker save". The release loads container-image.tar.gz, finds container-image:<version> in it and pushes
    # that to the system's registry.
    Write-Step 'ContainerImage'
    New-Item -ItemType Directory -Path $imageDir -Force | Out-Null
    $archive = Join-Path $imageDir 'container-image.tar'
    $image = @(
        '-t:PublishContainer'
        "-p:Configuration=$configuration"
        "-p:Version=$version"
        "-p:ContainerImageTag=$version"
        "-p:PublishDir=$publishDir$([System.IO.Path]::DirectorySeparatorChar)"
        "-p:ContainerArchiveOutputPath=$archive"
    )
    dotnet msbuild $hostProject @image
    if (-not (Test-Path -LiteralPath $archive)) { Stop-Build "The SDK wrote no image archive to $archive" }

    $compressed = "$archive.gz"
    $source = [System.IO.File]::OpenRead($archive)
    try {
        $target = [System.IO.File]::Create($compressed)
        try {
            $gzip = [System.IO.Compression.GZipStream]::new($target, [System.IO.Compression.CompressionLevel]::Fastest)
            try { $source.CopyTo($gzip) }
            finally { $gzip.Dispose() }
        }
        finally { $target.Dispose() }
    }
    finally { $source.Dispose() }
    Remove-Item -LiteralPath $archive -Force
    $megabytes = [Math]::Round((Get-Item -LiteralPath $compressed).Length / 1MB, 1)
    Write-Pass "container-image:$version is in build/container-image/container-image.tar.gz ($megabytes MB)"

    # Where a Docker daemon answers (the integration build), the archive is loaded as the release loads it and the
    # image is run: it must answer its version and its health on port 8080, as the unprivileged user it runs as.
    if (-not (Test-DockerDaemon)) {
        Write-Host 'SKIP the image is not loaded and run: no Docker daemon answers here (the integration build has one)'
        return
    }
    Test-ContainerImage -ArchivePath $compressed -Image "container-image:$version" -Version $version -LogPath (Join-Path $testResultsDir 'container.log')
    Write-Pass "container-image:$version loads with docker, and its container answers /_version, /_healthcheck and /_build on port 8080"
}

function DeployPackage {
    # The whole package the release sends to Octopus: the deploy/ folder as committed. What it deploys is not in it:
    # deploy.ps1 names the image of a version by its tag in the system's registry.
    Write-Step 'DeployPackage'
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    Copy-Item -Path (Join-Path $baseDir 'deploy' '*') -Destination $packageDir -Recurse
    foreach ($file in 'deploy.ps1', 'verify.ps1', 'settings.json', (Join-Path 'infra' 'main.bicep')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageDir $file))) { Stop-Build "The package has no $file" }
    }
    Write-Pass 'build/deploy-package: deploy.ps1, verify.ps1, settings.json and infra/main.bicep'
}

function Build {
    param([string] $Version = '1.0.0', [switch] $CI)
    $script:version = $Version
    $script:ranBy = $CI ? 'integration' : 'private'
    $started = [datetime]::UtcNow
    try {
        Init
        Analyze
        Compile
        UnitTests
        Publish
        PayloadBudget
        StaticFiles
        IntegrationTests
        AcceptanceTests
    }
    finally {
        Stop-Site
    }
    BuildFacts
    ContainerImage
    DeployPackage
    Write-Host "PASS the private build of $version, in $([int]([datetime]::UtcNow - $started).TotalSeconds) seconds"
}
