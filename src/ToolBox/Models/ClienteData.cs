using System.Text.Json.Serialization;

namespace ToolBox.Models;

public class ClienteData
{
    [JsonPropertyName("cpf")]
    public string Cpf { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; }
} 