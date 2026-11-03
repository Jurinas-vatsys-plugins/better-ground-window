<#
.SYNOPSIS
    Installs (or removes) the Better Ground Window plugin for vatSys.

.DESCRIPTION
    vatSys loads plugins from two places:
      * <profile>\Plugins       - only when that profile is loaded
      * <vatSys>\bin\Plugins    - for every profile

    The plugin is copied into a BetterGroundWindow sub-folder of the chosen
    Plugins folder. Installing in both places is safe: only one copy runs.

.EXAMPLE
    .\Install-BetterGroundWindow.ps1 -ProfileName "ZBPE FIR Beijing"

.EXAMPLE
    .\Install-BetterGroundWindow.ps1 -ProfilePath "D:\vatSys Profiles\Australia"

.EXAMPLE
    .\Install-BetterGroundWindow.ps1 -VatSysBin "C:\Program Files (x86)\vatSys\bin"

.EXAMPLE
    .\Install-BetterGroundWindow.ps1 -ProfileName "ZBPE FIR Beijing" -Uninstall
#>
[CmdletBinding(DefaultParameterSetName = 'ProfileName')]
param(
    # Name of a profile under Documents\vatSys Files\Profiles.
    [Parameter(Mandatory = $true, ParameterSetName = 'ProfileName')]
    [string] $ProfileName,

    # Full path to a profile folder.
    [Parameter(Mandatory = $true, ParameterSetName = 'ProfilePath')]
    [string] $ProfilePath,

    # vatSys program bin folder (the one containing vatSys.exe).
    [Parameter(Mandatory = $true, ParameterSetName = 'Bin')]
    [string] $VatSysBin,

    # Remove the plugin from the chosen location instead of installing it.
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$pluginFile = 'BetterGroundWindow.dll'

switch ($PSCmdlet.ParameterSetName) {
    'ProfileName' {
        $profilesRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'vatSys Files\Profiles'
        $root = Join-Path $profilesRoot $ProfileName
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            $available = (Get-ChildItem -LiteralPath $profilesRoot -Directory -ErrorAction SilentlyContinue).Name -join ', '
            throw "Profile '$ProfileName' was not found in $profilesRoot. Available: $available"
        }
    }
    'ProfilePath' {
        $root = $ProfilePath
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw "Profile folder not found: $root"
        }
    }
    'Bin' {
        $root = $VatSysBin
        if (-not (Test-Path -LiteralPath (Join-Path $root 'vatSys.exe') -PathType Leaf)) {
            throw "vatSys.exe was not found in $root. Pass the vatSys 'bin' folder."
        }
    }
}

$pluginsDir = Join-Path $root 'Plugins'
$targetDir = Join-Path $pluginsDir 'BetterGroundWindow'

# A copy placed directly in Plugins\ (older manual installs) is removed too.
$legacyFile = Join-Path $pluginsDir $pluginFile

if ($Uninstall) {
    $removed = $false
    foreach ($path in @($targetDir, $legacyFile)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            $removed = $true
        }
    }
    if ($removed) {
        Write-Host "Better Ground Window removed from $pluginsDir"
    } else {
        Write-Host "Better Ground Window was not installed in $pluginsDir"
    }
    return
}

# Works both from the repository (after 'dotnet build -c Release') and from an
# extracted release zip, where the DLL sits next to this script.
$candidates = @(
    (Join-Path $PSScriptRoot $pluginFile),
    (Join-Path $PSScriptRoot "bin\Release\net472\$pluginFile")
)
$source = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $source) {
    throw "Could not find $pluginFile. Run 'dotnet build -c Release' first."
}

if ((Resolve-Path -LiteralPath $source).Path -eq [IO.Path]::GetFullPath((Join-Path $targetDir $pluginFile))) {
    Write-Host "Better Ground Window is already in place: $source"
    return
}

try {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir $pluginFile) -Force
    if (Test-Path -LiteralPath $legacyFile) {
        Remove-Item -LiteralPath $legacyFile -Force
    }
} catch [UnauthorizedAccessException] {
    throw "Access denied writing to $targetDir. Run PowerShell as Administrator (needed for installs under Program Files)."
} catch [System.IO.IOException] {
    throw "Could not write to $targetDir. Close vatSys and try again. ($($_.Exception.Message))"
}

Write-Host "Better Ground Window installed: $(Join-Path $targetDir $pluginFile)"
Write-Host 'Restart vatSys to load it.'
