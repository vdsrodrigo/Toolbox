using ToolBox.Domain.Entities;

namespace ToolBox.Services;

public interface ILedgerRepository
{
    Task InsertManyAsync(List<Ledger> ledgers);
    Task CreateIndexIfNotExistsAsync();
}