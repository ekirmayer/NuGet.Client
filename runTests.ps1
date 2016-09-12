<#
.SYNOPSIS
Build and run unit-tests and functional tests.

.PARAMETER Configuration
Build configuration (debug by default)

.PARAMETER SkipXProj
Skips running NuGet.Core.Tests and NuGet.Core.FuncTests

.PARAMETER SkipVS14
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS14 toolset

.PARAMETER SkipVS15
Skips running NuGet.Clients.Tests and NuGet.Clients.FuncTests with VS15 toolset

.PARAMETER SkipUnitTests
Skips running NuGet.Core.Tests and NuGet.Clients.Tests

.PARAMETER SkipFuncTests
Skips running NuGet.Core.FuncTests and NuGet.Clients.FuncTests

.EXAMPLE
Running full test suite:
.\runTests.ps1 -Verbose

Running functional tests only:
.\runTests.ps1 -sut

Running core unit tests only:
.\runTests.ps1 -sft -s14 -s15
#>
[CmdletBinding()]
param (
    [ValidateSet("debug", "release")]
    [Alias('c')]
    [string]$Configuration = 'debug',
    [Alias('sx')]
    [switch]$SkipXProj,
    [Alias('s14')]
    [switch]$SkipVS14,
    [Alias('s15')]
    [switch]$SkipVS15,
    [Alias('sut')]
    [switch]$SkipUnitTests,
    [Alias('sft')]
    [switch]$SkipFuncTests
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
Trace-Log "Test suite run #$BuildNumber started at $startTime"

$BuildErrors = @()

Invoke-BuildStep 'Running NuGet.Core unit-tests' {
        param($Configuration)
        Test-CoreProjects $Configuration
    } `
    -args $Configuration `
    -skip:($SkipXProj -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Core functional tests' {
        param($Configuration)
        Test-FuncCoreProjects $Configuration
    } `
    -args $Configuration `
    -skip:($SkipXProj -or $SkipFuncTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients unit-tests - VS14 Toolset' {
        param($Configuration)
        Test-ClientsProjects $Configuration -ToolsetVersion 14
    } `
    -args $Configuration `
    -skip:($SkipVS14 -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS14 Toolset' {
        param($Configuration)
        Test-FuncClientsProjects $Configuration -ToolsetVersion 14
    } `
    -args $Configuration `
    -skip:($SkipVS14 -or $SkipFuncTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients tests - VS15 Toolset' {
        param($Configuration)
        # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
        Test-ClientsProjects $Configuration -ToolsetVersion 15 -SkipProjects 'NuGet.CommandLine.Test'
    } `
    -args $Configuration `
    -skip:($SkipVS15 -or $SkipUnitTests) `
    -ev +BuildErrors

Invoke-BuildStep 'Running NuGet.Clients functional tests - VS15 Toolset' {
        param($Configuration)
        # We don't run command line tests on VS15 as we don't build a nuget.exe for this version
        Test-FuncClientsProjects $Configuration -ToolsetVersion 15 -SkipProjects 'NuGet.CommandLine.FuncTest'
    } `
    -args $Configuration `
    -skip:($SkipVS15 -or $SkipFuncTests) `
    -ev +BuildErrors

Trace-Log ('-' * 60)

## Calculating Build time
$endTime = [DateTime]::UtcNow
Trace-Log "Test suite run has completed at $endTime"
Trace-Log "Time elapsed $(Format-ElapsedTime ($endTime - $startTime))"

Trace-Log ('=' * 60)

if ($BuildErrors) {
    $ErrorLines = $BuildErrors | %{ ">>> $($_.Exception.Message)" }
    Write-Error "Build's completed with $($BuildErrors.Count) error(s):`r`n$($ErrorLines -join "`r`n")" -ErrorAction Stop
}

Write-Host ("`r`n" * 3)
