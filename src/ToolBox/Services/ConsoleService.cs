using ToolBox.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Logging;

namespace ToolBox.Services;

public class ConsoleService : IConsoleService
{
    private readonly ICsvImportService _csvImportService;
    private readonly IJsonToRedisService _jsonToRedisService;
    private readonly IJsonFormatterService _jsonFormatterService;
    private readonly ITextReplacementService _textReplacementService;
    private readonly ISqlFileService _sqlFileService;
    private readonly ILogger<ConsoleService> _logger;

    public ConsoleService(
        ICsvImportService csvImportService,
        IJsonToRedisService jsonToRedisService,
        IJsonFormatterService jsonFormatterService,
        ITextReplacementService textReplacementService,
        ISqlFileService sqlFileService,
        ILogger<ConsoleService> logger)
    {
        _csvImportService = csvImportService;
        _jsonToRedisService = jsonToRedisService;
        _jsonFormatterService = jsonFormatterService;
        _textReplacementService = textReplacementService;
        _sqlFileService = sqlFileService;
        _logger = logger;
    }

    public void DisplayHeader()
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("               ToolBox v1.0.0                  ");
        Console.WriteLine("===============================================");
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

    public async Task ProcessOptionAsync(int option)
    {
        try
        {
            switch (option)
            {
                case 1:
                    await ProcessCsvImportAsync();
                    break;
                case 2:
                    await ProcessJsonToRedisAsync();
                    break;
                case 3:
                    await ProcessJsonFormatterAsync();
                    break;
                case 4:
                    await ProcessSqlFileAsync();
                    break;
                default:
                    Console.WriteLine("Opção inválida!");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar opção {Option}", option);
            Console.WriteLine($"Erro: {ex.Message}");
        }
    }

    public async Task ProcessCsvImportAsync()
    {
        Console.WriteLine("\nImportação de CSV para MongoDB");
        Console.WriteLine("=============================");

        var filePath = GetInputFilePath("Digite o caminho do arquivo CSV");
        if (string.IsNullOrEmpty(filePath)) return;

        var result = await _csvImportService.ImportCsvToMongoAsync(filePath);
        DisplayImportResult(result);
    }

    public async Task ProcessJsonToRedisAsync()
    {
        Console.WriteLine("\nConversão de JSON para Redis");
        Console.WriteLine("===========================");

        var filePath = GetInputFilePath("Digite o caminho do arquivo JSON");
        if (string.IsNullOrEmpty(filePath)) return;

        Console.Write("Digite o campo chave (padrão: id): ");
        var keyField = Console.ReadLine() ?? "id";

        Console.Write("Digite a chave do Redis (padrão: data): ");
        var redisKey = Console.ReadLine() ?? "data";

        await _jsonToRedisService.ExecuteAsync(filePath, keyField, redisKey);
        Console.WriteLine("\nArquivo processado com sucesso!");
    }

    public async Task ProcessJsonFormatterAsync()
    {
        Console.WriteLine("\nFormatação de Arquivo JSON");
        Console.WriteLine("=========================");

        var filePath = GetInputFilePath("Digite o caminho do arquivo JSON");
        if (string.IsNullOrEmpty(filePath)) return;

        await _jsonFormatterService.FormatJsonFileAsync(filePath);
        Console.WriteLine("\nArquivo formatado com sucesso!");
    }

    public async Task ProcessSqlFileAsync()
    {
        Console.WriteLine("\nProcessamento de Arquivo SQL");
        Console.WriteLine("===========================");

        var filePath = GetInputFilePath("Digite o caminho do arquivo SQL");
        if (string.IsNullOrEmpty(filePath)) return;

        Console.WriteLine("\nOpções disponíveis:");
        Console.WriteLine("1. Remover campo");
        Console.WriteLine("2. Executar instruções SQL");
        Console.Write("\nEscolha uma opção: ");

        if (!int.TryParse(Console.ReadLine(), out int subOption))
        {
            Console.WriteLine("Opção inválida!");
            return;
        }

        switch (subOption)
        {
            case 1:
                Console.Write("\nDigite o nome do campo a ser removido: ");
                var fieldName = Console.ReadLine();
                if (string.IsNullOrEmpty(fieldName))
                {
                    Console.WriteLine("Nome do campo inválido!");
                    return;
                }

                var outputPath = await _sqlFileService.RemoveFieldFromSqlFileAsync(filePath, fieldName);
                Console.WriteLine($"\nArquivo processado com sucesso!");
                Console.WriteLine($"Novo arquivo salvo em: {outputPath}");
                break;
            case 2:
                var (success, logPath) = await _sqlFileService.ExecuteSqlFileAsync(filePath);
                if (success)
                {
                    Console.WriteLine("\nTodas as instruções SQL foram executadas com sucesso!");
                }
                else
                {
                    Console.WriteLine("\nAlgumas instruções SQL falharam durante a execução.");
                    Console.WriteLine($"Detalhes dos erros foram salvos em: {logPath}");
                }
                break;
            default:
                Console.WriteLine("Opção inválida!");
                break;
        }
    }

    private void DisplayImportResult(ImportResult result)
    {
        Console.WriteLine("\nResultado da Importação:");
        Console.WriteLine($"Total de registros: {result.TotalRecords:N0}");
        Console.WriteLine($"Registros inseridos: {result.InsertedRecords:N0}");
        Console.WriteLine($"Total de lotes: {result.TotalBatches:N0}");
        Console.WriteLine($"Lotes com falha: {result.FailedBatches:N0}");
        Console.WriteLine($"Duração: {result.DurationInSeconds:N2} segundos");
        Console.WriteLine($"Registros por segundo: {result.RecordsPerSecond:N2}");
    }

    private string GetInputFilePath(string prompt)
    {
        Console.Write(prompt + ": ");
        return Console.ReadLine();
    }
}