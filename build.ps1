#Requires -Version 7.4

<#
.SYNOPSIS
    The steps of the build: Init, Analyze, Compile, UnitTests, Publish, PayloadBudget, StaticFiles,
    IntegrationTests, AcceptanceTests, BuildFacts, DeployPackage; and Build, which runs them in that order.

.DESCRIPTION
    Dot-sourced by PrivateBuild.ps1, which is the command to run. A step can be run alone after the ones before it:
        . ./build.ps1 ; Init ; Compile ; UnitTests

    What the build leaves behind:
        build/publish/wwwroot    the site: the published client with _health/ (healthcheck.txt, alive.txt,
                                 version.json, build-facts.json, files.json)
        build/deploy-package     the package: deploy.ps1, verify.ps1, main.bicep, site.zip, version.txt
        TestResults              trx files, coverage, the emulator's log, Playwright's traces

    healthcheck.txt says "Pending" until every test has passed against the published site; only then does the build
    write "Healthy" into it. A build that stops early cannot ship a site that says it is healthy.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. (Join-Path $PSScriptRoot 'BuildFunctions.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:baseDir = $PSScriptRoot
$script:solution = Join-Path $baseDir 'AdamEve.slnx'
$script:clientProject = Join-Path $baseDir 'src' 'AdamEve.Client'
$script:unitTestProject = Join-Path $baseDir 'tests' 'AdamEve.UnitTests'
$script:integrationTestProject = Join-Path $baseDir 'tests' 'AdamEve.IntegrationTests'
$script:acceptanceTestProject = Join-Path $baseDir 'tests' 'AdamEve.AcceptanceTests'
$script:buildDir = Join-Path $baseDir 'build'
$script:publishDir = Join-Path $buildDir 'publish'
$script:siteDir = Join-Path $publishDir 'wwwroot'
$script:healthDir = Join-Path $siteDir '_health'
$script:packageDir = Join-Path $buildDir 'deploy-package'
$script:testResultsDir = Join-Path $baseDir 'TestResults'
$script:configuration = 'Release'
$script:version = '1.0.0'
$script:ranBy = 'private'
$script:emulator = $null

# The first load of the game on a phone: 3.0 MB, measured over the brotli files the publish step writes.
$script:payloadBudgetBytes = 3.0 * 1024 * 1024
$script:title = 'Adam and woman in the garden of Eden'

function Init {
    Write-Step 'Init'
    Assert-Tool -Name 'dotnet' -Purpose 'the .NET 10 SDK compiles, tests and publishes the solution'
    Assert-Tool -Name 'node' -Purpose 'Node.js 20 or later runs the Static Web Apps CLI emulator the tests ask'
    Assert-Tool -Name 'npm' -Purpose 'npm installs the Static Web Apps CLI emulator'
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
    if (-not (Test-Path -LiteralPath (Join-Path $baseDir 'node_modules' '.bin'))) {
        Push-Location -LiteralPath $baseDir
        try { npm ci --no-audit --no-fund }
        finally { Pop-Location }
    }
    dotnet restore $solution
    Write-Pass "tools found, build/ and TestResults/ empty, packages restored (version $version, run by the $ranBy build)"
}

function Analyze {
    Write-Step 'Analyze'
    $files = @(Get-ChildItem -LiteralPath $baseDir -Recurse -File -Include '*.ps1' |
            Where-Object { $_.FullName -notmatch '[\\/](node_modules|bin|obj|build|TestResults)[\\/]' })
    $settings = Join-Path $baseDir 'PSScriptAnalyzerSettings.psd1'
    $findings = @($files | ForEach-Object { Invoke-ScriptAnalyzer -Path $_.FullName -Settings $settings })
    foreach ($finding in $findings) {
        Write-Host "  $([System.IO.Path]::GetRelativePath($baseDir, $finding.ScriptPath)):$($finding.Line) $($finding.RuleName): $($finding.Message)"
    }
    if ($findings.Count -gt 0) { Stop-Build "PSScriptAnalyzer: $($findings.Count) finding(s) in $($files.Count) scripts; a warning is an error" }
    Write-Pass "PSScriptAnalyzer: no finding in $($files.Count) scripts"
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
    dotnet publish $clientProject --configuration $configuration --output $publishDir "-p:Version=$version"
    foreach ($file in 'index.html', '404.html', 'staticwebapp.config.json') {
        if (-not (Test-Path -LiteralPath (Join-Path $siteDir $file))) { Stop-Build "The published site has no $file" }
    }
    Write-Pass "the site is published to $([System.IO.Path]::GetRelativePath($baseDir, $siteDir))"
}

function PayloadBudget {
    # What a first visit downloads: every file of the site but the ones a first visit never asks for, each at the
    # size of its brotli file where the publish step wrote one. Of the three ICU data files the runtime loads one,
    # by the browser's language: the largest counts.
    Write-Step 'PayloadBudget'
    $neverAsked = '^(404\.html|staticwebapp\.config\.json|_health/.*)$'
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
    # The site as Azure Static Web Apps gets it: without the compressed copies (the service compresses what it
    # serves and never reads them), with the health files, and with a fingerprint in the name of every file the
    # configuration lets a browser keep for a year.
    Write-Step 'StaticFiles'
    Get-ChildItem -LiteralPath $siteDir -Recurse -File | Where-Object { $_.Extension -in '.br', '.gz' } | Remove-Item -Force

    $config = Get-Content -LiteralPath (Join-Path $siteDir 'staticwebapp.config.json') -Raw | ConvertFrom-Json
    $immutable = @($config.routes | Where-Object { $_.PSObject.Properties['headers'] -and "$($_.headers.'Cache-Control')" -match 'immutable' } | ForEach-Object { $_.route })
    $unmarked = [System.Collections.Generic.List[string]]::new()
    foreach ($route in $immutable) {
        $folder = Join-Path $siteDir ($route -replace '^/', '' -replace '/\*$', '')
        if (-not (Test-Path -LiteralPath $folder)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $folder -Recurse -File) {
            if ($file.Name -cnotmatch '\.[a-z0-9]{10}\.[A-Za-z0-9]+$') { $unmarked.Add([System.IO.Path]::GetRelativePath($siteDir, $file.FullName)) }
        }
    }
    if ($unmarked.Count -gt 0) {
        Stop-Build "Served as immutable without a fingerprint in the name: $($unmarked -join ', ')"
    }
    Write-Pass "every file under $($immutable -join ', ') has a fingerprint in its name"

    $index = Get-Content -LiteralPath (Join-Path $siteDir 'index.html') -Raw
    if ($index -notmatch [regex]::Escape("<title>$title</title>")) { Stop-Build "index.html does not have the title `"$title`"" }

    # The publish step writes the import map into index.html: the fingerprinted name of every script of the runtime.
    # It is an inline script, which the content security policy refuses (script-src 'self'), and without it the
    # runtime does not start. The policy of the published site therefore names this one import map by its SHA-256:
    # no other inline script runs. A browser hashes the text with its line ends as LF.
    $importMap = [regex]::Match($index, '(?s)<script type="importmap">(.*?)</script>')
    if ($importMap.Success -and $importMap.Groups[1].Value.Trim()) {
        $text = $importMap.Groups[1].Value.Replace("`r`n", "`n")
        $hash = [System.Convert]::ToBase64String([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($text)))
        $configPath = Join-Path $siteDir 'staticwebapp.config.json'
        $configText = Get-Content -LiteralPath $configPath -Raw
        $scriptSource = "script-src 'self' 'wasm-unsafe-eval'"
        if (-not $configText.Contains("$scriptSource;")) { Stop-Build "staticwebapp.config.json has no `"$scriptSource;`" to add the import map's hash to" }
        Write-TextFile -Path $configPath -Text $configText.Replace("$scriptSource;", "$scriptSource 'sha256-$hash';")
        Write-Pass "the content security policy allows the import map of index.html by its hash, and no other inline script"
    }
    else {
        Write-Host 'SKIP index.html has no import map: the content security policy stays as written'
    }

    New-Item -ItemType Directory -Path $healthDir -Force | Out-Null
    Write-TextFile -Path (Join-Path $healthDir 'healthcheck.txt') -Text 'Pending'
    Write-TextFile -Path (Join-Path $healthDir 'alive.txt') -Text 'alive'
    Write-TextFile -Path (Join-Path $healthDir 'version.json') -Text (@{ version = $version } | ConvertTo-Json -Compress)
    # A placeholder until BuildFacts: the tests ask /_build before the facts can count them.
    Write-TextFile -Path (Join-Path $healthDir 'build-facts.json') -Text (@{ version = $version } | ConvertTo-Json -Compress)
    Write-Pass 'the health files are written; healthcheck.txt says Pending until every test has passed'
}

function Start-Site {
    # The emulator, once for the integration tests and the full-system tests.
    if ($null -ne $script:emulator) { return }
    $script:emulator = Start-SiteEmulator -SitePath $siteDir -RepositoryRoot $baseDir -LogPath (Join-Path $testResultsDir 'emulator.log')
    $env:ADAMEVE_BASE_URL = $script:emulator.Address
    $env:ADAMEVE_VERSION = $version
    Write-Host "  the emulator serves the site at $($script:emulator.Address)"
}

function Stop-Site {
    if ($null -eq $script:emulator) { return }
    Stop-SiteEmulator -Process $script:emulator.Process
    $script:emulator = $null
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
    # After the last test: the facts count the tests, healthcheck.txt becomes Healthy, and files.json lists every
    # file of the site as it is now, for verify.ps1 to compare a deployment with. All but two: files.json itself, and
    # staticwebapp.config.json, which the service reads and never serves.
    Write-Step 'BuildFacts'
    $facts = @{
        OutputPath      = Join-Path $healthDir 'build-facts.json'
        RepoRoot        = $baseDir
        Version         = $version
        TestResultsPath = $testResultsDir
        RanBy           = $ranBy
    }
    & (Join-Path $baseDir 'scripts' 'Write-BuildFacts.ps1') @facts
    if ($LASTEXITCODE -ne 0) { Stop-Build "Write-BuildFacts.ps1 ended with exit code $LASTEXITCODE" }
    Write-TextFile -Path (Join-Path $healthDir 'healthcheck.txt') -Text 'Healthy'

    $files = @(Get-ChildItem -LiteralPath $siteDir -Recurse -File |
            Where-Object { $_.FullName -notin (Join-Path $healthDir 'files.json'), (Join-Path $siteDir 'staticwebapp.config.json') } |
            Sort-Object -Property FullName |
            ForEach-Object {
                [ordered]@{
                    path   = [System.IO.Path]::GetRelativePath($siteDir, $_.FullName).Replace('\', '/')
                    size   = $_.Length
                    sha256 = Get-FileSha256 -Path $_.FullName
                }
            })
    Write-TextFile -Path (Join-Path $healthDir 'files.json') -Text ((@{ version = $version; files = $files } | ConvertTo-Json -Depth 4) + "`n")
    Write-Pass "build-facts.json written, healthcheck.txt says Healthy, files.json lists $($files.Count) files"
}

function DeployPackage {
    # The whole package the release sends to Octopus: the two scripts, their Bicep file, the site and its version.
    Write-Step 'DeployPackage'
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    foreach ($file in 'deploy.ps1', 'verify.ps1', 'main.bicep') {
        Copy-Item -LiteralPath (Join-Path $baseDir 'deploy' $file) -Destination $packageDir
    }
    Write-TextFile -Path (Join-Path $packageDir 'version.txt') -Text $version
    $zip = Join-Path $packageDir 'site.zip'
    [System.IO.Compression.ZipFile]::CreateFromDirectory($siteDir, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $megabytes = [Math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 2)
    Write-Pass "build/deploy-package: deploy.ps1, verify.ps1, main.bicep, version.txt and site.zip ($megabytes MB)"
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
    DeployPackage
    Write-Host "PASS the private build of $version, in $([int]([datetime]::UtcNow - $started).TotalSeconds) seconds"
}
