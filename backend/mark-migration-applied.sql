-- This script marks the InitialCreate migration as applied without running it
-- Use this if you already have the database created via EnsureCreated()

USE default_db;
GO

-- Insert the migration record into the history table
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20251112070803_InitialCreate', '8.0.8');
GO

-- Verify the migration was registered
SELECT * FROM [__EFMigrationsHistory];
GO
