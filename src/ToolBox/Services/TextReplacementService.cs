using Microsoft.Extensions.Logging;

namespace ToolBox.Services;

public interface ITextReplacementService
{
    Task<string> ReplaceTextInFileAsync(string filePath, string searchText, string? replacementText);
}

public class TextReplacementService : ITextReplacementService
{
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<TextReplacementService> _logger;

    public TextReplacementService(
        IProgressBarService progressBarService,
        ILogger<TextReplacementService> logger)
    {
        _progressBarService = progressBarService;
        _logger = logger;
    }

    public async Task<string> ReplaceTextInFileAsync(string filePath, string searchText, string? replacementText)
    {
        var outputFilePath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_replaced{Path.GetExtension(filePath)}"
        );

        var totalLines = File.ReadLines(filePath).Count();
        var processedLines = 0;
        var matchesFound = 0;

        _progressBarService.InitializeProgressBar(totalLines, "Substituindo texto no arquivo");

        try
        {
            using var inputStream = File.OpenRead(filePath);
            using var reader = new StreamReader(inputStream);
            using var outputStream = File.Create(outputFilePath);
            using var writer = new StreamWriter(outputStream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Contains(searchText))
                {
                    matchesFound++;
                    line = line.Replace(searchText, replacementText ?? string.Empty);
                }

                await writer.WriteLineAsync(line);
                processedLines++;
                _progressBarService.UpdateProgress(processedLines, $"Processado {processedLines:N0} de {totalLines:N0} linhas | {matchesFound:N0} substituições");
            }

            return outputFilePath;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }
} 