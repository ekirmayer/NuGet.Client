<#
.SYNOPSIS
Builds NuGet client solutions and runs unit-tests.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER ReleaseLabel
Release label to use for package and assemblies versioning (zlocal by default)

.PARAMETER BuildNumber
Build number to use for package and assemblies versioning (auto-generated if not provided)

.PARAMETER SkipRestore
Builds without restoring first

.PARAMETER CleanCache
Cleans NuGet packages cache before build

.PARAMETER MSPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER NuGetPFXPath
Path to a code signing certificate for delay-sigining (optional)

.PARAMETER SkipXProj
Skips building the NuGet.Core XProj projects

.PARAMETER SkipVS14
Skips building binaries targeting Visual Studio "14" (released as Visual Studio 2015)

.PARAMETER SkipVS15
Skips building binaries targeting Visual Studio "15"

.PARAMETER SkipSubModules
Skips updating submodules

.PARAMETER SkipTests
Skips building and running unit-tests

.PARAMETER SkipILMerge
Skips creating an ILMerged nuget.exe

.EXAMPLE
To run full clean build, e.g after switching branches:
.\build.ps1 -CleanCache

To troubleshoot build issues:
.\build.ps1 -Verbose -ErrorAction Stop
#>
[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [ValidateSet("debug", "release")]
    [string]$Configuration = 'debug',
    [ValidateSet("release","rtm", "rc", "rc1", "beta", "beta1", "beta2", "final", "xprivate", "zlocal")]
    [string]$ReleaseLabel = 'zlocal',
    [int]$BuildNumber,
    [switch]$SkipRestore,
    [switch]$CleanCache,
    [string]$MSPFXPath,
    [string]$NuGetPFXPath,
    [switch]$SkipXProj,
    [switch]$SkipVS14,
    [switch]$SkipVS15,
    [switch]$SkipSubModules,
    [switch]$SkipTests,
    [switch]$SkipILMerge
)

# For TeamCity - Incase any issue comes in this script fail the build. - Be default TeamCity returns exit code of 0 for all powershell even if it fails
trap {
    if ($env:TEAMCITY_VERSION) {
        Write-Host "##teamcity[buildProblem description='$(Format-TeamCityMessage($_.ToString()))']"
    }

    Write-Host "BUILD FAILED: $_" -ForegroundColor Red
    Write-Host "ERROR DETAILS:" -ForegroundColor Red
    Write-Host $_.Exception -ForegroundColor Red
    Write-Host ("`r`n" * 3)
    exit 1
}

. "$PSScriptRoot\build\common.ps1"

# Adjust version skipping if only one version installed - if VS15 is not installed, no need to specify SkipVS15
$SkipVS14 = $SkipVS14 -or -not $VS14Installed
$SkipVS15 = $SkipVS15 -or -not $VS15Installed

Write-Host ("`r`n" * 3)
Trace-Log ('=' * 60)

$startTime = [DateTime]::UtcNow
if (-not $BuildNumber) {
    $BuildNumber = Get-BuildNumber
}
Trace-Log "Build #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Updating sub-modules' { Update-SubModules } `
    -skip:$SkipSubModules `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning artifacts' { Clear-Artifacts } `
    -skip:$SkipXProj `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning nupkgs' { Clear-Nupkgs } `
    -skip:$SkipXProj `
    -ev +BuildErrors

Invoke-BuildStep 'Installing NuGet.exe' { Install-NuGet } `
    -ev +BuildErrors

Invoke-BuildStep 'Cleaning package cache' { Clear-PackageCache } `
    -skip:(-not $CleanCache) `
    -ev +BuildErrors

Invoke-BuildStep 'Installing dotnet CLI' { Install-DotnetCLI } `
    -ev +BuildErrors

# Restoring tools required for build
Invoke-BuildStep 'Restoring solution packages' { Restore-SolutionPackages } `
    -skip:$SkipRestore `
    -ev +BuildErrors

Invoke-BuildStep 'Enabling delayed signing' {
        param($MSPFXPath, $NuGetPFXPath)
        Enable-DelaySigning $MSPFXPath $NuGetPFXPath
    } `
    -args $MSPFXPath, $NuGetPFXPath `
    -skip:((-not $MSPFXPath) -and (-not $NuGetPFXPath)) `
    -ev +BuildErrors

Invoke-BuildStep 'Building NuGet.Core projects' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-CoreProjects $Configuration $ReleaseLabel $BuildNumber -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipXProj `
    -ev +BuildErrors

## Building the VS15 Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects - VS15 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 15 -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipVS15 `
    -ev +BuildErrors

## Building the VS14 Tooling solution
Invoke-BuildStep 'Building NuGet.Clients projects - VS14 Toolset' {
        param($Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore)
        Build-ClientsProjects $Configuration $ReleaseLabel $BuildNumber -ToolsetVersion 14 -SkipRestore:$SkipRestore
    } `
    -args $Configuration, $ReleaseLabel, $BuildNumber, $SkipRestore `
    -skip:$SkipVS14 `
    -ev +BuildErrors

## ILMerge the VS14 exe only
Invoke-BuildStep 'Merging NuGet.exe' {
        param($Configuration, $MSPFXPath)
        Invoke-ILMerge $Configuration 14 $MSPFXPath
    } `
    -args $Configuration, $MSPFXPath `
    -skip:($SkipILMerge -or $SkipVS14) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Build #$BuildNumber ended at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)
