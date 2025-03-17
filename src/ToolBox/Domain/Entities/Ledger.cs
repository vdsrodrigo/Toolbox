using ToolBox.Domain.Exceptions;

namespace ToolBox.Domain.Entities;

public class Ledger
{
    public string Cpf { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int LedgerTypeId { get; private set; }
    public int? Points { get; private set; }
    public int PointsBlocked { get; private set; }
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