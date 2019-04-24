<#
.SYNOPSIS
This is a Powershell script to bootstrap a Cake build.
.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Cake)
and execute your Cake build script with the parameters you provide.
.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER ScriptArgss
Remaining arguments are added here.
.LINK
https://cakebuild.net
#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$WhatIf,
    [String]$BuildFile = "build.cake",
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$ScriptArgs

)

$CakeVersion = "0.28.0"
# $DotNetChannel = "Current";
$DotNetVersion = "2.1.4";
$DotNetInstallerUri = "https://dot.net/v1/dotnet-install.ps1";

$ToolsDir = Join-Path $PSScriptRoot "tools"
$ToolsProj = Join-Path $ToolsDir "tools.proj"
$ToolsProjUri = "https://raw.githubusercontent.com/SoftwarePioniere/SoftwarePioniere.Cake/master/tools.proj";

$NugetConfig = Join-Path $ToolsDir "nuget.config"
$NugetConfigUri = "https://raw.githubusercontent.com/SoftwarePioniere/SoftwarePioniere.Cake/master/nuget.config";

# $PackageDir = "$ToolsDir\Cake.CoreCLR.$CakeVersion"
# $CakeDll = "$PackageDir\cake.coreclr\$CakeVersion\Cake.dll"

$PackageDir = "$ToolsDir\packages"
$CakeDll = "$PackageDir\cake.coreclr\$CakeVersion\Cake.dll"

# Temporarily skip verification of addins.
$ENV:CAKE_SETTINGS_SKIPVERIFICATION = 'true'

# Make sure tools folder exists
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

if (!(Test-Path $ToolsDir)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolsDir -Type directory | out-null
}

###########################################################################
# INSTALL .NET CORE CLI
###########################################################################

Function Remove-PathVariable([string]$VariableToRemove) {
    $path = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($path -ne $null) {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "User")
    }

    $path = [Environment]::GetEnvironmentVariable("PATH", "Process")
    if ($path -ne $null) {
        $newItems = $path.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { "$($_)" -inotlike $VariableToRemove }
        [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "Process")
    }
}

Write-Host "Check for Installing .NET Core CLI $DotNetVersion";

[bool] $DotNetCliVersionGlobalInstalled = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {

    $versions = dotnet --list-sdks;
    # Write-Host "Found .NET CLI Versions:"

    # Write-Host $FoundDotNetCliVersions.GetType()
    $versions.GetEnumerator() | ForEach-Object {
    #    Write-Host $_

        if (!$DotNetCliVersionGlobalInstalled) {
            [int] $i = $_.IndexOf($DotNetVersion)
            #Write-Host $i;
            $DotNetCliVersionGlobalInstalled = ($i -gt -1 )
            # Write-Host $DotNetCliVersionGlobalInstalled
        }
    }
}

if ( ! $DotNetCliVersionGlobalInstalled ) {
    Write-Host "Preparing local .NET Core CLI";

    $InstallPath = Join-Path $ToolsDir ".dotnet"

    if (!(Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null;
    }

    if (! (Test-Path "$InstallPath\dotnet-install.ps1") ) {
        Write-Host "Installing .NET Core CLI SDK local"

        (New-Object System.Net.WebClient).DownloadFile($DotNetInstallerUri, "$InstallPath\dotnet-install.ps1");
        & $InstallPath\dotnet-install.ps1 -Channel $DotNetChannel -Version $DotNetVersion -InstallDir $InstallPath;

    }
    else {
        Write-Host ".NET Core CLI already local Installed";
    }

    Remove-PathVariable "$InstallPath"
    $env:PATH = "$InstallPath;$env:PATH"
}
else {
    Write-Host ".NET CLI Version global Installed: $DotNetCliVersionGlobalInstalled"
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1

###########################################################################
# INSTALL CAKE
###########################################################################


if (!(Test-Path $ToolsProj)) {
    Write-Host "Installing tools.proj"
    (New-Object System.Net.WebClient).DownloadFile($ToolsProjUri, $ToolsProj);
}

if (!(Test-Path $NugetConfig)) {
    Write-Host "Installing nuget.config"
    (New-Object System.Net.WebClient).DownloadFile($NugetConfigUri, $NugetConfig);
}

if (!(Test-Path $CakeDll)) {
    Write-Host "Installing Cake.Core"
    dotnet add "$ToolsProj" package Cake.CoreCLR -v "$CakeVersion" --package-directory "$PackageDir"
}

if (! (Test-Path $CakeDll)) {
    Write-Host "Could not find Cake.dll at '$CAKE_DLL'."
    exit 1
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

#Build the argument list.
$Arguments = @{
    target        = $Target;
    configuration = $Configuration;
    verbosity     = $Verbosity;
    dryrun        = $WhatIf;
}.GetEnumerator() | ForEach-Object {"--{0}=`"{1}`"" -f $_.key, $_.value };


# Start Cake
Write-Host "Running build script $BuildFile ..."
Write-Host "Arguments: $Arguments "
dotnet $CakeDll $BuildFile $Arguments

exit $LASTEXITCODE