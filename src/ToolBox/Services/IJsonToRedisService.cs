namespace ToolBox.Services;

public interface IJsonToRedisService
{
    Task<int> ExecuteAsync(string filePath, string keyField = "id", string redisKey = "data");
} 