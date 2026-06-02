# EF Core Migration Helper Script (Multi-Provider Support)
# Usage:
#   .\migrate.ps1 add <MigrationName>       - Create new migration for current provider
#   .\migrate.ps1 add <MigrationName> -All  - Create migrations for ALL providers
#   .\migrate.ps1 update                    - Apply migrations to database
#   .\migrate.ps1 remove                    - Remove last migration
#   .\migrate.ps1 list                      - List all migrations
#   .\migrate.ps1 script                    - Generate SQL script
#   .\migrate.ps1 provider                  - Show current database provider

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Command,

    [Parameter(Position=1)]
    [string]$Name,

    [Parameter()]
    [switch]$All
)

$StartupProject = "WebApi"
$MigrationsProject = "Infrastructure"
$Context = "AppDbContext"

# Read current provider from appsettings
function Get-CurrentProvider {
    $appSettingsPath = "$StartupProject/appsettings.json"
    if (Test-Path $appSettingsPath) {
        $config = Get-Content $appSettingsPath | ConvertFrom-Json
        $provider = $config.DatabaseProvider
        if ([string]::IsNullOrEmpty($provider)) {
            return "sqlserver"
        }
        return $provider.ToLower()
    }
    return "sqlserver"
}

# Get output directory based on provider
function Get-OutputDir {
    param([string]$Provider)

    switch ($Provider) {
        { $_ -in "postgres", "postgresql" } { return "Data/Migrations/PostgreSql" }
        default { return "Data/Migrations/SqlServer" }
    }
}

# Get display name for provider
function Get-ProviderDisplayName {
    param([string]$Provider)

    switch ($Provider) {
        { $_ -in "postgres", "postgresql" } { return "PostgreSQL" }
        default { return "SQL Server" }
    }
}

# Temporarily set provider in appsettings
function Set-Provider {
    param([string]$Provider)

    $appSettingsPath = "$StartupProject/appsettings.json"
    $config = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
    $config.DatabaseProvider = $Provider
    $config | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
}

$currentProvider = Get-CurrentProvider
$outputDir = Get-OutputDir -Provider $currentProvider
$providerName = Get-ProviderDisplayName -Provider $currentProvider

switch ($Command.ToLower()) {
    "add" {
        if ([string]::IsNullOrEmpty($Name)) {
            Write-Host "Error: Migration name is required" -ForegroundColor Red
            Write-Host "Usage: .\migrate.ps1 add <MigrationName>" -ForegroundColor Yellow
            Write-Host "       .\migrate.ps1 add <MigrationName> -All  (for all providers)" -ForegroundColor Yellow
            exit 1
        }

        if ($All) {
            # Create migrations for ALL providers
            Write-Host "Creating migrations for ALL providers..." -ForegroundColor Cyan
            Write-Host ""

            $originalProvider = $currentProvider
            $providers = @(
                @{ Name = "sqlserver"; Display = "SQL Server"; OutputDir = "Data/Migrations/SqlServer" },
                @{ Name = "postgresql"; Display = "PostgreSQL"; OutputDir = "Data/Migrations/PostgreSql" }
            )

            foreach ($p in $providers) {
                Write-Host "[$($p.Display)] Creating migration: $Name" -ForegroundColor Green
                Set-Provider -Provider $p.Name

                dotnet ef migrations add $Name `
                    --project $MigrationsProject `
                    --startup-project $StartupProject `
                    --context $Context `
                    --output-dir $p.OutputDir

                if ($LASTEXITCODE -eq 0) {
                    Write-Host "[$($p.Display)] Migration created successfully" -ForegroundColor Green
                } else {
                    Write-Host "[$($p.Display)] Migration failed" -ForegroundColor Red
                }
                Write-Host ""
            }

            # Restore original provider
            Set-Provider -Provider $originalProvider
            Write-Host "Restored provider to: $(Get-ProviderDisplayName -Provider $originalProvider)" -ForegroundColor Cyan
        }
        else {
            # Create migration for current provider only
            Write-Host "[$providerName] Creating migration: $Name" -ForegroundColor Green
            Write-Host "Output directory: $outputDir" -ForegroundColor Gray
            Write-Host ""

            dotnet ef migrations add $Name `
                --project $MigrationsProject `
                --startup-project $StartupProject `
                --context $Context `
                --output-dir $outputDir

            if ($LASTEXITCODE -eq 0) {
                Write-Host ""
                Write-Host "Migration created successfully!" -ForegroundColor Green
                Write-Host "Tip: Use '.\migrate.ps1 add $Name -All' to create for all providers" -ForegroundColor Yellow
            }
        }
    }

    "update" {
        Write-Host "[$providerName] Applying migrations to database..." -ForegroundColor Green
        dotnet ef database update `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context
    }

    "remove" {
        Write-Host "[$providerName] Removing last migration..." -ForegroundColor Yellow
        Write-Host "Output directory: $outputDir" -ForegroundColor Gray
        dotnet ef migrations remove `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context `
            --force
    }

    "list" {
        Write-Host "[$providerName] Listing migrations:" -ForegroundColor Green
        dotnet ef migrations list `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context
    }

    "script" {
        $OutputFile = "migration_$($currentProvider).sql"
        if (-not [string]::IsNullOrEmpty($Name)) {
            $OutputFile = $Name
        }
        Write-Host "[$providerName] Generating SQL script: $OutputFile" -ForegroundColor Green
        dotnet ef migrations script `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context `
            --output $OutputFile `
            --idempotent
        Write-Host "SQL script saved to: $OutputFile" -ForegroundColor Green
    }

    "provider" {
        Write-Host "Current database provider: $providerName" -ForegroundColor Cyan
        Write-Host "Migrations directory: $outputDir" -ForegroundColor Gray
        Write-Host ""
        Write-Host "To change provider, edit 'DatabaseProvider' in $StartupProject/appsettings.json" -ForegroundColor Yellow
        Write-Host "  - 'sqlserver' (default) -> Data/Migrations/SqlServer/"
        Write-Host "  - 'postgresql'          -> Data/Migrations/PostgreSql/"
    }

    default {
        Write-Host "Invalid command: $Command" -ForegroundColor Red
        Write-Host ""
        Write-Host "Current provider: $providerName" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Available commands:" -ForegroundColor Yellow
        Write-Host "  add <name>        - Create migration for current provider ($providerName)"
        Write-Host "  add <name> -All   - Create migrations for ALL providers"
        Write-Host "  update            - Apply migrations to database"
        Write-Host "  remove            - Remove last migration"
        Write-Host "  list              - List all migrations"
        Write-Host "  script [file]     - Generate SQL script"
        Write-Host "  provider          - Show current database provider"
        exit 1
    }
}
