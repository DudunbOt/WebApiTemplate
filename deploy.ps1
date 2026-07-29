# Web API - Deployment Script
# Run this script from the project root: .\deploy.ps1

param(
    [string]$DeployPath,
    [switch]$SkipFrontend,
    [switch]$SkipBackend,
    [switch]$RestartIIS,
    [switch]$Reconfigure
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ConfigFile = "$ProjectRoot\deploy.config.json"

# Load or create configuration
function Get-DeployConfig {
    if ((Test-Path $ConfigFile) -and -not $Reconfigure) {
        $config = Get-Content $ConfigFile | ConvertFrom-Json
        return $config
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host "  First-Time Configuration" -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""

    # Ask for project name
    $defaultName = Split-Path -Leaf $ProjectRoot
    Write-Host "Enter project name (used in deployment messages)" -ForegroundColor Yellow
    $projectName = Read-Host "Project name [$defaultName]"
    if ([string]::IsNullOrWhiteSpace($projectName)) {
        $projectName = $defaultName
    }

    Write-Host ""

    # Ask for deploy path
    Write-Host "Enter the deployment directory (where publish output will be copied)" -ForegroundColor Yellow
    Write-Host "Example: C:\inetpub\wwwroot\my-api" -ForegroundColor Gray
    $deployPath = Read-Host "Deploy path"
    while ([string]::IsNullOrWhiteSpace($deployPath)) {
        Write-Host "Deploy path is required!" -ForegroundColor Red
        $deployPath = Read-Host "Deploy path"
    }

    # Save configuration
    $config = @{
        ProjectName = $projectName
        DeployPath = $deployPath
    }

    $config | ConvertTo-Json | Set-Content $ConfigFile

    Write-Host ""
    Write-Host "Configuration saved to: $ConfigFile" -ForegroundColor Green
    Write-Host ""

    return [PSCustomObject]$config
}

# Load configuration
$config = Get-DeployConfig

# Allow command-line override of DeployPath
if ([string]::IsNullOrWhiteSpace($DeployPath)) {
    $DeployPath = $config.DeployPath
}

$ProjectName = $config.ProjectName

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  $ProjectName - Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build Frontend
if (-not $SkipFrontend) {
    Write-Host "[1/4] Building frontend..." -ForegroundColor Yellow
    Set-Location "$ProjectRoot\frontend"

    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Frontend build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Frontend build complete." -ForegroundColor Green
} else {
    Write-Host "[1/4] Skipping frontend build." -ForegroundColor Gray
}

# Publish Backend
if (-not $SkipBackend) {
    Write-Host ""
    Write-Host "[2/4] Publishing backend..." -ForegroundColor Yellow
    Set-Location "$ProjectRoot\backend\WebApi"

    dotnet publish -c Release -o $DeployPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Backend publish failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Backend publish complete." -ForegroundColor Green
} else {
    Write-Host "[2/4] Skipping backend publish." -ForegroundColor Gray
}

# Copy Frontend to wwwroot
if (-not $SkipFrontend) {
    Write-Host ""
    Write-Host "[3/4] Copying frontend to deployment folder..." -ForegroundColor Yellow

    $FrontendDist = "$ProjectRoot\frontend\dist"
    $FrontendDeploy = "$DeployPath\frontend"

    # Remove old frontend files
    if (Test-Path $FrontendDeploy) {
        Remove-Item -Recurse -Force $FrontendDeploy
    }

    # Create frontend folder and copy files
    New-Item -ItemType Directory -Force -Path $FrontendDeploy | Out-Null
    Copy-Item -Path "$FrontendDist\*" -Destination $FrontendDeploy -Recurse -Force

    Write-Host "Frontend copied to: $FrontendDeploy" -ForegroundColor Green
} else {
    Write-Host "[3/4] Skipping frontend copy." -ForegroundColor Gray
}

# Restart IIS (optional)
if ($RestartIIS) {
    Write-Host ""
    Write-Host "[4/4] Restarting IIS..." -ForegroundColor Yellow
    iisreset
    Write-Host "IIS restarted." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[4/4] Skipping IIS restart. Run with -RestartIIS to restart." -ForegroundColor Gray
}

# Done
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Deployment location: $DeployPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Gray
Write-Host "  .\deploy.ps1                    # Full deployment"
Write-Host "  .\deploy.ps1 -SkipFrontend      # Backend only"
Write-Host "  .\deploy.ps1 -SkipBackend       # Frontend only"
Write-Host "  .\deploy.ps1 -RestartIIS        # Full deployment + restart IIS"
Write-Host "  .\deploy.ps1 -Reconfigure       # Re-run first-time setup"
Write-Host ""

Set-Location $ProjectRoot
