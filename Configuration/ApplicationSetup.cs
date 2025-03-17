using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using ToolBox.Services;
using ILogger = Serilog.ILogger;

namespace ToolBox.Configuration;

public static class ApplicationSetup
{
    public static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    public static ILogger CreateLogger(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    public static ServiceProvider ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        ConfigureLogging(services);
        ConfigureOptions(services, configuration);
        RegisterServices(services);

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