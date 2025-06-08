using System;
using System.Text.Json.Serialization;

namespace ToolBox.Models;

public class JsonMember
{
    [JsonPropertyName("_id")]
    public JsonMemberId? Id { get; set; }

    [JsonPropertyName("cpf")]
    public string? Cpf { get; set; }

    [JsonPropertyName("createdAt")]
    public JsonDateTime? CreatedAt { get; set; }

    [JsonPropertyName("ledgerTypeId")]
    public int LedgerTypeId { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("pointsBlocked")]
    public int PointsBlocked { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class JsonMemberId
{
    [JsonPropertyName("$oid")]
    public string? Oid { get; set; }
}

public class JsonDateTime
{
    [JsonPropertyName("$date")]
    public string? Date { get; set; }
    public DateTime ToDateTime()
    {
        if (Date != null && DateTime.TryParse(Date, out DateTime result))
        {
            // Converter para UTC explicitamente
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }
}
