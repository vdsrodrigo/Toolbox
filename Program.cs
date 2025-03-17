using Serilog;
using ToolBox.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolBox.Services;

var defaultCsvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "members_without_ledger.csv");

try
{
    var configuration = ApplicationSetup.CreateConfiguration();
    Log.Logger = ApplicationSetup.CreateLogger(configuration);

    var serviceProvider = ApplicationSetup.ConfigureServices(configuration);
    var consoleService = serviceProvider.GetRequiredService<ConsoleService>();

    consoleService.DisplayHeader();

    var csvFilePath = args.Length > 0 ? args[0] : defaultCsvFilePath;
    consoleService.DisplayInputFile(csvFilePath);

    var importService = serviceProvider.GetRequiredService<CsvImportService>();
    var result = await importService.ImportCsvToMongoAsync(csvFilePath);

    consoleService.DisplayImportResults(result);
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