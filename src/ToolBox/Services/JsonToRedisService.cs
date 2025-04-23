using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ToolBox.Services;

public class JsonToRedisService : IJsonToRedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<JsonToRedisService> _logger;

    public JsonToRedisService(
        IConnectionMultiplexer redis,
        IProgressBarService progressBarService,
        ILogger<JsonToRedisService> logger)
    {
        _redis = redis;
        _progressBarService = progressBarService;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string filePath, string keyField, string valueField)
    {
        var db = _redis.GetDatabase();
        
        // Validação mais robusta da conexão
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints.First());
        _logger.LogInformation("Detalhes da conexão Redis:");
        _logger.LogInformation("Endpoint: {Endpoint}", server.EndPoint);
        _logger.LogInformation("Server Type: {ServerType}", server.ServerType);
        _logger.LogInformation("IsConnected: {IsConnected}", server.IsConnected);
        
        if (!server.IsConnected)
        {
            throw new Exception("Não foi possível estabelecer conexão com o Redis. Verifique a connectionString.");
        }
        
        var totalLines = File.ReadLines(filePath).Count();
        var batchSize = 1000;
        var totalBatches = (int)Math.Ceiling(totalLines / (double)batchSize);
        var currentBatch = 0;
        var totalPublished = 0;

        _progressBarService.InitializeProgressBar(totalBatches, "Processando arquivo JSONL");

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream);
            var batch = new List<(string Key, string Value)>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var jsonDoc = JsonDocument.Parse(line);
                    var key = jsonDoc.RootElement.GetProperty(keyField).GetString();
                    var value = jsonDoc.RootElement.GetProperty(valueField).GetString();

                    if (key != null && value != null)
                    {
                        batch.Add((key, value));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar linha: {Line}", line);
                }

                if (batch.Count >= batchSize)
                {
                    await PublishBatchAsync(db, batch);
                    totalPublished += batch.Count;
                    _logger.LogInformation("Batch publicado com sucesso. Total de registros: {TotalPublished}", totalPublished);
                    batch.Clear();
                    currentBatch++;
                    _progressBarService.UpdateProgress(currentBatch, $"Processado {totalPublished:N0} registros");
                }
            }

            if (batch.Any())
            {
                await PublishBatchAsync(db, batch);
                totalPublished += batch.Count;
                _logger.LogInformation("Último batch publicado com sucesso. Total final de registros: {TotalPublished}", totalPublished);
                currentBatch++;
                _progressBarService.UpdateProgress(currentBatch, $"Processado {totalPublished:N0} registros");
            }

            return totalPublished;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução do serviço");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    private async Task PublishBatchAsync(IDatabase db, List<(string Key, string Value)> batch)
    {
        try
        {
            var tasks = batch.Select(item => 
            {
                var fullKey = $"plis-statement:{item.Key}";
                _logger.LogInformation("Publicando no Redis - Chave: {Key}, Valor: {Value}", fullKey, item.Value);
                return db.StringSetAsync(fullKey, item.Value);
            });
            
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);
            _logger.LogInformation("Batch publicado. Sucessos: {SuccessCount}/{Total}", successCount, batch.Count);
            
            // Verifica se as chaves foram realmente salvas
            foreach (var item in batch)
            {
                var fullKey = $"plis-statement:{item.Key}";
                var exists = await db.KeyExistsAsync(fullKey);
                _logger.LogInformation("Verificação pós-inserção - Chave: {Key}, Existe: {Exists}", fullKey, exists);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar batch no Redis");
            throw;
        }
    }
}