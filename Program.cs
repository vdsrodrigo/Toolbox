using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolBox.Services;
using Serilog;

namespace ToolBox;

public class Program
{
    private static readonly string _defaultCsvFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "members_without_ledger.csv");

    public static async Task Main(string[] args)
    {
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Setup Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            // Setup DI
            var serviceProvider = ConfigureServices(configuration);

            Console.WriteLine("====================================================");
            Console.WriteLine("  CSV to MongoDB Ledger Importer");
            Console.WriteLine("====================================================");

            var csvFilePath = args.Length > 0 ? args[0] : _defaultCsvFilePath;
            Console.WriteLine($"CSV File: {csvFilePath}");

            // Run import service
            var importService = serviceProvider.GetRequiredService<CsvImportService>();
            var result = await importService.ImportCsvToMongoAsync(csvFilePath);

            // Display results
            Console.WriteLine("\nImport Statistics:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine($"Total records processed: {result.TotalRecords:N0}");
            Console.WriteLine($"Total records imported: {result.InsertedRecords:N0}");
            Console.WriteLine($"Total batches: {result.TotalBatches:N0}");
            Console.WriteLine($"Failed batches: {result.FailedBatches:N0}");
            Console.WriteLine($"Duration: {result.DurationMs / 1000:N2} seconds");
            Console.WriteLine($"Average rate: {result.TotalRecords / (result.DurationMs / 1000):N0} records/second");
            Console.WriteLine("====================================================");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            Console.WriteLine($"ERROR: {ex.Message}");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static ServiceProvider ConfigureServices(IConfiguration configuration)
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
        services.AddSingleton<MongoDbService>();
        services.AddSingleton<CsvImportService>();

        return services.BuildServiceProvider();
    }
}