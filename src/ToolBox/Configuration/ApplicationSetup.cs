using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;
using MongoDB.Driver;
using ToolBox.Services;
using ILogger = Serilog.ILogger;

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

    public static IServiceProvider ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        ConfigureLogging(services);
        ConfigureOptions(services, configuration);
        RegisterServices(services);
        services.AddScoped<JsonFormatterService>();
        services.AddScoped<ITextReplacementService, TextReplacementService>();

        // Obtenha as configurações do Redis
        var redisConnectionString = configuration["Redis:ConnectionString"];
        var redisInstanceName = configuration["Redis:InstanceName"];

        // Registrar o ConnectionMultiplexer com singleton
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

        // Registrar o InstanceName diretamente na DI:
        services.AddSingleton(new RedisSettings(redisInstanceName));
        
        services.AddScoped<IJsonToRedisService, JsonToRedisService>();
        
        // Configurar MongoDB
        var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>();
        var mongoClient = new MongoClient(mongoSettings.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoSettings.DatabaseName);
        services.AddSingleton<IMongoDatabase>(mongoDatabase);

        // Registra os serviços
        services.AddSingleton<IConsoleService, ConsoleService>();
        services.AddSingleton<IProgressBarService, ProgressBarService>();
        services.AddSingleton<CsvImportService>();
        services.AddSingleton<JsonFormatterService>();
        services.AddSingleton<ITextReplacementService, TextReplacementService>();
        services.AddSingleton<IJsonToRedisService, JsonToRedisService>();

        // Registra as configurações
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDb"));
        services.Configure<RedisSettings>(configuration.GetSection("Redis"));

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