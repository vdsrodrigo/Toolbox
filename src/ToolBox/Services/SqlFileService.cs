using Microsoft.Extensions.Logging;
using Npgsql;
using ToolBox.Configuration;

namespace ToolBox.Services;

public interface ISqlFileService
{
    Task<string> RemoveFieldFromSqlFileAsync(string filePath, string fieldName);
    Task<(bool success, string logPath)> ExecuteSqlFileAsync(string filePath);
    Task<string> FilterSqlLinesAsync(string filePath, IEnumerable<string> searchStrings);
}

public class SqlFileService : ISqlFileService
{
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<SqlFileService> _logger;
    private readonly PostgresSettings _postgresSettings;

    public SqlFileService(
        IProgressBarService progressBarService,
        ILogger<SqlFileService> logger,
        PostgresSettings postgresSettings)
    {
        _progressBarService = progressBarService;
        _logger = logger;
        _postgresSettings = postgresSettings;
    }

    public async Task<string> RemoveFieldFromSqlFileAsync(string filePath, string fieldName)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_removed_{fieldName}{Path.GetExtension(filePath)}"
        );

        var lines = await File.ReadAllLinesAsync(filePath);
        var totalLines = lines.Length;
        var processedLines = 0;

        _progressBarService.InitializeProgressBar(totalLines, $"Removendo campo '{fieldName}' do arquivo SQL");

        try
        {
            using var writer = new StreamWriter(outputPath);
            foreach (var line in lines)
            {
                var newLine = RemoveFieldFromLine(line, fieldName);
                await writer.WriteLineAsync(newLine);
                
                processedLines++;
                _progressBarService.UpdateProgress(processedLines, $"Processadas {processedLines:N0} de {totalLines:N0} linhas");
            }

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar arquivo SQL");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    private string RemoveFieldFromLine(string line, string fieldName)
    {
        // Verifica se é uma instrução INSERT
        if (line.Trim().StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
        {
            return RemoveFieldFromInsertStatement(line, fieldName);
        }
        
        // Para outros tipos de instruções SQL, usa o método anterior
        var patterns = new[]
        {
            // Padrão para "campo = 'valor'"
            $@"{fieldName}\s*=\s*'[^']*'",
            // Padrão para "campo='valor'"
            $@"{fieldName}\s*=\s*'[^']*'",
            // Padrão para "campo = valor" (sem aspas)
            $@"{fieldName}\s*=\s*[^,)]+",
            // Padrão para "campo=valor" (sem aspas)
            $@"{fieldName}\s*=\s*[^,)]+",
            // Padrão para "campo" (apenas o nome do campo)
            $@"{fieldName}\b"
        };

        var result = line;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, string.Empty);
        }

        // Limpa vírgulas extras que podem ter ficado
        result = System.Text.RegularExpressions.Regex.Replace(result, @",\s*,", ",");
        result = System.Text.RegularExpressions.Regex.Replace(result, @",\s*\)", ")");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\(\s*,", "(");

        return result;
    }

    private string RemoveFieldFromInsertStatement(string line, string fieldName)
    {
        // Divide a instrução em partes: antes dos campos, lista de campos, e lista de valores
        var insertMatch = System.Text.RegularExpressions.Regex.Match(line, @"(INSERT INTO.*?\()(.*?)\)\s*VALUES\s*\((.*)\)");
        
        if (!insertMatch.Success)
        {
            return line; // Se não for possível identificar as partes, retorna a linha original
        }

        var beforeFields = insertMatch.Groups[1].Value;
        var fieldsList = insertMatch.Groups[2].Value;
        var valuesList = insertMatch.Groups[3].Value;

        // Divide as listas em itens individuais
        var fields = SplitSqlList(fieldsList);
        var values = SplitSqlList(valuesList);

        // Encontra o índice do campo a ser removido
        var fieldIndex = -1;
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].Trim().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                fieldIndex = i;
                break;
            }
        }

        // Se o campo não foi encontrado, retorna a linha original
        if (fieldIndex == -1)
        {
            return line;
        }

        // Remove o campo e o valor correspondente
        fields.RemoveAt(fieldIndex);
        if (fieldIndex < values.Count)
        {
            values.RemoveAt(fieldIndex);
        }

        // Reconstrói a instrução SQL
        return $"{beforeFields}{string.Join(", ", fields)}) VALUES ({string.Join(", ", values)})";
    }

    private List<string> SplitSqlList(string list)
    {
        var result = new List<string>();
        var currentItem = new System.Text.StringBuilder();
        var inQuotes = false;
        var inParentheses = 0;
        
        for (int i = 0; i < list.Length; i++)
        {
            var c = list[i];
            
            if (c == '\'' && (i == 0 || list[i-1] != '\\'))
            {
                inQuotes = !inQuotes;
                currentItem.Append(c);
            }
            else if (c == '(' && !inQuotes)
            {
                inParentheses++;
                currentItem.Append(c);
            }
            else if (c == ')' && !inQuotes)
            {
                inParentheses--;
                currentItem.Append(c);
            }
            else if (c == ',' && !inQuotes && inParentheses == 0)
            {
                result.Add(currentItem.ToString().Trim());
                currentItem.Clear();
            }
            else
            {
                currentItem.Append(c);
            }
        }
        
        if (currentItem.Length > 0)
        {
            result.Add(currentItem.ToString().Trim());
        }
        
        return result;
    }

    public async Task<(bool success, string logPath)> ExecuteSqlFileAsync(string filePath)
    {
        var logPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_execution_log_{DateTime.Now:yyyyMMddHHmmss}.txt"
        );

        var lines = await File.ReadAllLinesAsync(filePath);
        var totalLines = lines.Length;
        var processedLines = 0;
        var errorCount = 0;
        var logBuilder = new System.Text.StringBuilder();

        _progressBarService.InitializeProgressBar(totalLines, "Executando instruções SQL");

        try
        {
            await using var connection = new NpgsqlConnection(_postgresSettings.ConnectionString);
            await connection.OpenAsync();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("--"))
                {
                    processedLines++;
                    _progressBarService.UpdateProgress(processedLines, $"Processadas {processedLines:N0} de {totalLines:N0} linhas");
                    continue;
                }

                try
                {
                    using var command = new NpgsqlCommand(line, connection);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var errorMessage = $"Erro na linha {processedLines + 1}: {ex.Message}";
                    _logger.LogError(ex, errorMessage);
                    logBuilder.AppendLine($"Linha {processedLines + 1}: {line}");
                    logBuilder.AppendLine($"Erro: {ex.Message}");
                    logBuilder.AppendLine(new string('-', 80));
                }

                processedLines++;
                _progressBarService.UpdateProgress(processedLines, $"Processadas {processedLines:N0} de {totalLines:N0} linhas");
            }

            await File.WriteAllTextAsync(logPath, logBuilder.ToString());
            return (errorCount == 0, logPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar arquivo SQL");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    public async Task<string> FilterSqlLinesAsync(string filePath, IEnumerable<string> searchStrings)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_filtrado{Path.GetExtension(filePath)}"
        );

        var lines = await File.ReadAllLinesAsync(filePath);
        var totalLines = lines.Length;
        var processedLines = 0;
        var matchedLines = 0;

        _progressBarService.InitializeProgressBar(totalLines, "Filtrando linhas do arquivo SQL");

        try
        {
            using var writer = new StreamWriter(outputPath);
            foreach (var line in lines)
            {
                if (searchStrings.Any(search => line.Contains(search, StringComparison.OrdinalIgnoreCase)))
                {
                    await writer.WriteLineAsync(line);
                    matchedLines++;
                }
                
                processedLines++;
                _progressBarService.UpdateProgress(processedLines, 
                    $"Processadas {processedLines:N0} de {totalLines:N0} linhas. Encontradas: {matchedLines:N0}");
            }

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao filtrar linhas do arquivo SQL");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }
} 