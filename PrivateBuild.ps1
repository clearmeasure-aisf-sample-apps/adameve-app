#Requires -Version 7.4

<#
.SYNOPSIS
    The private build: the one command that proves a change, at a desk and in the integration build.

.DESCRIPTION
    Compiles the solution with warnings as errors, runs the unit tests, publishes the host with the client as its
    web root, checks the payload budget, starts the published host as a process and runs the integration tests and
    the full-system tests (Playwright) against it, writes the build facts, makes the container image from the very
    folder the tests asked (build/container-image/container-image.tar.gz) and the package build/deploy-package. The
    workflow Build runs nothing but this command with -CI.

    Needs the .NET 10 SDK and PowerShell 7.4 or later with the module PSScriptAnalyzer. No Docker: the SDK builds
    the image. Where a Docker daemon answers, the build also loads the image and runs it once.

.PARAMETER Version
    The version of the build (MAJOR.MINOR.run_number). Default: 1.0.0.

.PARAMETER CI
    The integration build runs it: the browsers are installed with their system packages, and the build facts say
    that the integration build made this artifact.

.EXAMPLE
    pwsh ./PrivateBuild.ps1
    The private build at a desk.

.EXAMPLE
    pwsh ./PrivateBuild.ps1 -CI -Version 1.0.42
    What the workflow Build runs.
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [switch] $CI
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. (Join-Path $PSScriptRoot 'build.ps1')

Build -Version $Version -CI:$CI
