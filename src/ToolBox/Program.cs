using Serilog;
using ToolBox.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolBox.Services;

try
{
    var configuration = ApplicationSetup.CreateConfiguration();
    Log.Logger = ApplicationSetup.CreateLogger(configuration);

    var serviceProvider = ApplicationSetup.ConfigureServices();
    var consoleService = serviceProvider.GetRequiredService<ConsoleService>();

    bool exit = false;
    while (!exit)
    {
        consoleService.DisplayHeader();
        Console.WriteLine("\nEscolha uma opção:");
        Console.WriteLine("1. Importar CSV para MongoDB");
        Console.WriteLine("2. Converter JSON para Redis");
        Console.WriteLine("3. Formatar arquivo JSON");
        Console.WriteLine("4. Processar arquivo SQL");
        Console.WriteLine("5. Formatar arquivo de migração");
        Console.WriteLine("6. Processar CPFs do CSV");
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
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    await consoleService.ProcessOptionAsync(option);
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