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
}

public interface IProgressBarService
{
    void InitializeProgressBar(int totalTicks, string message);
    void InitializeChildProgressBar(int totalTicks, string message);
    void UpdateProgress(int currentTick, string? message = null);
    void UpdateChildProgress(int currentTick, string? message = null);
    void Dispose();
} 