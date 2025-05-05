using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ToolBox.Models;
using Microsoft.Extensions.Logging;

namespace ToolBox.Services;

public class ClienteDataProcessor
{
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<ClienteDataProcessor> _logger;

    public ClienteDataProcessor(
        IProgressBarService progressBarService,
        ILogger<ClienteDataProcessor> logger)
    {
        _progressBarService = progressBarService;
        _logger = logger;
    }

    public async Task ProcessarArquivos(string arquivoCsv, string arquivoJsonl, string arquivoSaida)
    {
        var cpfs = new HashSet<string>();
        
        // Lê os CPFs do arquivo CSV
        using (var reader = new StreamReader(arquivoCsv))
        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            var totalLines = File.ReadLines(arquivoCsv).Count();
            _progressBarService.InitializeProgressBar(totalLines, "Lendo CPFs do arquivo CSV");
            var processedLines = 0;

            while (await csv.ReadAsync())
            {
                var cpf = csv.GetField(0);
                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    cpfs.Add(cpf);
                }
                processedLines++;
                _progressBarService.UpdateProgress(processedLines, $"Processados {processedLines:N0} de {totalLines:N0} CPFs");
            }
        }

        // Processa o arquivo JSONL e cria o arquivo de saída
        using (var reader = new StreamReader(arquivoJsonl))
        using (var writer = new StreamWriter(arquivoSaida))
        {
            var totalLines = File.ReadLines(arquivoJsonl).Count();
            _progressBarService.InitializeProgressBar(totalLines, "Processando arquivo JSONL");
            var processedLines = 0;
            var matchedLines = 0;
            var invalidLines = 0;

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var clienteData = JsonSerializer.Deserialize<ClienteData>(line);
                    if (clienteData != null && cpfs.Contains(clienteData.Cpf))
                    {
                        await writer.WriteLineAsync(line);
                        matchedLines++;
                    }
                }
                catch (JsonException)
                {
                    invalidLines++;
                    // Ignora linhas inválidas
                    continue;
                }
                finally
                {
                    processedLines++;
                    _progressBarService.UpdateProgress(processedLines, 
                        $"Processadas {processedLines:N0} de {totalLines:N0} linhas - {matchedLines:N0} CPFs encontrados - {invalidLines:N0} linhas inválidas");
                }
            }
        }
    }
} 