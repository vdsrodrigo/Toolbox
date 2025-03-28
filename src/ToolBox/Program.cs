using Serilog;
using ToolBox.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolBox.Services;

try
{
    var configuration = ApplicationSetup.CreateConfiguration();
    Log.Logger = ApplicationSetup.CreateLogger(configuration);

    var serviceProvider = ApplicationSetup.ConfigureServices(configuration);
    var consoleService = serviceProvider.GetRequiredService<ConsoleService>();

    bool exit = false;
    while (!exit)
    {
        consoleService.DisplayHeader();
        Console.WriteLine("\nEscolha uma opção:");
        Console.WriteLine("1 - Importar CSV para MongoDB");
        Console.WriteLine("2 - Formatar arquivo JSON");
        Console.WriteLine("3 - Substituir Texto em Arquivo");
        Console.WriteLine("4 - Ler JSONL e publicar dados no Redis");
        Console.WriteLine("0 - Sair");
        Console.Write("\nSua escolha: ");

        if (int.TryParse(Console.ReadLine(), out int option))
        {
            switch (option)
            {
                case 0:
                    exit = true;
                    break;
                case 1:
                    await consoleService.ImportCsvToMongoAsync(serviceProvider);
                    break;
                case 2:
                    await consoleService.FormatJsonFileAsync(serviceProvider);
                    break;
                case 3:
                    await consoleService.ReplaceTextInFileAsync(serviceProvider);
                    break;
                case 4:
                    await consoleService.JsonToRedisAsync(serviceProvider);
                    break;
                default:
                    Console.WriteLine("\nOpção inválida. Tente novamente.");
                    break;
            }
        }
        else
        {
            Console.WriteLine("\nEntrada inválida. Por favor, digite um número.");
        }

        if (!exit)
        {
            Console.WriteLine("\nPressione Enter para continuar...");
            Console.ReadLine();
            Console.Clear();
        }
    }
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