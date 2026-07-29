# Database Migrations Guide

This project uses **Entity Framework Core Migrations** to manage database schema changes.

## Overview

Migrations are stored in: `Infrastructure/Data/Migrations/`

The application automatically applies pending migrations on startup (see [Program.cs](WebApi/Program.cs#L135)).

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- SQL Server (localhost)
- EF Core tools (already installed as local tool)

### Database Connection
Update your connection string in [appsettings.Development.json](WebApi/appsettings.Development.json):
```json
"ConnectionStrings": {
  "default": "Server=localhost;Database=default_db;user id=YourUser;password=YourPassword;..."
}
```

## Using PowerShell Script (Recommended for Windows)

We provide a helper script [migrate.ps1](migrate.ps1) for common migration tasks:

### Create a New Migration
```powershell
.\migrate.ps1 add AddNewColumn
```

### Apply Migrations to Database
```powershell
.\migrate.ps1 update
```

### Remove Last Migration
```powershell
.\migrate.ps1 remove
```

### List All Migrations
```powershell
.\migrate.ps1 list
```

### Generate SQL Script
```powershell
.\migrate.ps1 script           # Creates migration.sql
.\migrate.ps1 script output.sql  # Custom filename
```

## Using VS Code Tasks

Press **Ctrl+Shift+P** → "Tasks: Run Task" → Select from:
- `ef: add migration` - Create new migration (prompts for name)
- `ef: update database` - Apply migrations
- `ef: remove last migration` - Undo last migration
- `ef: list migrations` - Show all migrations
- `ef: generate SQL script` - Export migrations as SQL

## Using CLI Directly

If you prefer using the command line directly:

### Create Migration
```bash
dotnet dotnet-ef migrations add MigrationName \
  --project Infrastructure \
  --startup-project WebApi \
  --context AppDbContext \
  --output-dir Data/Migrations
```

### Apply Migrations
```bash
dotnet dotnet-ef database update \
  --project Infrastructure \
  --startup-project WebApi \
  --context AppDbContext
```

### Remove Last Migration
```bash
dotnet dotnet-ef migrations remove \
  --project Infrastructure \
  --startup-project WebApi \
  --context AppDbContext \
  --force
```

### List Migrations
```bash
dotnet dotnet-ef migrations list \
  --project Infrastructure \
  --startup-project WebApi \
  --context AppDbContext
```

### Generate SQL Script
```bash
dotnet dotnet-ef migrations script \
  --project Infrastructure \
  --startup-project WebApi \
  --context AppDbContext \
  --output migration.sql \
  --idempotent
```

## Workflow Examples

### Adding a New Entity

1. Create your entity in `ApplicationCore/Entities/`
2. Add DbSet to `AppDbContext`:
   ```csharp
   public DbSet<YourEntity> YourEntities { get; set; }
   ```
3. Create migration:
   ```powershell
   .\migrate.ps1 add AddYourEntityTable
   ```
4. Review the generated migration in `Infrastructure/Data/Migrations/`
5. Apply to database:
   ```powershell
   .\migrate.ps1 update
   ```

### Modifying an Existing Entity

1. Update your entity class (add/remove/modify properties)
2. Create migration:
   ```powershell
   .\migrate.ps1 add ModifyUserInfoTable
   ```
3. Review the generated migration
4. Apply to database:
   ```powershell
   .\migrate.ps1 update
   ```

### Rolling Back

To undo the last migration:
```powershell
.\migrate.ps1 remove
```

To rollback to a specific migration:
```bash
dotnet dotnet-ef database update MigrationName --project Infrastructure --startup-project WebApi
```

## Production Deployment

### Option 1: Auto-migrate on Startup (Current Setup)
The app automatically runs `context.Database.Migrate()` on startup. This works for simple deployments.

**Pros:** Simple, automatic
**Cons:** Can cause issues with multiple instances, longer startup time

### Option 2: Manual SQL Scripts
1. Generate idempotent SQL script:
   ```powershell
   .\migrate.ps1 script production.sql
   ```
2. Review the generated `production.sql`
3. Run it manually against your production database

**Pros:** Full control, can review before applying
**Cons:** Manual process

### Option 3: CI/CD Pipeline
Run migrations as part of your deployment pipeline:
```bash
dotnet dotnet-ef database update --project Infrastructure --startup-project WebApi
```

## First-Time Setup (Existing Database)

If you already have a database created with `EnsureCreated()`, run this SQL to mark the initial migration as applied:

```sql
-- Run mark-migration-applied.sql
USE default_db;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20251112070803_InitialCreate', '8.0.8');
```

Or use the provided script:
```bash
sqlcmd -S localhost -d default_db -i mark-migration-applied.sql
```

## Troubleshooting

### "There is already an object named 'X' in the database"
This means the table exists but the migration history doesn't know about it. See "First-Time Setup" above.

### "No migrations were applied"
The database is already up to date. Check with:
```powershell
.\migrate.ps1 list
```

### "Could not execute because dotnet-ef does not exist"
The tool is installed locally. Make sure you're using `dotnet dotnet-ef` (two "dotnet"s) or use the provided scripts.

### Connection String Issues
Ensure your connection string in `appsettings.Development.json` is correct and you have access to the SQL Server.

## Best Practices

1. **Always review migrations** before applying them
2. **Use descriptive names** for migrations (e.g., `AddUserEmailIndex`, not `Update1`)
3. **Test migrations** in development before production
4. **Keep migrations small** and focused on one change
5. **Don't modify** existing migrations that have been applied to production
6. **Version control** your migrations alongside your code
7. **Backup databases** before applying migrations in production

## References

- [EF Core Migrations Documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Migration Command Reference](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
