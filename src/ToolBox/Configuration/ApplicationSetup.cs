using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using MongoDB.Driver;
using ToolBox.Services;
using ILogger = Serilog.ILogger;
using StackExchange.Redis;

namespace ToolBox.Configuration;

public static class ApplicationSetup
{
    public static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static ILogger CreateLogger(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configura o logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        });

        // Configura as configurações
        var configuration = CreateConfiguration();
        services.AddSingleton<IConfiguration>(configuration);

        // Configura o MongoDB
        var mongoDbSettings = new MongoDbSettings();
        configuration.GetSection("MongoDB").Bind(mongoDbSettings);
        mongoDbSettings.Validate();

        var postgresSettings = new PostgresSettings();
        configuration.GetSection("Postgres").Bind(postgresSettings);

        var client = new MongoClient(mongoDbSettings.ConnectionString);
        var database = client.GetDatabase(mongoDbSettings.DatabaseName);

        // Configura o Redis
        var redisSettings = new RedisSettings(configuration["Redis:InstanceName"]);
        var redisConnectionString = configuration["Redis:ConnectionString"];
        var redis = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redis);
        services.AddSingleton(redisSettings);

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.AddSingleton(mongoDbSettings);
        services.AddSingleton<IPostgresSettings>(postgresSettings);
        services.AddSingleton(database);

        // Registra os serviços
        services.AddSingleton<ICsvImportService, CsvImportService>();
        services.AddSingleton<IJsonToRedisService, JsonToRedisService>();
        services.AddSingleton<IJsonFormatterService, JsonFormatterService>();
        services.AddSingleton<ITextReplacementService, TextReplacementService>();
        services.AddSingleton<ISqlFileService, SqlFileService>();
        services.AddSingleton<IMigrationFileService, MigrationFileService>();
        services.AddSingleton<IConsoleService, ConsoleService>();
        services.AddSingleton<IProgressBarService, ProgressBarService>();
        services.AddSingleton<ConsoleService>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<ILedgerRepository, MongoDbService>();
        services.AddSingleton<ICsvReaderService, CsvReaderService>();
        services.AddSingleton<CsvImportService>();
        services.AddSingleton<ConsoleService>();
    }
}