using Microsoft.Extensions.Logging;

namespace ToolBox.Services;

public interface IMigrationFileService
{
    Task<string> ProcessMigrationFileAsync(string filePath, string[]? ledgerCustomerIds = null);
}

public class MigrationFileService : IMigrationFileService
{
    private readonly ILogger<MigrationFileService> _logger;
    private readonly IProgressBarService _progressBarService;

    public MigrationFileService(
        ILogger<MigrationFileService> logger,
        IProgressBarService progressBarService)
    {
        _logger = logger;
        _progressBarService = progressBarService;
    }

    public async Task<string> ProcessMigrationFileAsync(string filePath, string[]? ledgerCustomerIds = null)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_formatado{Path.GetExtension(filePath)}"
        );

        var lines = await File.ReadAllLinesAsync(filePath);
        var totalLines = lines.Length;
        var processedLines = 0;

        _progressBarService.InitializeProgressBar(totalLines, "Processando arquivo de migração");

        try
        {
            var transactionInserts = new List<string>();
            var accrualInserts = new List<string>();
            var redemptionInserts = new List<string>();
            var deleteStatements = new List<string>();

            // Coleta os IDs únicos de ledger_customer_id
            var uniqueLedgerCustomerIds = new HashSet<string>();
            foreach (var line in lines)
            {
                if (IsInsertStatement(line))
                {
                    var ledgerCustomerId = ExtractLedgerCustomerId(line);
                    if (!string.IsNullOrEmpty(ledgerCustomerId))
                    {
                        uniqueLedgerCustomerIds.Add(ledgerCustomerId);
                    }
                }
            }

            // Gera as instruções DELETE
            if (uniqueLedgerCustomerIds.Any())
            {
                var idsList = string.Join(",", uniqueLedgerCustomerIds.Select(id => $"'{id}'"));
                deleteStatements.Add($"DELETE FROM public.redemption WHERE ledger_customer_id IN ({idsList});");
                deleteStatements.Add($"DELETE FROM public.accrual WHERE ledger_customer_id IN ({idsList});");
                deleteStatements.Add($"DELETE FROM public.transaction WHERE ledger_customer_id IN ({idsList});");
            }

            foreach (var line in lines)
            {
                if (IsInsertStatement(line))
                {
                    if (ledgerCustomerIds != null && ledgerCustomerIds.Length > 0)
                    {
                        if (!ShouldIncludeLine(line, ledgerCustomerIds))
                        {
                            processedLines++;
                            _progressBarService.UpdateProgress(processedLines, 
                                $"Processadas {processedLines:N0} de {totalLines:N0} linhas");
                            continue;
                        }
                    }

                    var formattedLine = FormatSchema(line);
                    if (IsTransactionTable(line))
                    {
                        transactionInserts.Add(formattedLine);
                    }
                    else if (IsAccrualTable(line))
                    {
                        accrualInserts.Add(formattedLine);
                    }
                    else if (IsRedemptionTable(line))
                    {
                        redemptionInserts.Add(formattedLine);
                    }
                }

                processedLines++;
                _progressBarService.UpdateProgress(processedLines, 
                    $"Processadas {processedLines:N0} de {totalLines:N0} linhas");
            }

            // Combina as instruções DELETE com as inserções na ordem correta
            var allStatements = deleteStatements
                .Concat(transactionInserts)
                .Concat(accrualInserts)
                .Concat(redemptionInserts)
                .ToList();

            await File.WriteAllLinesAsync(outputPath, allStatements);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar arquivo de migração");
            throw;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    private bool IsInsertStatement(string line)
    {
        return line.Trim().StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTransactionTable(string line)
    {
        return line.Contains("\"transaction\"", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("public.transaction", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAccrualTable(string line)
    {
        return line.Contains("\"accrual\"", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("public.accrual", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRedemptionTable(string line)
    {
        return line.Contains("\"redemption\"", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("public.redemption", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatSchema(string line)
    {
        // Substitui qualquer schema que não seja 'public' por 'public'
        var formattedLine = System.Text.RegularExpressions.Regex.Replace(
            line,
            @"INSERT\s+INTO\s+[^.]+\.",
            "INSERT INTO public.",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Remove os campos item_number e legacy_redemption_id e seus valores
        formattedLine = RemoveFieldAndValue(formattedLine, "item_number");
        formattedLine = RemoveFieldAndValue(formattedLine, "legacy_redemption_id");

        return formattedLine;
    }

    private string RemoveFieldAndValue(string line, string fieldName)
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

    private bool ShouldIncludeLine(string line, string[] ledgerCustomerIds)
    {
        var ledgerCustomerId = ExtractLedgerCustomerId(line);
        
        if (string.IsNullOrEmpty(ledgerCustomerId))
        {
            return false;
        }

        return ledgerCustomerIds.Contains(ledgerCustomerId);
    }

    private string? ExtractLedgerCustomerId(string line)
    {
        var fieldsMatch = System.Text.RegularExpressions.Regex.Match(line, @"INSERT INTO.*?\((.*?)\)");
        if (!fieldsMatch.Success)
        {
            return null;
        }

        var fields = fieldsMatch.Groups[1].Value.Split(',').Select(f => f.Trim()).ToList();
        var ledgerCustomerIdIndex = fields.FindIndex(f => 
            f.Equals("ledger_customer_id", StringComparison.OrdinalIgnoreCase));

        if (ledgerCustomerIdIndex == -1)
        {
            return null;
        }

        var valuesMatch = System.Text.RegularExpressions.Regex.Match(line, @"VALUES\s*\((.*?)\)");
        if (!valuesMatch.Success)
        {
            return null;
        }

        var values = SplitSqlList(valuesMatch.Groups[1].Value);
        if (ledgerCustomerIdIndex >= values.Count)
        {
            return null;
        }

        return values[ledgerCustomerIdIndex].Trim('\'');
    }
} 