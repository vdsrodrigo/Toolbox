using ShellProgressBar;

namespace ToolBox.Services;

public class ProgressBarService : IProgressBarService
{
    private ProgressBar? _progressBar;
    private ChildProgressBar? _childProgressBar;

    public void InitializeProgressBar(int totalTicks, string message)
    {
        var options = new ProgressBarOptions
        {
            ForegroundColor = ConsoleColor.Yellow,
            BackgroundColor = ConsoleColor.DarkGray,
            ProgressCharacter = '─',
            ProgressBarOnBottom = true,
            ShowEstimatedDuration = true,
            DisplayTimeInRealTime = true,
            EnableTaskBarProgress = true
        };

        _progressBar = new ProgressBar(totalTicks, message, options);
    }

    public void InitializeChildProgressBar(int totalTicks, string message)
    {
        if (_progressBar == null)
            throw new InvalidOperationException("ProgressBar principal não foi inicializado");

        var options = new ProgressBarOptions
        {
            ForegroundColor = ConsoleColor.Green,
            ProgressCharacter = '─',
            ProgressBarOnBottom = true,
            ShowEstimatedDuration = true,
            DisplayTimeInRealTime = true
        };

        _childProgressBar = _progressBar.Spawn(totalTicks, message, options);
    }

    public void UpdateProgress(int currentTick, string? message = null)
    {
        if (_progressBar == null)
            throw new InvalidOperationException("ProgressBar não foi inicializado");

        _progressBar.Tick(currentTick, message);
    }

    public void UpdateChildProgress(int currentTick, string? message = null)
    {
        if (_childProgressBar == null)
            throw new InvalidOperationException("ChildProgressBar não foi inicializado");

        _childProgressBar.Tick(currentTick, message);
    }

    public void Dispose()
    {
        _childProgressBar?.Dispose();
        _progressBar?.Dispose();
    }

    // New methods for the JsonToPostgresService and other services
    public void Initialize(long totalTicks, string message)
    {
        InitializeProgressBar((int)totalTicks, message);
    }

    public void Report(long currentTick, string? message = null)
    {
        UpdateProgress((int)currentTick, message);
    }

    public void Complete()
    {
        Dispose();
    }
}
