#Requires -Version 7.4

<#
.SYNOPSIS
    Makes an environment run a version of the game: applies the application's own infrastructure and uploads the site.

.DESCRIPTION
    The system's steps "Update deployable" and "Revert deployable" start this script from the release's package
    (hosting "own" of the demo-environment kit), in a session of the Azure CLI signed in as the tier's deploy
    identity. The package holds this script, verify.ps1, main.bicep, site.zip (the published site) and version.txt
    (the version site.zip is).

    1. The deployment stack stack-adameve-<environment>-web from main.bicep, in the tier's resource group: one Azure
       Static Web App on the Free plan, swa-adameve-<environment>-web. The stack denies writing and deleting to
       everyone but the deploy identity, and deletes what it no longer manages.
    2. The content: site.zip when -Version is the version of this package. Any other version (the kit's "Revert
       deployable" asks for the version before) cannot be deployed by this package: the script says so and exits 1.
       That is the kit's documented limit for a package that carries what it deploys, until the archive of released
       sites exists (slice S3 of docs/design.md).
    3. The site's deployment token, read from Azure for this one upload. It is held in the process environment only:
       never written to a file, a log or a command line, and removed when the upload ends.
    4. The upload, with the Static Web Apps CLI at a fixed version.

    Nothing is written to standard error: a deployment takes an error line for a failure. What the tools write there
    is captured and written to standard output, and their exit codes decide.

.PARAMETER Environment
    The environment's name: tdd or prod.

.PARAMETER Version
    The version the environment is to run.

.PARAMETER Context
    The JSON file the system writes: system, deployable, environment, version, resourceGroup (the tier's),
    registryServer, deployPrincipalId and systemRepository.

.EXAMPLE
    pwsh -NoProfile -File deploy.ps1 -Environment tdd -Version 1.0.42 -Context context.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[a-z][a-z0-9]{1,15}$')] [string] $Environment,
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [Parameter(Mandatory)] [string] $Context
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$ProgressPreference = 'SilentlyContinue'

# The Azure CLI writes progress and, when it installs Bicep, a warning to standard error: both off.
$env:AZURE_CORE_DISABLE_PROGRESS_BAR = 'true'
$env:AZURE_BICEP_USE_BINARY_FROM_PATH = 'false'

# The Static Web Apps CLI, at a fixed version: a promotion deploys with the tool the earlier environments used.
$swaCliVersion = '2.0.10'
$swaCliNodeVersion = 18

function Stop-Step {
    # "FAIL text" on standard output, then exit code 1: the step fails without a line on standard error.
    param([Parameter(Mandatory)] [string] $Text)
    Write-Host "FAIL $Text"
    exit 1
}

function Invoke-Tool {
    # A native command whose every line, of both streams, becomes a line of standard output. Returns the exit code.
    param([Parameter(Mandatory)] [scriptblock] $Command, [string] $Secret = '')
    $PSNativeCommandUseErrorActionPreference = $false
    $output = @(& $Command 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    foreach ($line in $output) {
        # Colour codes out, and the secret too, should a tool ever echo it.
        $text = ($line -replace '\x1b\[[0-9;?]*[ -/]*[@-~]', '').TrimEnd()
        if ($Secret) { $text = $text.Replace($Secret, '***') }
        if ($text) { Write-Host "  $text" }
    }
    return $code
}

Write-Host '==> Context'
if (-not (Test-Path -LiteralPath $Context -PathType Leaf)) { Stop-Step "The context file $Context does not exist." }
$facts = Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in 'resourceGroup', 'deployPrincipalId') {
    if (-not $facts.ContainsKey($key) -or -not "$($facts[$key])".Trim()) { Stop-Step "The context has no $key." }
}
$resourceGroup = [string] $facts.resourceGroup
$deployPrincipalId = [string] $facts.deployPrincipalId
$stack = "stack-adameve-$Environment-web"
$site = "swa-adameve-$Environment-web"
$packagedVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.txt') -Raw).Trim()
Write-Host "PASS environment $Environment, version $Version, resource group $resourceGroup, package $packagedVersion"

Write-Host '==> Content'
if ($Version -ne $packagedVersion) {
    Stop-Step "This package holds the site of version $packagedVersion and cannot deploy version $Version. Deploy the release $Version itself. An archive of released sites, from which any version can be put back, is slice S3 of the design."
}
$siteZip = Join-Path $PSScriptRoot 'site.zip'
if (-not (Test-Path -LiteralPath $siteZip -PathType Leaf)) { Stop-Step 'The package has no site.zip.' }
Write-Host "PASS site.zip is the site of version $Version"

Write-Host '==> Tools'
foreach ($tool in 'az', 'node', 'npx') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { Stop-Step "The worker has no $tool. The Static Web Apps CLI needs Node.js $swaCliNodeVersion or later with npx; the infrastructure needs the Azure CLI." }
}
$nodeVersion = ([string] (node --version)).Trim()
if ([int] ($nodeVersion -replace '^v(\d+).*$', '$1') -lt $swaCliNodeVersion) {
    Stop-Step "The worker has Node.js $nodeVersion; the Static Web Apps CLI $swaCliVersion needs $swaCliNodeVersion or later."
}
Write-Host "PASS the Azure CLI, Node.js $nodeVersion and npx"

Write-Host "==> Deployment stack $stack"
$code = Invoke-Tool {
    az stack group create --name $stack --resource-group $resourceGroup `
        --template-file (Join-Path $PSScriptRoot 'main.bicep') --parameters "environmentName=$Environment" `
        --deny-settings-mode denyWriteAndDelete --deny-settings-excluded-principals $deployPrincipalId `
        --action-on-unmanage deleteResources --yes --only-show-errors --output none
}
if ($code -ne 0) { Stop-Step "az stack group create ended with exit code $code; its output is above." }
Write-Host "PASS $stack holds $site (Free), denies writing and deleting to everyone but the deploy identity"

Write-Host '==> Deployment token'
$token = ([string] (az staticwebapp secrets list --name $site --resource-group $resourceGroup --query properties.apiKey --only-show-errors --output tsv)).Trim()
if (-not $token) { Stop-Step "Azure returned no deployment token for $site." }
Write-Host 'PASS read for this upload; it is not stored'

Write-Host "==> Upload with the Static Web Apps CLI $swaCliVersion"
# The CLI takes its working directory for the "app location", which it searches for an api folder, workflow files and
# a configuration file: it runs in a folder of its own that holds nothing but the site.
$env:NO_COLOR = '1'
$env:NPM_CONFIG_UPDATE_NOTIFIER = 'false'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) "adameve-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $stage | Out-Null
Expand-Archive -LiteralPath $siteZip -DestinationPath (Join-Path $stage 'site')
Push-Location -LiteralPath $stage
try {
    $env:SWA_CLI_DEPLOYMENT_TOKEN = $token
    $code = Invoke-Tool -Secret $token { npx --yes "@azure/static-web-apps-cli@$swaCliVersion" deploy ./site --env production }
}
finally {
    Remove-Item -LiteralPath Env:SWA_CLI_DEPLOYMENT_TOKEN -ErrorAction SilentlyContinue
    $token = $null
    Pop-Location
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}
if ($code -ne 0) { Stop-Step "The Static Web Apps CLI ended with exit code $code while uploading version $Version to $site; its output is above." }
Write-Host "PASS version $Version is uploaded to $site"
