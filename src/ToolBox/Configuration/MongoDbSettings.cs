namespace ToolBox.Configuration;

public class MongoDbSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public string CollectionName { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrEmpty(ConnectionString) ||
            string.IsNullOrEmpty(DatabaseName) ||
            string.IsNullOrEmpty(CollectionName))
        {
            throw new ArgumentException("MongoDB configuration is incomplete. Check appsettings.json");
        }
    }
}