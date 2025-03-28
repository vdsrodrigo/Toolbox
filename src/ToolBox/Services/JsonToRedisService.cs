using StackExchange.Redis;
using System.Text.Json;
using ShellProgressBar;
using ToolBox.Configuration;

namespace ToolBox.Services;

public interface IJsonToRedisService
{
    Task<int> ExecuteAsync(string filePath, string keyField, string valueField);
}

public class JsonToRedisService(IConnectionMultiplexer redis, RedisSettings redisSettings) : IJsonToRedisService
{
    private readonly IDatabase _redisDb = redis.GetDatabase();
    private readonly string _instanceName = redisSettings.InstanceName;

    public async Task<int> ExecuteAsync(string filePath, string keyField, string valueField)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Arquivo não encontrado.", filePath);

        int totalEntries = 0;
        int batchSize = 5000; // Tamanho do lote - ajuste conforme necessário

        // Primeiro, contar linhas para definir o progresso total
        using var countStream = File.OpenRead(filePath);
        using var lineCounter = new StreamReader(countStream);
        var totalLines = 0;
        while (await lineCounter.ReadLineAsync() != null)
            totalLines++;

        // Configuração visual da progress bar
        var options = new ProgressBarOptions
        {
            ProgressCharacter = '─',
            ForegroundColor = ConsoleColor.Cyan,
            ForegroundColorDone = ConsoleColor.Green,
            BackgroundColor = ConsoleColor.DarkGray,
            BackgroundCharacter = '─',
            CollapseWhenFinished = false,
            DisplayTimeInRealTime = true
        };

        using var progressBar = new ProgressBar(totalLines, "Importando dados JSON para Redis ✅", options);

        // Iniciar leitura real agora com barra de progresso
        await using var fileStream = File.OpenRead(filePath);
        using var reader = new StreamReader(fileStream);

        string? line;
        List<KeyValuePair<RedisKey, RedisValue>> batch = new(batchSize);
        int processedLines = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            processedLines++;
            using var doc = JsonDocument.Parse(line);

            if (!doc.RootElement.TryGetProperty(keyField, out var keyElement) ||
                !doc.RootElement.TryGetProperty(valueField, out var valueElement))
            {
                // Atualizamos a barra de progresso mesmo para linhas inválidas
                progressBar.Tick($"Linha inválida, ignorando... ({processedLines} de {totalLines})");
                continue; // Ignorar caso algum campo não exista nesta linha
            }

            var key = keyElement.ToString();
            var value = valueElement.ToString();

            // Adicionar ao lote
            batch.Add(new KeyValuePair<RedisKey, RedisValue>($"{_instanceName}:{key}", value));
            totalEntries++;

            // Atualizamos a barra de progresso para cada linha processada
            progressBar.Tick($"Processando... ({processedLines} de {totalLines})");

            // Quando o lote estiver completo, enviar para o Redis
            if (batch.Count >= batchSize)
            {
                await _redisDb.StringSetAsync(batch.ToArray());
                batch.Clear();
            }
        }

        // Processar o último lote (caso exista)
        if (batch.Count > 0)
        {
            await _redisDb.StringSetAsync(batch.ToArray());
        }

        return totalEntries;
    }
}