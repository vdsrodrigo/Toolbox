namespace ToolBox.Services;

public interface IJsonFormatterService
{
    Task FormatJsonFileAsync(string filePath);
} 