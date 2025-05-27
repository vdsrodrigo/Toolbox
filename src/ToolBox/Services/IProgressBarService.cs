namespace ToolBox.Services;

public interface IProgressBarService
{
    void InitializeProgressBar(int totalTicks, string message);
    void InitializeChildProgressBar(int totalTicks, string message);
    void UpdateProgress(int currentTick, string? message = null);
    void UpdateChildProgress(int currentTick, string? message = null);
    void Dispose();

    // New methods for the JsonToPostgresService and other services
    void Initialize(long totalTicks, string message);
    void Report(long currentTick, string? message = null);
    void Complete();
}
