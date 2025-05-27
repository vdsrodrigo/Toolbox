using ToolBox.Domain.Entities;

namespace ToolBox.Services;

public interface IMemberRepository
{
    Task InsertManyAsync(List<Member> members);
    Task CreateTableIfNotExistsAsync();
    Task CreateIndexIfNotExistsAsync();
}
