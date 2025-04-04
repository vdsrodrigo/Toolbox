using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ToolBox.Services;

public class JsonFormatterService : IJsonFormatterService
{
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<JsonFormatterService> _logger;

    public JsonFormatterService(
        IProgressBarService progressBarService,
        ILogger<JsonFormatterService> logger)
    {
        _progressBarService = progressBarService;
        _logger = logger;
    }

    public async Task<string> ExtractFieldsToNewFileAsync(
        string jsonlFilePath, 
        string[] fieldsToExtract)
    {
        if (!File.Exists(jsonlFilePath))
        {
            throw new FileNotFoundException($"Arquivo JSONL não encontrado: {jsonlFilePath}");
        }

        if (fieldsToExtract == null || fieldsToExtract.Length == 0)
        {
            throw new ArgumentException("É necessário especificar pelo menos um campo para extração");
        }

        _logger.LogInformation($"Iniciando extração de campos do arquivo: {jsonlFilePath}");
        _logger.LogInformation($"Campos a serem extraídos: {string.Join(", ", fieldsToExtract)}");

        // Define o nome do arquivo de saída
        string directory = Path.GetDirectoryName(jsonlFilePath);
        string fileName = Path.GetFileNameWithoutExtension(jsonlFilePath);
        string extension = Path.GetExtension(jsonlFilePath);
        string outputFilePath = Path.Combine(directory, $"{fileName}_formatted{extension}");

        // Conta linhas do arquivo para calcular o progresso
        int totalLines = CountLinesInFile(jsonlFilePath);
        
        _progressBarService.InitializeProgressBar(totalLines, "Formatando arquivo JSONL");

        // Conta as linhas processadas
        int processedLines = 0;
        int successfulLines = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var inputStream = File.OpenRead(jsonlFilePath);
            using var reader = new StreamReader(inputStream);
            using var outputStream = File.Create(outputFilePath);
            using var writer = new StreamWriter(outputStream);
            
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                processedLines++;
                
                try
                {
                    // Tenta desserializar a linha JSONL
                    using JsonDocument jsonDocument = JsonDocument.Parse(line);
                    var outputObject = new Dictionary<string, JsonElement>();
                    
                    // Extrai os campos solicitados
                    foreach (string field in fieldsToExtract)
                    {
                        if (jsonDocument.RootElement.TryGetProperty(field, out JsonElement value))
                        {
                            outputObject[field] = value.Clone();
                        }
                    }

                    // Serializa o objeto de saída e escreve no arquivo
                    string outputLine = JsonSerializer.Serialize(outputObject);
                    await writer.WriteLineAsync(outputLine);
                    successfulLines++;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha: {Line}", line);
                }
                
                // Atualiza a barra de progresso
                _progressBarService.UpdateProgress(processedLines, $"Processado {processedLines:N0} de {totalLines:N0} linhas");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar o arquivo JSONL: {jsonlFilePath}");
            throw;
        }

        stopwatch.Stop();
        _logger.LogInformation($"Processamento concluído. Linhas processadas: {processedLines}, Linhas extraídas com sucesso: {successfulLines}");
        _logger.LogInformation($"Tempo total: {stopwatch.Elapsed.TotalSeconds:N2} segundos");
        _logger.LogInformation($"Arquivo de saída: {outputFilePath}");

        return outputFilePath;
    }
    
    private int CountLinesInFile(string filePath)
    {
        int lineCount = 0;
        using (var reader = new StreamReader(filePath))
        {
            while (reader.ReadLine() != null)
            {
                lineCount++;
            }
        }
        return lineCount;
    }

    public async Task FormatJsonFileAsync(string filePath)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_formatted{Path.GetExtension(filePath)}"
        );

        var json = await File.ReadAllTextAsync(filePath);
        var totalLines = json.Count(c => c == '\n') + 1;
        var processedLines = 0;

        _progressBarService.InitializeProgressBar(totalLines, "Formatando arquivo JSON");

        try
        {
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(json);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var formattedJson = JsonSerializer.Serialize(jsonObject, options);
            await File.WriteAllTextAsync(outputPath, formattedJson);

            _progressBarService.UpdateProgress(totalLines, "Formatação concluída");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao formatar arquivo JSON");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }
}