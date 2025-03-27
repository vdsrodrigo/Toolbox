namespace ToolBox.Services;

public interface IConsoleService
{
    // ... métodos existentes
    
    void UpdateProgress(double percentage, int matchesFound, int linesProcessed, TimeSpan remainingTime);
}
