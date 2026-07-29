# Deployment Guide

This project includes a PowerShell deployment script that handles building and publishing both frontend and backend.

## Quick Start

```powershell
.\deploy.ps1
```

On first run, you'll be prompted to configure:
- **Project name** - Used in deployment messages (defaults to folder name)
- **Deploy path** - Directory where publish output will be copied

Configuration is saved to `deploy.config.json` and reused on subsequent runs.

## Usage

```powershell
# Full deployment (frontend + backend)
.\deploy.ps1

# Backend only
.\deploy.ps1 -SkipFrontend

# Frontend only
.\deploy.ps1 -SkipBackend

# Full deployment + restart IIS
.\deploy.ps1 -RestartIIS

# Re-run first-time configuration
.\deploy.ps1 -Reconfigure

# Override deploy path for a single run
.\deploy.ps1 -DeployPath "C:\other\path"
```

## Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `-DeployPath` | string | Override the configured deploy directory for this run |
| `-SkipFrontend` | switch | Skip frontend build and copy |
| `-SkipBackend` | switch | Skip backend publish |
| `-RestartIIS` | switch | Restart IIS after deployment |
| `-Reconfigure` | switch | Re-run first-time setup prompts |

## What It Does

The script performs these steps:

1. **Build Frontend** - Runs `npm run build` in the `frontend` folder
2. **Publish Backend** - Runs `dotnet publish -c Release` for the WebApi project
3. **Copy Frontend** - Copies `frontend/dist` to `{DeployPath}/frontend`
4. **Restart IIS** - (Optional) Runs `iisreset`

## Configuration File

After first-time setup, configuration is stored in `deploy.config.json`:

```json
{
  "ProjectName": "MyProject",
  "DeployPath": "C:\\inetpub\\wwwroot\\my-api"
}
```

This file should be added to `.gitignore` as it contains machine-specific paths.

## Prerequisites

- Node.js and npm (for frontend build)
- .NET SDK (for backend publish)
- Administrator privileges (if using `-RestartIIS`)

## Examples

### Development Server Deployment

```powershell
# First time - configure for dev server
.\deploy.ps1
# Enter: MyProject
# Enter: C:\inetpub\wwwroot\myproject-dev

# Subsequent runs
.\deploy.ps1
```

### Quick Backend Update

```powershell
# Skip frontend when only backend code changed
.\deploy.ps1 -SkipFrontend -RestartIIS
```

### Production Deployment

```powershell
# Use different path for production
.\deploy.ps1 -DeployPath "D:\wwwroot\myproject-prod" -RestartIIS
```
