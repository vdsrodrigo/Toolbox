using MongoDB.Bson.Serialization.Attributes;
using ToolBox.Domain.Exceptions;

namespace ToolBox.Domain.Entities;

public class Ledger
{
    [BsonElement("cpf")]
    public string Cpf { get; private set; }
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; private set; }
    [BsonElement("ledgerTypeId")]
    public int LedgerTypeId { get; private set; }
    [BsonElement("points")]
    public int? Points { get; private set; }
    [BsonElement("pointsBlocked")]
    public int PointsBlocked { get; private set; }
    [BsonElement("status")]
    public string Status { get; private set; }

    private Ledger() { } // Para o MongoDB

    public static Ledger Create(string cpf, DateTime createdAt)
    {
        if (string.IsNullOrEmpty(cpf))
            throw new DomainException("CPF não pode ser vazio");

        return new Ledger
        {
            Cpf = cpf,
            CreatedAt = createdAt,
            LedgerTypeId = 1,
            Points = null,
            PointsBlocked = 0,
            Status = "Ativo"
        };
    }
}