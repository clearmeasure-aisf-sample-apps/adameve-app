#Requires -Version 7.4

<#
.SYNOPSIS
    Says whether an environment runs a version of the game and answers: exit code 0 when it does.

.DESCRIPTION
    The system's steps "Verify deployable" and "Verify revert" start this script from the release's package (hosting
    "own" of the demo-environment kit), in a session of the Azure CLI signed in as the tier's deploy identity. It
    changes nothing.

    1. The site's address, read from Azure (swa-adameve-<environment>-web, defaultHostname).
    2. /_version, asked until it answers -Version, for up to 5 minutes: a new deployment can take that long to serve.
    3. /_healthcheck (200, "Healthy") and /alive (200); every origin may read them, /_version and /_build
       (Access-Control-Allow-Origin: *); / has the title of the game.
    4. Every file of the deployment: /_health/files.json lists each one with its size and SHA-256 (all but itself
       and staticwebapp.config.json, which the service reads and never serves), and each is fetched and compared. The static health file cannot prove that every file arrived intact; this does.
    5. The first load as Azure sends it: the files a first visit asks for, fetched again with Accept-Encoding: br,
       the bytes of the answers added up. Above 3.0 MB the step fails. Of the three ICU data files the largest counts.
    6. The nodes file, when the context names one: the site as the one node of the environment, with its paths.

    Nothing is written to standard error: a deployment takes an error line for a failure.

.PARAMETER Environment
    The environment's name: tdd or prod.

.PARAMETER Version
    The version the environment is to run.

.PARAMETER Context
    The JSON file the system writes: resourceGroup (the tier's) and, optionally, nodesFile: the absolute path of a
    file that does not exist yet, which this script writes when the environment runs the version.

.EXAMPLE
    pwsh -NoProfile -File verify.ps1 -Environment tdd -Version 1.0.42 -Context context.json
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
$env:AZURE_CORE_DISABLE_PROGRESS_BAR = 'true'

$title = 'Adam and woman in the garden of Eden'
$payloadBudgetBytes = 3.0 * 1024 * 1024
$region = 'centralus'
$failures = 0

function Stop-Step {
    # "FAIL text" on standard output, then exit code 1: the step fails without a line on standard error.
    param([Parameter(Mandatory)] [string] $Text)
    Write-Host "FAIL $Text"
    exit 1
}

function Test-That {
    # One check: PASS or FAIL with its name; a failed one is counted and the script goes on to the next.
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [bool] $Condition, [string] $Detail = '')
    if ($Condition) { Write-Host "PASS $Name" }
    else {
        Write-Host "FAIL $Name$(if ($Detail) { ": $Detail" })"
        $script:failures++
    }
}

$client = [System.Net.Http.HttpClient]::new([System.Net.Http.HttpClientHandler]@{ AutomaticDecompression = [System.Net.DecompressionMethods]::None })
$client.Timeout = [timespan]::FromSeconds(60)

function Get-Answer {
    # One GET: status, headers, the bytes as they were sent, and the error when no answer came.
    param([Parameter(Mandatory)] [string] $Address, [string] $AcceptEncoding = '')
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $Address)
    if ($AcceptEncoding) { $request.Headers.TryAddWithoutValidation('Accept-Encoding', $AcceptEncoding) | Out-Null }
    try {
        $response = $client.Send($request)
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $origin = [System.Collections.Generic.IEnumerable[string]] $null
        $encoding = @($response.Content.Headers.ContentEncoding) -join ','
        return @{
            Status   = [int] $response.StatusCode
            Bytes    = $bytes
            Text     = [System.Text.Encoding]::UTF8.GetString($bytes)
            Origin   = if ($response.Headers.TryGetValues('Access-Control-Allow-Origin', [ref] $origin)) { @($origin) -join ',' } else { '' }
            Encoding = $encoding
            Error    = ''
        }
    }
    catch [System.Net.Http.HttpRequestException], [System.Threading.Tasks.TaskCanceledException] {
        return @{ Status = 0; Bytes = [byte[]]::new(0); Text = ''; Origin = ''; Encoding = ''; Error = $_.Exception.Message }
    }
    finally {
        $request.Dispose()
    }
}

Write-Host '==> Context'
if (-not (Test-Path -LiteralPath $Context -PathType Leaf)) { Stop-Step "The context file $Context does not exist." }
$facts = Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json -AsHashtable
if (-not $facts.ContainsKey('resourceGroup') -or -not "$($facts.resourceGroup)".Trim()) { Stop-Step 'The context has no resourceGroup.' }
$resourceGroup = [string] $facts.resourceGroup
$nodesFile = if ($facts.ContainsKey('nodesFile')) { [string] $facts.nodesFile } else { '' }
$site = "swa-adameve-$Environment-web"

$PSNativeCommandUseErrorActionPreference = $false
$hostName = ([string] (az staticwebapp show --name $site --resource-group $resourceGroup --query defaultHostname --only-show-errors --output tsv 2>$null)).Trim()
$PSNativeCommandUseErrorActionPreference = $true
if (-not $hostName) { Stop-Step "Azure has no static web app $site in $resourceGroup, or the deploy identity may not read it." }
$address = "https://$hostName"
Write-Host "PASS $site answers at $address"

Write-Host "==> Version $Version"
$deadline = [datetime]::UtcNow.AddMinutes(5)
$served = ''
do {
    $answer = Get-Answer -Address "$address/_version"
    $served = ''
    if ($answer.Status -eq 200) {
        try { $served = [string] ($answer.Text | ConvertFrom-Json).version } catch { $served = '' }
    }
    if ($served -eq $Version) { break }
    Start-Sleep -Seconds 10
} while ([datetime]::UtcNow -lt $deadline)
if ($served -ne $Version) {
    Stop-Step "/_version did not answer $Version within 5 minutes: status $($answer.Status), version `"$served`"$(if ($answer.Error) { ", $($answer.Error)" })."
}
Write-Host "PASS /_version answers $Version"

Write-Host '==> Health and the paths every origin may read'
$health = Get-Answer -Address "$address/_healthcheck"
Test-That '/_healthcheck answers 200 and Healthy' ($health.Status -eq 200 -and $health.Text.Trim() -eq 'Healthy') "status $($health.Status), `"$($health.Text.Trim())`""
$alive = Get-Answer -Address "$address/alive"
Test-That '/alive answers 200' ($alive.Status -eq 200) "status $($alive.Status)"
$build = Get-Answer -Address "$address/_build"
Test-That '/_build answers 200' ($build.Status -eq 200) "status $($build.Status)"
foreach ($path in @{ Path = '/_healthcheck'; Answer = $health }, @{ Path = '/alive'; Answer = $alive }, @{ Path = '/_version'; Answer = $answer }, @{ Path = '/_build'; Answer = $build }) {
    Test-That "$($path.Path) has Access-Control-Allow-Origin: *" ($path.Answer.Origin -eq '*') "`"$($path.Answer.Origin)`""
}
$front = Get-Answer -Address "$address/"
Test-That "/ has the title `"$title`"" ($front.Status -eq 200 -and $front.Text.Contains("<title>$title</title>")) "status $($front.Status)"

Write-Host '==> Every file of the deployment'
$list = Get-Answer -Address "$address/_health/files.json"
$files = @()
if ($list.Status -eq 200) {
    try { $files = @(($list.Text | ConvertFrom-Json).files) } catch { $files = @() }
}
if ($files.Count -eq 0) { Stop-Step "/_health/files.json lists no file: status $($list.Status)." }
$different = [System.Collections.Generic.List[string]]::new()
$sha256 = [System.Security.Cryptography.SHA256]::Create()
foreach ($file in $files) {
    $got = Get-Answer -Address "$address/$($file.path)"
    $hash = [System.Convert]::ToHexString($sha256.ComputeHash($got.Bytes)).ToLowerInvariant()
    if ($got.Status -ne 200 -or $got.Bytes.Length -ne [long] $file.size -or $hash -ne [string] $file.sha256) {
        $different.Add("$($file.path) (status $($got.Status), $($got.Bytes.Length) of $($file.size) bytes)")
    }
}
Test-That "$($files.Count) files are served as the build made them (size and SHA-256)" ($different.Count -eq 0) ($different -join '; ')

Write-Host '==> The first load, as Azure sends it'
$neverAsked = '^(404\.html|staticwebapp\.config\.json|_health/.*)$'
$total = 0L
$largestIcu = 0L
$compressed = 0
$count = 0
foreach ($file in $files | Where-Object { $_.path -notmatch $neverAsked }) {
    $got = Get-Answer -Address "$address/$($file.path)" -AcceptEncoding 'br'
    if ($got.Status -ne 200) { Test-That "$($file.path) answers with Accept-Encoding: br" $false "status $($got.Status)"; continue }
    if ($got.Encoding) { $compressed++ }
    if ([System.IO.Path]::GetFileName($file.path) -match '^icudt.*\.dat$') {
        if ($got.Bytes.Length -gt $largestIcu) { $largestIcu = $got.Bytes.Length }
        continue
    }
    $total += $got.Bytes.Length
    $count++
}
$total += $largestIcu
$megabytes = [Math]::Round($total / 1MB, 2)
Write-Host "  $compressed of the answers were compressed by the service"
Test-That "the first load is $megabytes MB ($count files and one ICU data file), within 3.0 MB" ($total -le $payloadBudgetBytes)

if ($failures -gt 0) { Stop-Step "$failures check(s) failed: $Environment does not run version $Version as built." }

Write-Host '==> Nodes'
if ($nodesFile) {
    $nodes = [ordered]@{
        healthPath  = '/_healthcheck'
        alivePath   = '/alive'
        versionPath = '/_version'
        nodes       = @([ordered]@{ name = $site; region = $region; role = 'primary'; url = $address })
    }
    [System.IO.File]::WriteAllText($nodesFile, (($nodes | ConvertTo-Json -Depth 4) + "`n"), [System.Text.UTF8Encoding]::new($false))
    Write-Host "PASS the nodes file names $site at $address"
}
else {
    Write-Host 'SKIP the context names no nodes file'
}
Write-Host "PASS $Environment runs version $Version"
