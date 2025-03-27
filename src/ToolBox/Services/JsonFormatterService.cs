using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ToolBox.Services;

    public class JsonFormatterService(ILogger<JsonFormatterService> logger)
    {
        public async Task<string> ExtractFieldsToNewFileAsync(
            string jsonlFilePath, 
            string[] fieldsToExtract, 
            IProgress<(int Processed, int Total, TimeSpan ElapsedTime)> progress = null)
        {
            if (!File.Exists(jsonlFilePath))
            {
                throw new FileNotFoundException($"Arquivo JSONL não encontrado: {jsonlFilePath}");
            }

            if (fieldsToExtract == null || fieldsToExtract.Length == 0)
            {
                throw new ArgumentException("É necessário especificar pelo menos um campo para extração");
            }

            logger.LogInformation($"Iniciando extração de campos do arquivo: {jsonlFilePath}");
            logger.LogInformation($"Campos a serem extraídos: {string.Join(", ", fieldsToExtract)}");

            // Define o nome do arquivo de saída
            string directory = Path.GetDirectoryName(jsonlFilePath);
            string fileName = Path.GetFileNameWithoutExtension(jsonlFilePath);
            string extension = Path.GetExtension(jsonlFilePath);
            string outputFilePath = Path.Combine(directory, $"{fileName}_novo{extension}");

            // Conta linhas do arquivo para calcular o progresso
            int totalLines = CountLinesInFile(jsonlFilePath);
            
            // Conta as linhas processadas
            int processedLines = 0;
            int successfulLines = 0;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var reader = new StreamReader(jsonlFilePath);
                using var writer = new StreamWriter(outputFilePath);
                
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
                        logger.LogWarning($"Erro ao processar linha {processedLines}: {ex.Message}");
                    }
                    
                    // Reporta o progresso a cada 100 linhas ou na última linha
                    if (progress != null && (processedLines % 100 == 0 || processedLines == totalLines))
                    {
                        progress.Report((processedLines, totalLines, stopwatch.Elapsed));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Erro ao processar o arquivo JSONL: {jsonlFilePath}");
                throw;
            }

            stopwatch.Stop();
            logger.LogInformation($"Processamento concluído. Linhas processadas: {processedLines}, Linhas extraídas com sucesso: {successfulLines}");
            logger.LogInformation($"Tempo total: {stopwatch.Elapsed.TotalSeconds:N2} segundos");
            logger.LogInformation($"Arquivo de saída: {outputFilePath}");

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
    }