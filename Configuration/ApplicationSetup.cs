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

        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Add configuration
        services.AddSingleton(configuration);

        // Add services
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);
        services.AddScoped<ILedgerRepository, MongoDbService>();
        services.AddSingleton<CsvImportService>();
        services.AddSingleton<ConsoleService>();

        return services.BuildServiceProvider();
    }
}