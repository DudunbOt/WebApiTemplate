using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using System.Reflection;

namespace Infrastructure.Data.Migrations
{
    public abstract class ProviderMigrationsAssembly : IMigrationsAssembly
    {
        private readonly IMigrationsIdGenerator _idGenerator;
        private readonly IDiagnosticsLogger<DbLoggerCategory.Migrations> _logger;
        private readonly ICurrentDbContext _currentContext;
        private readonly IDbContextOptions _contextOptions;
        private readonly IMigrationsAssembly _innerAssembly;

        protected abstract string MigrationsNamespace { get; }

        protected ProviderMigrationsAssembly(
            ICurrentDbContext currentContext,
            IDbContextOptions contextOptions,
            IMigrationsIdGenerator idGenerator,
            IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        {
            _currentContext = currentContext;
            _contextOptions = contextOptions;
            _idGenerator = idGenerator;
            _logger = logger;

            // Create the default implementation to delegate to
            _innerAssembly = new MigrationsAssembly(
                currentContext,
                contextOptions,
                idGenerator,
                logger);
        }

        public Assembly Assembly => _innerAssembly.Assembly;

        public string? FindMigrationId(string nameOrId)
        {
            // Filter migrations by namespace
            var migrations = Migrations;
            foreach (var migration in migrations)
            {
                if (string.Equals(migration.Key, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(migration.Value.GetTypeInfo().Name, nameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    return migration.Key;
                }
            }
            return null;
        }

        public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
        {
            return _innerAssembly.CreateMigration(migrationClass, activeProvider);
        }

        public IReadOnlyDictionary<string, TypeInfo> Migrations
        {
            get
            {
                // Filter to only include migrations from the specific namespace
                return _innerAssembly.Migrations
                    .Where(m => m.Value.Namespace?.StartsWith(MigrationsNamespace, StringComparison.OrdinalIgnoreCase) == true)
                    .ToDictionary(m => m.Key, m => m.Value);
            }
        }

        public ModelSnapshot? ModelSnapshot
        {
            get
            {
                // Find snapshot in the correct namespace
                var snapshotType = Assembly.GetTypes()
                    .FirstOrDefault(t =>
                        typeof(ModelSnapshot).IsAssignableFrom(t) &&
                        t.Namespace?.StartsWith(MigrationsNamespace, StringComparison.OrdinalIgnoreCase) == true);

                if (snapshotType == null)
                    return null;

                return (ModelSnapshot?)Activator.CreateInstance(snapshotType);
            }
        }
    }

    public class PostgreSqlMigrationsAssembly : ProviderMigrationsAssembly
    {
        protected override string MigrationsNamespace => "Infrastructure.Data.Migrations.PostgreSql";

        public PostgreSqlMigrationsAssembly(
            ICurrentDbContext currentContext,
            IDbContextOptions contextOptions,
            IMigrationsIdGenerator idGenerator,
            IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
            : base(currentContext, contextOptions, idGenerator, logger)
        {
        }
    }

    public class SqlServerMigrationsAssembly : ProviderMigrationsAssembly
    {
        protected override string MigrationsNamespace => "Infrastructure.Data.Migrations.SqlServer";

        public SqlServerMigrationsAssembly(
            ICurrentDbContext currentContext,
            IDbContextOptions contextOptions,
            IMigrationsIdGenerator idGenerator,
            IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
            : base(currentContext, contextOptions, idGenerator, logger)
        {
        }
    }
}
