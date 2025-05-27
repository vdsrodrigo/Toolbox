using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ToolBox.Models;

namespace ToolBox.Services;

public interface IJsonReaderService
{
    Task<long> CountLinesAsync(string filePath);
    IAsyncEnumerable<JsonMember> ReadJsonMembersAsync(string filePath);
}

public class JsonReaderService : IJsonReaderService
{
    private readonly ILogger<JsonReaderService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonReaderService(ILogger<JsonReaderService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<long> CountLinesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("JSONL file not found", filePath);
        }

        long lineCount = 0;

        using (var reader = new StreamReader(filePath))
        {
            while (await reader.ReadLineAsync() != null)
            {
                lineCount++;
            }
        }

        return lineCount;
    }
    public async IAsyncEnumerable<JsonMember> ReadJsonMembersAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("JSONL file not found", filePath);
        }

        using var reader = new StreamReader(filePath);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonMember? member = null;
            try
            {
                member = JsonSerializer.Deserialize<JsonMember>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error deserializing JSON line: {line}");
            }

            if (member != null)
            {
                yield return member;
            }
        }
    }
}
