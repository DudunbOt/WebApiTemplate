using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Configurations
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Build configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../WebApi"))
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var dbProvider = configuration.GetValue<string>("DatabaseProvider") ?? "sqlserver";
            var connectionString = configuration.GetConnectionString("default");

            // Use separate connection string for PostgreSQL
            if (dbProvider.ToLower() == "postgres" || dbProvider.ToLower() == "postgresql")
            {
                connectionString = configuration.GetConnectionString("default_postgres");
            }

            switch (dbProvider.ToLower())
            {
                case "postgres":
                case "postgresql":
                    optionsBuilder.UseNpgsql(connectionString, x => x
                        .MigrationsAssembly("Infrastructure")
                        .MigrationsHistoryTable("__ef_migrations_history_postgre_sql"))
                        .UseSnakeCaseNamingConvention();
                    optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly,
                        Data.Migrations.PostgreSqlMigrationsAssembly>();
                    break;
                case "sqlserver":
                default:
                    optionsBuilder.UseSqlServer(connectionString, x => x
                        .MigrationsAssembly("Infrastructure")
                        .MigrationsHistoryTable("__EFMigrationsHistory_SqlServer"));
                    optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly,
                        Data.Migrations.SqlServerMigrationsAssembly>();
                    break;
            }

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
