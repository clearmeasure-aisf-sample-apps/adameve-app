#Requires -Version 7.4

<#
.SYNOPSIS
    Makes an environment run a version of the game: one container app, running the image of that version.

.DESCRIPTION
    The system's steps "Update deployable" and "Revert deployable" start this script from the release's package
    (hosting "own" of the demo-environment kit), in a session of the Azure CLI signed in as the tier's deploy
    identity. The package holds this script, verify.ps1, settings.json and infra/main.bicep. It does not hold what
    it deploys: the image of every released version stays in the system's registry as
    <registry>/<system>/web:<version>, its tag locked, so this script deploys any released version, the one before
    included ("Revert deployable").

    1. The context and settings.json: the system, the tier's resource group, the registry, the deploy identity; the
       name of the system's Container Apps environment and of the identity that pulls the image; the scale of the
       environment asked for.
    2. The system's Container Apps express environment (cae-<system> in rg-<system>-apps). The system's seed creates
       it; this script reads it and creates nothing there. Missing, not ready or not an express environment: the
       script says so and exits 1 before anything is applied.
    3. The deployment stack stack-<system>-<environment>-web from infra/main.bicep, in the tier's resource group:
       the container app ca-<system>-<environment>-web, with the image <registry>/<system>/web:<version> and the
       pull identity in the same request (an express app keeps no registry setting). The stack denies writing and
       deleting to everyone but the deploy identity, and deletes what it no longer manages. A failure that may be
       the platform's own is tried once more after a pause; one that no second attempt changes is not.
    4. The app as Azure shows it: provisioned, with the image of the version. A version whose image is not in the
       registry cannot be provisioned: the script prints what Azure says and exits 1.

    Whether the game answers is verify.ps1's question, not this script's.

    Nothing is written to standard error: a deployment takes an error line for a failure. What the Azure CLI writes
    there is kept, and written to standard output when a step fails.

.PARAMETER Environment
    The environment's name: tdd or prod.

.PARAMETER Version
    The version the environment is to run: the tag of the image.

.PARAMETER Context
    The JSON file the system writes: system, deployable, environment, version, resourceGroup (the tier's),
    registryServer, deployPrincipalId and systemRepository.

.PARAMETER RetryPauseSeconds
    How long to wait before the second attempt at the stack. The system passes none.

.EXAMPLE
    pwsh -NoProfile -File deploy.ps1 -Environment tdd -Version 1.0.42 -Context context.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[a-z][a-z0-9]{1,15}$')] [string] $Environment,
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [Parameter(Mandatory)] [string] $Context,
    [ValidateRange(0, 600)] [int] $RetryPauseSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$ProgressPreference = 'SilentlyContinue'

# The Azure CLI writes progress to standard error: off.
$env:AZURE_CORE_DISABLE_PROGRESS_BAR = 'true'

$apiVersion = '2026-07-01'

function Stop-Step {
    # "FAIL text" on standard output, then exit code 1: the step fails without a line on standard error.
    param([Parameter(Mandatory)] [string] $Text)
    Write-Host "FAIL $Text"
    exit 1
}

function Invoke-Az {
    # The Azure CLI, with what it writes to standard error kept apart: its notices, and its error when it fails.
    # Nothing of it reaches this script's standard error.
    param([Parameter(Mandatory)] [string[]] $Arguments)
    $saidFile = Join-Path ([System.IO.Path]::GetTempPath()) "az-$([Guid]::NewGuid().ToString('N')).log"
    $PSNativeCommandUseErrorActionPreference = $false
    $output = az @Arguments --only-show-errors 2>$saidFile
    $exitCode = $LASTEXITCODE
    $said = @(if (Test-Path -LiteralPath $saidFile) { Get-Content -LiteralPath $saidFile })
    Remove-Item -LiteralPath $saidFile -Force -ErrorAction SilentlyContinue
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output   = (@($output) | ForEach-Object { [string] $_ }) -join "`n"
        Said     = [string[]] @($said | ForEach-Object { ([string] $_).TrimEnd() } | Where-Object { $_ })
    }
}

function Write-Said {
    # What the Azure CLI said, as lines of standard output.
    param([string[]] $Said = @(), [string] $Indent = '  ')
    foreach ($line in @($Said)) { Write-Host "$Indent$line" }
}

function Read-Resource {
    # One resource by its ID, as a table; null when Azure has none or the identity may not read it.
    param([Parameter(Mandatory)] [string] $Id)
    $read = Invoke-Az -Arguments @('rest', '--method', 'get', '--url', "https://management.azure.com${Id}?api-version=$apiVersion", '--output', 'json')
    if ($read.ExitCode -ne 0 -or -not $read.Output) { return $null }
    return $read.Output | ConvertFrom-Json -AsHashtable
}

function Get-Property {
    # A property of a resource by its path, or an empty text.
    param($Resource, [Parameter(Mandatory)] [string[]] $Path)
    $value = $Resource
    foreach ($name in $Path) {
        if ($value -isnot [System.Collections.IDictionary] -or -not $value.Contains($name)) { return '' }
        $value = $value[$name]
    }
    return $value
}

# The errors of Azure Resource Manager that no second attempt changes: the template or the request is wrong, or Azure
# does not allow it. Every other failure may be the platform's own, and is tried once more.
$errorsNoAttemptChanges = @(
    'InvalidTemplate', 'InvalidTemplateDeployment', 'InvalidDeploymentParameterValue', 'InvalidRequestContent',
    'RequestDisallowedByPolicy', 'RequestDisallowedByAzure', 'LocationNotAvailableForResourceType',
    'NoRegisteredProviderFound', 'MissingSubscriptionRegistration', 'AuthorizationFailed', 'LinkedAuthorizationFailed'
)

function Find-HopelessError {
    # The first such error in what the CLI said; an empty text when there is none.
    param([string[]] $Said = @())
    $text = @($Said) -join "`n"
    foreach ($code in $errorsNoAttemptChanges) {
        # The code as a word of its own: "InvalidTemplate" is not found in "InvalidTemplateDeployment".
        if ($text -cmatch "(?<![A-Za-z])$code(?![A-Za-z])") { return $code }
    }
    # The image is not in the registry, or the Bicep file does not compile.
    if ($text -cmatch 'MANIFEST_UNKNOWN|Error BCP\d+') { return $Matches[0] }
    return ''
}

Write-Host '==> Context'
if (-not (Get-Command az -ErrorAction SilentlyContinue)) { Stop-Step 'The worker has no Azure CLI.' }
if (-not (Test-Path -LiteralPath $Context -PathType Leaf)) { Stop-Step "The context file $Context does not exist." }
$facts = Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in 'system', 'resourceGroup', 'registryServer', 'deployPrincipalId') {
    if (-not $facts.ContainsKey($key) -or -not "$($facts[$key])".Trim()) { Stop-Step "The context has no $key." }
}
$system = [string] $facts.system
$resourceGroup = [string] $facts.resourceGroup
$registryServer = [string] $facts.registryServer
$deployPrincipalId = [string] $facts.deployPrincipalId

$settingsFile = Join-Path $PSScriptRoot 'settings.json'
$template = Join-Path $PSScriptRoot 'infra' 'main.bicep'
foreach ($file in $settingsFile, $template) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { Stop-Step "The package has no $([System.IO.Path]::GetRelativePath($PSScriptRoot, $file))." }
}
$settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json -AsHashtable
if (-not $settings.environments.ContainsKey($Environment)) { Stop-Step "settings.json says nothing about the environment '$Environment'." }
$place = $settings.environments[$Environment]
$named = { param([string] $Text) $Text.Replace('{system}', $system).Replace('{environment}', $Environment) }

$account = Invoke-Az -Arguments @('account', 'show', '--query', 'id', '--output', 'tsv')
$subscription = $account.Output.Trim()
if ($account.ExitCode -ne 0 -or -not $subscription) {
    Write-Said -Said $account.Said
    Stop-Step 'The Azure CLI is not signed in: the system starts this script in a session of the deploy identity.'
}
$group = "/subscriptions/$subscription/resourceGroups/$resourceGroup"
$pullIdentityId = "$group/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$(& $named ([string] $settings.pullIdentity))"
$environmentGroup = & $named ([string] $settings.appEnvironment.resourceGroup)
$environmentName = & $named ([string] $settings.appEnvironment.name)
$environmentId = "/subscriptions/$subscription/resourceGroups/$environmentGroup/providers/Microsoft.App/managedEnvironments/$environmentName"
$stack = "stack-$system-$Environment-web"
$app = "ca-$system-$Environment-web"
$appId = "$group/providers/Microsoft.App/containerApps/$app"
$image = "$registryServer/$system/web:$Version"
Write-Host "PASS environment $Environment, version $Version, resource group $resourceGroup, image $image"

Write-Host "==> Container Apps environment $environmentName"
$managedEnvironment = Read-Resource -Id $environmentId
if ($null -eq $managedEnvironment) {
    Stop-Step "Azure has no Container Apps environment $environmentName in $environmentGroup, or the deploy identity may not read it. The system's seed creates it and lets the deploy identities of both tiers use it; nothing was deployed."
}
$state = [string] (Get-Property -Resource $managedEnvironment -Path 'properties', 'provisioningState')
$mode = [string] (Get-Property -Resource $managedEnvironment -Path 'properties', 'environmentMode')
# Azure writes a region as "Central US" or as "centralus": one form, for the template and for the nodes file.
$location = ([string] (Get-Property -Resource $managedEnvironment -Path 'location')).Replace(' ', '').ToLowerInvariant()
if ($state -ne 'Succeeded') { Stop-Step "$environmentName is not ready (provisioning state '$state'); nothing was deployed." }
if ($mode -ne 'Express') { Stop-Step "$environmentName is not an express environment (mode '$mode'): infra/main.bicep is written for one; nothing was deployed." }
if (-not $location) { Stop-Step "$environmentName does not say which region it is in; nothing was deployed." }
Write-Host "PASS $environmentName in $environmentGroup is an express environment in $location, ready"

Write-Host "==> Deployment stack $stack"
$parametersFile = Join-Path ([System.IO.Path]::GetTempPath()) "parameters-$stack-$([Guid]::NewGuid().ToString('N')).json"
$parameters = @{
    '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters     = @{
        system               = @{ value = $system }
        environmentName      = @{ value = $Environment }
        version              = @{ value = $Version }
        registryServer       = @{ value = $registryServer }
        pullIdentityId       = @{ value = $pullIdentityId }
        managedEnvironmentId = @{ value = $environmentId }
        location             = @{ value = $location }
        port                 = @{ value = [int] $settings.port }
        minReplicas          = @{ value = [int] $place.minReplicas }
        maxReplicas          = @{ value = [int] $place.maxReplicas }
    }
}
[System.IO.File]::WriteAllText($parametersFile, ($parameters | ConvertTo-Json -Depth 6), [System.Text.UTF8Encoding]::new($false))
$apply = @(
    'stack', 'group', 'create',
    '--name', $stack,
    '--resource-group', $resourceGroup,
    '--template-file', $template,
    '--parameters', "@$parametersFile",
    '--action-on-unmanage', 'deleteResources',
    '--deny-settings-mode', 'denyWriteAndDelete',
    '--deny-settings-excluded-principals', $deployPrincipalId,
    '--yes',
    '--output', 'none'
)
try {
    $attempts = @(Invoke-Az -Arguments $apply)
    $hopeless = ''
    if ($attempts[0].ExitCode -ne 0) {
        $hopeless = Find-HopelessError -Said $attempts[0].Said
        if (-not $hopeless) {
            Write-Host "  Applying $stack failed (exit code $($attempts[0].ExitCode)). Azure said:"
            Write-Said -Said $attempts[0].Said -Indent '    '
            Write-Host "  Trying once more in $RetryPauseSeconds seconds: the platform fails by itself at times."
            Start-Sleep -Seconds $RetryPauseSeconds
            $attempts += @(Invoke-Az -Arguments $apply)
        }
    }
}
finally {
    Remove-Item -LiteralPath $parametersFile -Force -ErrorAction SilentlyContinue
}

function Write-DeploymentError {
    # An express environment says why an app could not start in the app's deploymentErrors.
    $shown = Read-Resource -Id $appId
    $errors = Get-Property -Resource $shown -Path 'properties', 'deploymentErrors'
    if ($errors) { Write-Host "  $app reports: $($errors | ConvertTo-Json -Depth 6 -Compress)" }
}

if ($attempts[-1].ExitCode -ne 0) {
    if ($attempts.Count -eq 1) {
        Write-Host "  Not tried again: no second attempt changes $hopeless. Azure said:"
    }
    else {
        Write-Host "  The second attempt failed too (exit code $($attempts[-1].ExitCode)). Azure said:"
    }
    Write-Said -Said $attempts[-1].Said -Indent '    '
    Write-DeploymentError
    Stop-Step "$stack was not applied: $Environment does not run version $Version. A version can be deployed while its image $image is in the registry."
}
Write-Host "PASS $stack holds $app$(if ($attempts.Count -gt 1) { ' (at the second attempt)' }), denies writing and deleting to everyone but the deploy identity"

Write-Host "==> Container app $app"
# An express app has one revision and lists no latest ready one: the image the app shows and its provisioning state
# say that the change is through.
$deadline = [datetime]::UtcNow.AddMinutes(5)
do {
    $shown = Read-Resource -Id $appId
    $state = [string] (Get-Property -Resource $shown -Path 'properties', 'provisioningState')
    $containers = @(Get-Property -Resource $shown -Path 'properties', 'template', 'containers')
    $running = if ($containers.Count -gt 0 -and $containers[0] -is [System.Collections.IDictionary]) { [string] $containers[0]['image'] } else { '' }
    if ($state -in 'Succeeded', 'Failed', 'Canceled') { break }
    Start-Sleep -Seconds 10
} while ([datetime]::UtcNow -lt $deadline)
if ($state -ne 'Succeeded' -or $running -ne $image) {
    Write-DeploymentError
    Stop-Step "$app is not provisioned with $image (provisioning state '$state', image '$running')."
}
$address = [string] (Get-Property -Resource $shown -Path 'properties', 'configuration', 'ingress', 'fqdn')
Write-Host "PASS $app is provisioned with $image$(if ($address) { " at https://$address" })"
