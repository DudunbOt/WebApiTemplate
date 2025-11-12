# EF Core Migration Helper Script
# Usage:
#   .\migrate.ps1 add <MigrationName>     - Create new migration
#   .\migrate.ps1 update                   - Apply migrations to database
#   .\migrate.ps1 remove                   - Remove last migration
#   .\migrate.ps1 list                     - List all migrations
#   .\migrate.ps1 script                   - Generate SQL script

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Command,

    [Parameter(Position=1)]
    [string]$Name
)

$StartupProject = "WebApi"
$MigrationsProject = "Infrastructure"
$Context = "AppDbContext"
$OutputDir = "Data/Migrations"

switch ($Command.ToLower()) {
    "add" {
        if ([string]::IsNullOrEmpty($Name)) {
            Write-Host "Error: Migration name is required" -ForegroundColor Red
            Write-Host "Usage: .\migrate.ps1 add <MigrationName>" -ForegroundColor Yellow
            exit 1
        }
        Write-Host "Creating migration: $Name" -ForegroundColor Green
        dotnet dotnet-ef migrations add $Name `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context `
            --output-dir $OutputDir
    }

    "update" {
        Write-Host "Applying migrations to database..." -ForegroundColor Green
        dotnet dotnet-ef database update `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context
    }

    "remove" {
        Write-Host "Removing last migration..." -ForegroundColor Yellow
        dotnet dotnet-ef migrations remove `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context `
            --force
    }

    "list" {
        Write-Host "Listing migrations:" -ForegroundColor Green
        dotnet dotnet-ef migrations list `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context
    }

    "script" {
        $OutputFile = "migration.sql"
        if (-not [string]::IsNullOrEmpty($Name)) {
            $OutputFile = $Name
        }
        Write-Host "Generating SQL script: $OutputFile" -ForegroundColor Green
        dotnet dotnet-ef migrations script `
            --project $MigrationsProject `
            --startup-project $StartupProject `
            --context $Context `
            --output $OutputFile `
            --idempotent
        Write-Host "SQL script saved to: $OutputFile" -ForegroundColor Green
    }

    default {
        Write-Host "Invalid command: $Command" -ForegroundColor Red
        Write-Host ""
        Write-Host "Available commands:" -ForegroundColor Yellow
        Write-Host "  add <name>     - Create new migration"
        Write-Host "  update         - Apply migrations to database"
        Write-Host "  remove         - Remove last migration"
        Write-Host "  list           - List all migrations"
        Write-Host "  script [file]  - Generate SQL script"
        exit 1
    }
}
