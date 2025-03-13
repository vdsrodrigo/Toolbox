using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ToolBox.Models;

public class Ledger
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("cpf")]
    public string Cpf { get; set; } = null!;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("ledgerTypeId")]
    public int LedgerTypeId { get; set; }

    [BsonElement("points")]
    public int? Points { get; set; }

    [BsonElement("pointsBlocked")]
    public int PointsBlocked { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = null!;
}

public class CsvMember
{
    public string LOYMEMBERID { get; set; } = null!;
    public string MEMBERPEOMEMNUM { get; set; } = null!;
}