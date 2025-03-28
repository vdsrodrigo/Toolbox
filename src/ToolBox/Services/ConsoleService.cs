using System.Text;
using ToolBox.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ToolBox.Services;

public class ConsoleService : IConsoleService
{
    public void DisplayHeader()
    {
        Console.WriteLine("====================================================");
        Console.WriteLine("  CSV to MongoDB Ledger Importer");
        Console.WriteLine("====================================================");
    }

    public void DisplayInputFile(string csvFilePath)
    {
        Console.WriteLine($"CSV File: {csvFilePath}");
    }

    public void DisplayImportResults(ImportResult result)
    {
        Console.WriteLine("\nImport Statistics:");
        Console.WriteLine("----------------------------------------------------");
        Console.WriteLine($"Total records processed: {result.TotalRecords:N0}");
        Console.WriteLine($"Total records imported: {result.InsertedRecords:N0}");
        Console.WriteLine($"Total batches: {result.TotalBatches:N0}");
        Console.WriteLine($"Failed batches: {result.FailedBatches:N0}");
        Console.WriteLine($"Duration: {result.DurationInSeconds:N2} seconds");
        Console.WriteLine($"Average rate: {result.RecordsPerSecond:N0} records/second");
        Console.WriteLine("====================================================");
    }

    public void DisplayError(string message)
    {
        Console.WriteLine($"ERROR: {message}");
    }
    
    public async Task ImportCsvToMongoAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("\nImportação de CSV para MongoDB");
        Console.WriteLine("---------------------------------");

        var defaultCsvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "members_without_ledger.csv");
        Console.WriteLine($"Arquivo CSV padrão: {defaultCsvFilePath}");
        Console.Write("Digite o caminho do arquivo CSV (ou pressione Enter para usar o padrão): ");

        var input = Console.ReadLine();
        var csvFilePath = string.IsNullOrWhiteSpace(input) ? defaultCsvFilePath : input;

        DisplayInputFile(csvFilePath);

        try
        {
            var importService = serviceProvider.GetRequiredService<CsvImportService>();
            var result = await importService.ImportCsvToMongoAsync(csvFilePath);
            DisplayImportResults(result);
        }
        catch (Exception ex)
        {
            DisplayError($"Falha na importação: {ex.Message}");
            Log.Error(ex, "Erro ao importar CSV para MongoDB");
        }
    }

    public async Task FormatJsonFileAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("\nFormatação de arquivo JSONL");
        Console.WriteLine("---------------------------------");

        Console.Write("Digite o caminho do arquivo JSONL: ");
        var jsonlFilePath = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(jsonlFilePath))
        {
            DisplayError("Caminho do arquivo não fornecido.");
            return;
        }

        if (!File.Exists(jsonlFilePath))
        {
            DisplayError($"Arquivo não encontrado: {jsonlFilePath}");
            return;
        }

        Console.WriteLine("\nDigite o nome do primeiro campo a ser extraído:");
        var field1 = Console.ReadLine()?.Trim();

        Console.WriteLine("Digite o nome do segundo campo a ser extraído:");
        var field2 = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(field1) && string.IsNullOrEmpty(field2))
        {
            DisplayError("É necessário pelo menos um campo para extração.");
            return;
        }

        var fieldsToExtract = new List<string>();
        if (!string.IsNullOrEmpty(field1)) fieldsToExtract.Add(field1);
        if (!string.IsNullOrEmpty(field2)) fieldsToExtract.Add(field2);

        try
        {
            Console.WriteLine("\nIniciando processamento do arquivo...");

            var jsonFormatterService = serviceProvider.GetRequiredService<JsonFormatterService>();
            var outputFilePath = await jsonFormatterService.ExtractFieldsToNewFileAsync(jsonlFilePath, fieldsToExtract.ToArray());

            Console.WriteLine("\n\nExtração concluída com sucesso!");
            Console.WriteLine($"Arquivo de saída: {outputFilePath}");
        }
        catch (Exception ex)
        {
            DisplayError($"Falha na formatação do arquivo: {ex.Message}");
            Log.Error(ex, "Erro ao formatar arquivo JSONL");
        }
    }

    public async Task ReplaceTextInFileAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("\n=== Substituir Texto em Arquivo ===");
        Console.Write("Digite o caminho do arquivo: ");
        var filePath = Console.ReadLine();

        if (string.IsNullOrEmpty(filePath))
        {
            DisplayError("Caminho do arquivo não pode ser vazio!");
            return;
        }

        Console.Write("Digite o texto a ser substituído: ");
        var searchText = Console.ReadLine();

        if (string.IsNullOrEmpty(searchText))
        {
            DisplayError("Texto a ser substituído não pode ser vazio!");
            return;
        }

        Console.Write("Digite o novo texto (pressione Enter para remover o texto): ");
        var replacementText = Console.ReadLine();

        try
        {
            var textReplacementService = serviceProvider.GetRequiredService<ITextReplacementService>();
            var newFilePath = await textReplacementService.ReplaceTextInFileAsync(filePath, searchText, replacementText);

            Console.WriteLine($"\nArquivo processado com sucesso!");
            Console.WriteLine($"Novo arquivo salvo em: {newFilePath}");
        }
        catch (Exception ex)
        {
            DisplayError($"Erro ao processar o arquivo: {ex.Message}");
            Log.Error(ex, "Erro ao substituir texto no arquivo");
        }
    }

    public async Task JsonToRedisAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("Informe o caminho do arquivo JSONL a ser processado:");
        var filePath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            DisplayError("Caminho não pode ser vazio.");
            return;
        }

        Console.WriteLine("Digite o nome do campo a ser usado como chave no Redis:");
        var keyField = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(keyField))
        {
            DisplayError("O campo-chave não pode ser vazio.");
            return;
        }

        Console.WriteLine("Digite o nome do campo a ser usado como valor no Redis:");
        var valueField = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(valueField))
        {
            DisplayError("O campo-valor não pode ser vazio.");
            return;
        }

        Console.WriteLine("\nProcessando e publicando dados para o Redis...");
        var startTime = DateTime.Now;

        try
        {
            var redisService = serviceProvider.GetRequiredService<IJsonToRedisService>();
            var totalPublished = await redisService.ExecuteAsync(filePath, keyField, valueField);

            Console.WriteLine("\n✅ Publicação concluída com sucesso.");
            Console.WriteLine($"📌 Total de entradas publicadas no Redis: {totalPublished}");
            Console.WriteLine($"⏱️ Tempo total gasto: {DateTime.Now - startTime}");
        }
        catch (Exception ex)
        {
            DisplayError($"Erro ao publicar no Redis: {ex.Message}");
            Log.Error(ex, "Erro ao publicar dados no Redis");
        }
    }
}