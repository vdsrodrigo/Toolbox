using Microsoft.Extensions.Logging;

namespace ToolBox.Services
{
    public interface ITextReplacementService
    {
        Task<string> ReplaceTextInFileAsync(string filePath, string searchText, string replacementText);
    }

    public class TextReplacementService : ITextReplacementService
    {
        private readonly ILogger<TextReplacementService> _logger;

        public TextReplacementService(ILogger<TextReplacementService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ReplaceTextInFileAsync(string filePath, string searchText, string replacementText)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");
                }

                var directory = Path.GetDirectoryName(filePath);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                var newFilePath = Path.Combine(directory, $"{fileName}_novo{extension}");

                _logger.LogInformation("Iniciando substituição de texto no arquivo: {FilePath}", filePath);
                _logger.LogInformation("Texto a ser substituído: {SearchText}", searchText);
                _logger.LogInformation("Novo texto: {ReplacementText}", replacementText);

                var lines = await File.ReadAllLinesAsync(filePath);
                var modifiedLines = lines.Select(line => line.Replace(searchText, replacementText));

                await File.WriteAllLinesAsync(newFilePath, modifiedLines);

                _logger.LogInformation("Arquivo processado com sucesso. Novo arquivo salvo em: {NewFilePath}", newFilePath);
                return newFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar o arquivo: {FilePath}", filePath);
                throw;
            }
        }
    }
}