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
                    await ImportCsvToMongo(serviceProvider);
                    break;
                case 2:
                    await FormatJsonFile(serviceProvider);
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

async Task ImportCsvToMongo(ServiceProvider serviceProvider)
{
    var consoleService = serviceProvider.GetRequiredService<ConsoleService>();

    Console.WriteLine("\nImportação de CSV para MongoDB");
    Console.WriteLine("---------------------------------");

    var defaultCsvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "members_without_ledger.csv");
    Console.WriteLine($"Arquivo CSV padrão: {defaultCsvFilePath}");
    Console.Write("Digite o caminho do arquivo CSV (ou pressione Enter para usar o padrão): ");

    var input = Console.ReadLine();
    var csvFilePath = string.IsNullOrWhiteSpace(input) ? defaultCsvFilePath : input;

    consoleService.DisplayInputFile(csvFilePath);

    try
    {
        var importService = serviceProvider.GetRequiredService<CsvImportService>();
        var result = await importService.ImportCsvToMongoAsync(csvFilePath);

        consoleService.DisplayImportResults(result);
    }
    catch (Exception ex)
    {
        consoleService.DisplayError($"Falha na importação: {ex.Message}");
        Log.Error(ex, "Erro ao importar CSV para MongoDB");
    }
}

async Task FormatJsonFile(ServiceProvider serviceProvider)
{
    var consoleService = serviceProvider.GetRequiredService<ConsoleService>();

    Console.WriteLine("\nFormatação de arquivo JSONL");
    Console.WriteLine("---------------------------------");

    Console.Write("Digite o caminho do arquivo JSONL: ");
    var jsonlFilePath = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(jsonlFilePath))
    {
        consoleService.DisplayError("Caminho do arquivo não fornecido.");
        return;
    }

    if (!File.Exists(jsonlFilePath))
    {
        consoleService.DisplayError($"Arquivo não encontrado: {jsonlFilePath}");
        return;
    }

    Console.WriteLine("\nDigite o nome do primeiro campo a ser extraído:");
    var field1 = Console.ReadLine()?.Trim();

    Console.WriteLine("Digite o nome do segundo campo a ser extraído:");
    var field2 = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(field1) && string.IsNullOrEmpty(field2))
    {
        consoleService.DisplayError("É necessário pelo menos um campo para extração.");
        return;
    }

    var fieldsToExtract = new List<string>();
    if (!string.IsNullOrEmpty(field1)) fieldsToExtract.Add(field1);
    if (!string.IsNullOrEmpty(field2)) fieldsToExtract.Add(field2);

    try
    {
        Console.WriteLine("\nIniciando processamento do arquivo...");

        // Configuração da barra de progresso
        int consoleWidth = Console.WindowWidth - 5;
        int barWidth = Math.Max(10, consoleWidth - 50); // Garantir uma largura mínima

        var progress = new Progress<(int Processed, int Total, TimeSpan ElapsedTime)>(data =>
        {
            double percentage = (double)data.Processed / data.Total;
            int completedWidth = (int)(barWidth * percentage);

            // Calcula tempo estimado restante
            TimeSpan estimatedTotalTime = TimeSpan.FromTicks((long)(data.ElapsedTime.Ticks / percentage));
            TimeSpan remainingTime = estimatedTotalTime - data.ElapsedTime;

            // Limpa a linha atual
            Console.Write("\r" + new string(' ', consoleWidth));
            Console.Write("\r");

            // Desenha a barra de progresso
            Console.Write("[");
            Console.Write(new string('#', completedWidth));
            Console.Write(new string(' ', barWidth - completedWidth));
            Console.Write("] ");

            // Escreve informações de progresso
            Console.Write($"{data.Processed:N0}/{data.Total:N0} ({percentage:P1}) ");

            // Tempo estimado restante
            if (data.Processed > 0)
            {
                Console.Write($"Tempo restante: {FormatTimeSpan(remainingTime)}");
            }
            else
            {
                Console.Write("Calculando tempo restante...");
            }
        });

        var jsonFormatterService = serviceProvider.GetRequiredService<JsonFormatterService>();
        var outputFilePath =
            await jsonFormatterService.ExtractFieldsToNewFileAsync(jsonlFilePath, fieldsToExtract.ToArray(), progress);

        Console.WriteLine("\n\nExtração concluída com sucesso!");
        Console.WriteLine($"Arquivo de saída: {outputFilePath}");
    }
    catch (Exception ex)
    {
        consoleService.DisplayError($"Falha na formatação do arquivo: {ex.Message}");
        Log.Error(ex, "Erro ao formatar arquivo JSONL");
    }
}

// Método auxiliar para formatar o TimeSpan de forma amigável
string FormatTimeSpan(TimeSpan timeSpan)
{
    if (timeSpan.TotalHours >= 1)
    {
        return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    return timeSpan.TotalMinutes >= 1 ? $"{timeSpan.Minutes}m {timeSpan.Seconds}s" : $"{timeSpan.Seconds}s";
}