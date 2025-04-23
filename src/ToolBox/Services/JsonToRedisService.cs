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
                    batch.Clear();
                    currentBatch++;
                    _progressBarService.UpdateProgress(currentBatch, $"Processado {totalPublished:N0} registros");
                }
            }

            if (batch.Any())
            {
                await PublishBatchAsync(db, batch);
                totalPublished += batch.Count;
                currentBatch++;
                _progressBarService.UpdateProgress(currentBatch, $"Processado {totalPublished:N0} registros");
            }

            return totalPublished;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    private async Task PublishBatchAsync(IDatabase db, List<(string Key, string Value)> batch)
    {
        var tasks = batch.Select(item => db.StringSetAsync($"plis-statement:{item.Key}", item.Value));
        await Task.WhenAll(tasks);
    }
}