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
        while ((line = await reader.ReadLineAsync()) != null)
        {
            using var doc = JsonDocument.Parse(line);

            if (!doc.RootElement.TryGetProperty(keyField, out var keyElement) ||
                !doc.RootElement.TryGetProperty(valueField, out var valueElement))
            {
                progressBar.Tick($"Linha inválida, ignorando...");
                continue; // Ignorar caso algum campo não exista nesta linha
            }

            var key = keyElement.ToString();
            var value = valueElement.ToString();

            // Usando a convenção InstanceName para a chave:
            await _redisDb.StringSetAsync($"{_instanceName}:{key}", value);
            totalEntries++;

            progressBar.Tick($"Importado {totalEntries} de {totalLines} registros.");
        }

        return totalEntries;
    }
}