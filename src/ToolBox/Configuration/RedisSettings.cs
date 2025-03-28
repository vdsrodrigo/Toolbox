namespace ToolBox.Configuration;

public class RedisSettings
{
    public string InstanceName { get; }

    public RedisSettings(string instanceName)
    {
        InstanceName = instanceName;
    }
}
