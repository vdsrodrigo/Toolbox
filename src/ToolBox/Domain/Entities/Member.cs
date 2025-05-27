using System;

namespace ToolBox.Domain.Entities;

public class Member
{
    public int Id { get; set; }
    public string LedgerCustomerId { get; set; }
    public Guid ExternalId { get; set; }
    public string Cpf { get; set; }
    public string LedgerTypeId { get; set; }
    public int? Points { get; set; }
    public int PointsBlocked { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Member() { } // Constructor for Entity Framework and JSONL deserialization

    public static Member Create(string cpf, string ledgerCustomerId)
    {
        if (string.IsNullOrEmpty(cpf))
            throw new Domain.Exceptions.DomainException("CPF não pode ser vazio");

        if (string.IsNullOrEmpty(ledgerCustomerId))
            throw new Domain.Exceptions.DomainException("ID do cliente não pode ser vazio");

        return new Member
        {
            LedgerCustomerId = ledgerCustomerId,
            ExternalId = GenerateUUIDv7(),
            Cpf = cpf,
            LedgerTypeId = "Stix",
            Points = null,
            PointsBlocked = 0,
            Status = "Ativo",
            CreatedAt = DateTime.UtcNow
        };
    }
    public static Guid GenerateUUIDv7()
    {
        // Simplificação do UUID v7 - em produção deve-se usar uma biblioteca específica
        byte[] guidBytes = new byte[16];

        // Preenche os primeiros 6 bytes com o timestamp de milissegundos atual
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] timestampBytes = BitConverter.GetBytes(timestamp);

        // Garantir que estamos usando big-endian para o timestamp
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes, 0, 6);
        }

        Array.Copy(timestampBytes, 0, guidBytes, 0, Math.Min(6, timestampBytes.Length));

        // Versão 7 (0b0111) nos bits 6-9 do byte 6
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x70);

        // Variante RFC 4122 (0b10xx) nos bits 0-1 do byte 8
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        // Preenche o resto com bytes aleatórios
        new Random().NextBytes(guidBytes.AsSpan(9));

        return new Guid(guidBytes);
    }
}
