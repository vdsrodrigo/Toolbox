namespace ToolBox.Configuration;

public interface IPostgresSettings
{
    string ConnectionString { get; set; }
}

public class PostgresSettings : IPostgresSettings
{
    public string ConnectionString { get; set; } = string.Empty;
} 