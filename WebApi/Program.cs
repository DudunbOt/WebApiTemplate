using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using Asp.Versioning;
using Infrastructure.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NLog;
using NLog.Web;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using WebApi.Middleware;

// Configure Npgsql to handle DateTime as UTC (required for PostgreSQL timestamp with time zone)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


// Early init of NLog to allow startup and exception logging
var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("Application starting...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog: Setup NLog for Dependency injection
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    //JWT Configuration
    var jwtSettings = builder.Configuration.GetSection("JWT");
    var jwtSettingObj = jwtSettings.Get<JwtSettings>();
    //make JWT Setting object to be "Injectable"
    builder.Services.Configure<JwtSettings>(jwtSettings);

    //Inject AppConfig
    builder.Services.Configure<AppConfig>(builder.Configuration.GetSection("AppConfig"));

    //Inject EmailSettings
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettingObj?.Issuer,
                ValidAudience = jwtSettingObj?.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettingObj?.Key))
            };
        });

    //Api Versioning
    builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

    //Setting Cors
    builder.Services.AddCors(options =>
{
    //Add more if specific policy is needed
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin();

    });
});

    //Setting DB - supports multiple providers via configuration
    var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "sqlserver";
    var connectionString = builder.Configuration.GetConnectionString("default");
    if (dbProvider.ToLower() == "postgres" || dbProvider.ToLower() == "postgresql")
    {
        logger.Info("Configuring PostgreSQL database provider");
        connectionString = builder.Configuration.GetConnectionString("default_postgres");
    }

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        switch (dbProvider.ToLower())
        {
            case "postgres":
            case "postgresql":
                options.UseNpgsql(connectionString, x => x
                    .MigrationsAssembly("Infrastructure")
                    .MigrationsHistoryTable("__ef_migrations_history_postgre_sql"))
                    .UseSnakeCaseNamingConvention();
                options.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly,
                    Infrastructure.Data.Migrations.PostgreSqlMigrationsAssembly>();
                break;
            case "sqlserver":
            default:
                options.UseSqlServer(connectionString, x => x
                    .MigrationsAssembly("Infrastructure")
                    .MigrationsHistoryTable("__EFMigrationsHistory_SqlServer"));
                options.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly,
                    Infrastructure.Data.Migrations.SqlServerMigrationsAssembly>();
                break;
        }
    });

    //Configure Redis
    builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});

    //Configure Health Checks - dynamic based on database provider
    var healthChecks = builder.Services.AddHealthChecks();

    switch (dbProvider.ToLower())
    {
        case "postgres":
        case "postgresql":
            healthChecks.AddNpgSql(
                connectionString!,
                name: "postgresql",
                tags: new[] { "db", "postgresql" });
            break;
        case "sqlserver":
        default:
            healthChecks.AddSqlServer(
                connectionString!,
                name: "sqlserver",
                tags: new[] { "db", "sqlserver" });
            break;
    }

    healthChecks.AddRedis(
        builder.Configuration.GetConnectionString("redis")!,
        name: "redis",
        tags: new[] { "cache", "redis" });


    //Setting AutoMapper
    builder.Services.AddAutoMapper(cfg => { }, typeof(AutoMapperProfile));

    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    //Inject Services
    var infrastructureAssembly = Assembly.Load("Infrastructure");
    //var applicationCoreAssembly = Assembly.Load("ApplicationCore");
    Assembly assembly = Assembly.GetExecutingAssembly();
    builder.Services.Scan(scan => scan
        .FromAssemblies(infrastructureAssembly)
        .AddClasses(classes => classes.AssignableTo(typeof(IServiceBase<>)))
        .AsImplementedInterfaces()
        .WithTransientLifetime()
    );

    //Register Background Services
    builder.Services.AddHostedService<Infrastructure.BackgroundServices.EmailQueueProcessor>();

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT token"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    var app = builder.Build();

    //Apply pending migrations automatically on startup
    using (IServiceScope scope = app.Services.CreateScope())
    {
        IServiceProvider services = scope.ServiceProvider;
        AppDbContext context = services.GetRequiredService<AppDbContext>();

        // Automatically apply pending migrations
        //context.Database.Migrate();
    }

    // Configure the HTTP request pipeline.
    // Global Exception Handler - must be early in the pipeline
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    //Change to spesific CORS policy if needed
    app.UseCors("AllowAllOrigins");

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    // Health Check Endpoints
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
    });

    // Simple liveness probe (no dependencies checked)
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false // Don't run any checks, just return 200 OK if app is running
    });

    // Readiness probe (checks all dependencies)
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("cache")
    });

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

