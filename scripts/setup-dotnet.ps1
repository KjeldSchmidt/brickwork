#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RequiredMajor = 10

function Add-DotNetToPath {
    $dotnetDirs = @(
        "$env:ProgramFiles\dotnet",
        ${env:ProgramFiles(x86)} + '\dotnet',
        "$env:USERPROFILE\.dotnet"
    )
    foreach ($dir in $dotnetDirs) {
        if (Test-Path $dir) {
            $env:Path = "$dir;$env:Path"
        }
    }
}

function Test-DotNetSdkInstalled {
    param([int]$Major)

    Add-DotNetToPath
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        return $false
    }

    $sdks = dotnet --list-sdks 2>$null
    foreach ($line in $sdks) {
        if ($line -match "^$Major\.") {
            return $true
        }
    }
    return $false
}

function Install-DotNetSdkWindows {
    param([int]$Major)

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "Installing .NET SDK $Major via winget..."
        & winget install --id "Microsoft.DotNet.SDK.$Major" --exact `
            --accept-package-agreements --accept-source-agreements
        Add-DotNetToPath
        if (Test-DotNetSdkInstalled -Major $Major) {
            return
        }
        Write-Warning "winget install finished but .NET SDK $Major is not on PATH yet."
    }

    Write-Host "Installing .NET SDK $Major via dotnet-install.ps1..."
    $installDir = "$env:USERPROFILE\.dotnet"
    $installScript = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript
    & $installScript -Channel "$Major.0" -InstallDir $installDir
    Add-DotNetToPath
}

if (Test-DotNetSdkInstalled -Major $RequiredMajor) {
    Write-Host ".NET SDK $RequiredMajor is already installed: $(dotnet --version)"
    exit 0
}

if ($env:OS -ne 'Windows_NT') {
    Write-Error @"
.NET SDK $RequiredMajor is not installed.
Install it from https://dotnet.microsoft.com/download/dotnet/$RequiredMajor.0
"@
    exit 1
}

Install-DotNetSdkWindows -Major $RequiredMajor

if (-not (Test-DotNetSdkInstalled -Major $RequiredMajor)) {
    Write-Error "Failed to install .NET SDK $RequiredMajor. Open a new terminal and run 'dotnet --version', or install manually."
    exit 1
}

Write-Host "Installed .NET SDK: $(dotnet --version)"
