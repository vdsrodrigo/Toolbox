namespace ToolBox.Services;

public interface IConsoleService
{
    // Métodos para gerenciar o fluxo de interação
    Task ImportCsvToMongoAsync(IServiceProvider serviceProvider);
    Task FormatJsonFileAsync(IServiceProvider serviceProvider);
    Task ReplaceTextInFileAsync(IServiceProvider serviceProvider);
    Task JsonToRedisAsync(IServiceProvider serviceProvider);
}