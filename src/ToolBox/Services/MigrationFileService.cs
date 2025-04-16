using Microsoft.Extensions.Logging;
using Npgsql;
using ToolBox.Configuration;
using ToolBox.Models;

namespace ToolBox.Services;

public interface IMigrationFileService
{
    Task<string> ProcessMigrationFileAsync(string filePath, string[]? ledgerCustomerIds = null, bool executeAfterGeneration = false);
}

public class MigrationFileService : IMigrationFileService
{
    private readonly ILogger<MigrationFileService> _logger;
    private readonly IProgressBarService _progressBarService;
    private readonly ISqlFileService _sqlFileService;
    private readonly IPostgresSettings _postgresSettings;

    public MigrationFileService(
        ILogger<MigrationFileService> logger,
        IProgressBarService progressBarService,
        ISqlFileService sqlFileService,
        IPostgresSettings postgresSettings)
    {
        _logger = logger;
        _progressBarService = progressBarService;
        _sqlFileService = sqlFileService;
        _postgresSettings = postgresSettings;
    }

    public async Task<string> ProcessMigrationFileAsync(string filePath, string[]? ledgerCustomerIds = null, bool executeAfterGeneration = false)
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

            // Se foram fornecidos IDs específicos para filtrar
            if (ledgerCustomerIds != null && ledgerCustomerIds.Length > 0)
            {
                uniqueLedgerCustomerIds = uniqueLedgerCustomerIds.Intersect(ledgerCustomerIds).ToHashSet();
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

            if (executeAfterGeneration)
            {
                await EnsureViewExistsAsync();
                var (success, logPath) = await _sqlFileService.ExecuteSqlFileAsync(outputPath);
                if (!success)
                {
                    _logger.LogError("Alguns comandos SQL falharam durante a execução. Verifique o arquivo de log: {LogPath}", logPath);
                }
            }

            // Gera instruções do MongoDB
            if (uniqueLedgerCustomerIds.Any())
            {
                var mongoOutputPath = await GenerateMongoDbInstructionsAsync(filePath, uniqueLedgerCustomerIds);
            }

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

    private async Task EnsureViewExistsAsync()
    {
        const string viewName = "vw_total_pontos_ff";
        const string checkViewQuery = "SELECT EXISTS (SELECT FROM pg_views WHERE viewname = @viewName)";
        const string createViewQuery = @"
            create view vw_total_pontos_ff(nome, pontos, ledger_customer_id, cpf) as
            SELECT CASE ledger_customer_id
                       WHEN '67c242feea468e15d4c30901'::text THEN 'Aline Curcino'::text
                       WHEN '67c3a9a49920699f08f8a25b'::text THEN 'Bianca Maria dos Santos'::text
                       WHEN '67c3b4729920699f08f8cc19'::text THEN 'Carlos Freitas'::text
                       WHEN '67c27218905be3ebd0390c17'::text THEN 'Cleia Freixo'::text
                       WHEN '67c21bac1960624fbcf6ad52'::text THEN 'Danilo Kawanish'::text
                       WHEN '67c259c5ea468e15d4c3f498'::text THEN 'David'::text
                       WHEN '67c265b0431318f9333515b7'::text THEN 'Denis'::text
                       WHEN '67c217872515ef1f4b44943b'::text THEN 'Eduardo Armbrust'::text
                       WHEN '67c22619905be3ebd035d3b9'::text THEN 'Eduardo Leonidas'::text
                       WHEN '67c22312ea468e15d4c16716'::text THEN 'Fernando Santana Barbosa'::text
                       WHEN '67c24fb4722bb335f27cb658'::text THEN 'Giulliana Brenat'::text
                       WHEN '67c3db139b10421f836b7abf'::text THEN 'Henrique Correa'::text
                       WHEN '67c26238905be3ebd0387357'::text THEN 'Jorge Proença'::text
                       WHEN '67c22b91ea468e15d4c1d44a'::text THEN 'Josiane Pereira'::text
                       WHEN '67c273709b2889adba614237'::text THEN 'Karen Pardo'::text
                       WHEN '67c28b05905be3ebd039e670'::text THEN 'Liliane Delgado'::text
                       WHEN '67c3509e286925f614e1c7a2'::text THEN 'Luan Guerra'::text
                       WHEN '67c31938722bb335f27d5cf2'::text THEN 'Lucas Marinho Botelho'::text
                       WHEN '67c22d27682a1df62de3fe06'::text THEN 'Luiz Suzuki'::text
                       WHEN '67c237f8431318f9333319d0'::text THEN 'Mariana Carmo'::text
                       WHEN '67c45d748c42e9bc67152cf1'::text THEN 'Nathalia Barros'::text
                       WHEN '67c51b6c8c42e9bc6716b7b6'::text THEN 'Nerly Gama'::text
                       WHEN '67c387fc8c42e9bc67125e82'::text THEN 'Paula Vieira Moreira Lazzeri'::text
                       WHEN '67c2203e431318f93331df44'::text THEN 'Paulo Cesar Correia de Melo'::text
                       WHEN '67c23b9d722bb335f27c86c4'::text THEN 'Rafael Santos'::text
                       WHEN '67c21a89cb2eae3fccbd0d14'::text THEN 'Renata Ceschin'::text
                       WHEN '67c24ce4b40d8d49e68a6eaa'::text THEN 'Rodrigo Simioli Costa'::text
                       WHEN '67c26cb5ea468e15d4c4bf9c'::text THEN 'Suelen Silva'::text
                       WHEN '67c22f81ea468e15d4c20a90'::text THEN 'Tiago Rosa'::text
                       WHEN '67c46b779b10421f836d1b74'::text THEN 'Valter Viana'::text
                       WHEN '67c2537f682a1df62de5a95b'::text THEN 'Victor França'::text
                       ELSE NULL::text
                       END              AS nome,
                   sum(available_value) AS pontos,
                   ledger_customer_id,
                   CASE ledger_customer_id
                       WHEN '67c242feea468e15d4c30901'::text THEN '31812947801'::text
                       WHEN '67c3a9a49920699f08f8a25b'::text THEN '41719009805'::text
                       WHEN '67c3b4729920699f08f8cc19'::text THEN '42909473449'::text
                       WHEN '67c27218905be3ebd0390c17'::text THEN '34899107889'::text
                       WHEN '67c21bac1960624fbcf6ad52'::text THEN '31273654897'::text
                       WHEN '67c259c5ea468e15d4c3f498'::text THEN '17991926877'::text
                       WHEN '67c265b0431318f9333515b7'::text THEN '24614568874'::text
                       WHEN '67c217872515ef1f4b44943b'::text THEN '30683103857'::text
                       WHEN '67c22619905be3ebd035d3b9'::text THEN '18377448823'::text
                       WHEN '67c22312ea468e15d4c16716'::text THEN '40516055828'::text
                       WHEN '67c24fb4722bb335f27cb658'::text THEN '41611757878'::text
                       WHEN '67c3db139b10421f836b7abf'::text THEN '29995139847'::text
                       WHEN '67c26238905be3ebd0387357'::text THEN '08170463807'::text
                       WHEN '67c22b91ea468e15d4c1d44a'::text THEN '10570976677'::text
                       WHEN '67c273709b2889adba614237'::text THEN '38019332880'::text
                       WHEN '67c28b05905be3ebd039e670'::text THEN '19276782850'::text
                       WHEN '67c3509e286925f614e1c7a2'::text THEN '37612302810'::text
                       WHEN '67c31938722bb335f27d5cf2'::text THEN '41526928809'::text
                       WHEN '67c22d27682a1df62de3fe06'::text THEN '27645034823'::text
                       WHEN '67c237f8431318f9333319d0'::text THEN '33333728869'::text
                       WHEN '67c45d748c42e9bc67152cf1'::text THEN '35130447808'::text
                       WHEN '67c51b6c8c42e9bc6716b7b6'::text THEN '27148137802'::text
                       WHEN '67c387fc8c42e9bc67125e82'::text THEN '31164438824'::text
                       WHEN '67c2203e431318f93331df44'::text THEN '22904396810'::text
                       WHEN '67c23b9d722bb335f27c86c4'::text THEN '11047749769'::text
                       WHEN '67c21a89cb2eae3fccbd0d14'::text THEN '12612668896'::text
                       WHEN '67c24ce4b40d8d49e68a6eaa'::text THEN '38374061804'::text
                       WHEN '67c26cb5ea468e15d4c4bf9c'::text THEN '37038756826'::text
                       WHEN '67c22f81ea468e15d4c20a90'::text THEN '38693099892'::text
                       WHEN '67c46b779b10421f836d1b74'::text THEN '13322310809'::text
                       WHEN '67c2537f682a1df62de5a95b'::text THEN '46683782830'::text
                       ELSE NULL::text
                       END              AS cpf
            FROM accrual
            WHERE ledger_customer_id = ANY
                  (ARRAY ['67c242feea468e15d4c30901'::text, '67c3a9a49920699f08f8a25b'::text, '67c3b4729920699f08f8cc19'::text, '67c27218905be3ebd0390c17'::text, '67c21bac1960624fbcf6ad52'::text, '67c259c5ea468e15d4c3f498'::text, '67c265b0431318f9333515b7'::text, '67c217872515ef1f4b44943b'::text, '67c22619905be3ebd035d3b9'::text, '67c22312ea468e15d4c16716'::text, '67c24fb4722bb335f27cb658'::text, '67c3db139b10421f836b7abf'::text, '67c26238905be3ebd0387357'::text, '67c22b91ea468e15d4c1d44a'::text, '67c273709b2889adba614237'::text, '67c28b05905be3ebd039e670'::text, '67c3509e286925f614e1c7a2'::text, '67c31938722bb335f27d5cf2'::text, '67c22d27682a1df62de3fe06'::text, '67c237f8431318f9333319d0'::text, '67c45d748c42e9bc67152cf1'::text, '67c51b6c8c42e9bc6716b7b6'::text, '67c387fc8c42e9bc67125e82'::text, '67c2203e431318f93331df44'::text, '67c23b9d722bb335f27c86c4'::text, '67c21a89cb2eae3fccbd0d14'::text, '67c24ce4b40d8d49e68a6eaa'::text, '67c26cb5ea468e15d4c4bf9c'::text, '67c22f81ea468e15d4c20a90'::text, '67c46b779b10421f836d1b74'::text, '67c2537f682a1df62de5a95b'::text])
            GROUP BY ledger_customer_id,
                     (
                         CASE ledger_customer_id
                             WHEN '67c242feea468e15d4c30901'::text THEN 'Aline Curcino'::text
                             WHEN '67c3a9a49920699f08f8a25b'::text THEN 'Bianca Maria dos Santos'::text
                             WHEN '67c3b4729920699f08f8cc19'::text THEN 'Carlos Freitas'::text
                             WHEN '67c27218905be3ebd0390c17'::text THEN 'Cleia Freixo'::text
                             WHEN '67c21bac1960624fbcf6ad52'::text THEN 'Danilo Kawanish'::text
                             WHEN '67c259c5ea468e15d4c3f498'::text THEN 'David'::text
                             WHEN '67c265b0431318f9333515b7'::text THEN 'Denis'::text
                             WHEN '67c217872515ef1f4b44943b'::text THEN 'Eduardo Armbrust'::text
                             WHEN '67c22619905be3ebd035d3b9'::text THEN 'Eduardo Leonidas'::text
                             WHEN '67c22312ea468e15d4c16716'::text THEN 'Fernando Santana Barbosa'::text
                             WHEN '67c24fb4722bb335f27cb658'::text THEN 'Giulliana Brenat'::text
                             WHEN '67c3db139b10421f836b7abf'::text THEN 'Henrique Correa'::text
                             WHEN '67c26238905be3ebd0387357'::text THEN 'Jorge Proença'::text
                             WHEN '67c22b91ea468e15d4c1d44a'::text THEN 'Josiane Pereira'::text
                             WHEN '67c273709b2889adba614237'::text THEN 'Karen Pardo'::text
                             WHEN '67c28b05905be3ebd039e670'::text THEN 'Liliane Delgado'::text
                             WHEN '67c3509e286925f614e1c7a2'::text THEN 'Luan Guerra'::text
                             WHEN '67c31938722bb335f27d5cf2'::text THEN 'Lucas Marinho Botelho'::text
                             WHEN '67c22d27682a1df62de3fe06'::text THEN 'Luiz Suzuki'::text
                             WHEN '67c237f8431318f9333319d0'::text THEN 'Mariana Carmo'::text
                             WHEN '67c45d748c42e9bc67152cf1'::text THEN 'Nathalia Barros'::text
                             WHEN '67c51b6c8c42e9bc6716b7b6'::text THEN 'Nerly Gama'::text
                             WHEN '67c387fc8c42e9bc67125e82'::text THEN 'Paula Vieira Moreira Lazzeri'::text
                             WHEN '67c2203e431318f93331df44'::text THEN 'Paulo Cesar Correia de Melo'::text
                             WHEN '67c23b9d722bb335f27c86c4'::text THEN 'Rafael Santos'::text
                             WHEN '67c21a89cb2eae3fccbd0d14'::text THEN 'Renata Ceschin'::text
                             WHEN '67c24ce4b40d8d49e68a6eaa'::text THEN 'Rodrigo Simioli Costa'::text
                             WHEN '67c26cb5ea468e15d4c4bf9c'::text THEN 'Suelen Silva'::text
                             WHEN '67c22f81ea468e15d4c20a90'::text THEN 'Tiago Rosa'::text
                             WHEN '67c46b779b10421f836d1b74'::text THEN 'Valter Viana'::text
                             WHEN '67c2537f682a1df62de5a95b'::text THEN 'Victor França'::text
                             ELSE NULL::text
                             END)
            ORDER BY (
                         CASE ledger_customer_id
                             WHEN '67c242feea468e15d4c30901'::text THEN 'Aline Curcino'::text
                             WHEN '67c3a9a49920699f08f8a25b'::text THEN 'Bianca Maria dos Santos'::text
                             WHEN '67c3b4729920699f08f8cc19'::text THEN 'Carlos Freitas'::text
                             WHEN '67c27218905be3ebd0390c17'::text THEN 'Cleia Freixo'::text
                             WHEN '67c21bac1960624fbcf6ad52'::text THEN 'Danilo Kawanish'::text
                             WHEN '67c259c5ea468e15d4c3f498'::text THEN 'David'::text
                             WHEN '67c265b0431318f9333515b7'::text THEN 'Denis'::text
                             WHEN '67c217872515ef1f4b44943b'::text THEN 'Eduardo Armbrust'::text
                             WHEN '67c22619905be3ebd035d3b9'::text THEN 'Eduardo Leonidas'::text
                             WHEN '67c22312ea468e15d4c16716'::text THEN 'Fernando Santana Barbosa'::text
                             WHEN '67c24fb4722bb335f27cb658'::text THEN 'Giulliana Brenat'::text
                             WHEN '67c3db139b10421f836b7abf'::text THEN 'Henrique Correa'::text
                             WHEN '67c26238905be3ebd0387357'::text THEN 'Jorge Proença'::text
                             WHEN '67c22b91ea468e15d4c1d44a'::text THEN 'Josiane Pereira'::text
                             WHEN '67c273709b2889adba614237'::text THEN 'Karen Pardo'::text
                             WHEN '67c28b05905be3ebd039e670'::text THEN 'Liliane Delgado'::text
                             WHEN '67c3509e286925f614e1c7a2'::text THEN 'Luan Guerra'::text
                             WHEN '67c31938722bb335f27d5cf2'::text THEN 'Lucas Marinho Botelho'::text
                             WHEN '67c22d27682a1df62de3fe06'::text THEN 'Luiz Suzuki'::text
                             WHEN '67c237f8431318f9333319d0'::text THEN 'Mariana Carmo'::text
                             WHEN '67c45d748c42e9bc67152cf1'::text THEN 'Nathalia Barros'::text
                             WHEN '67c51b6c8c42e9bc6716b7b6'::text THEN 'Nerly Gama'::text
                             WHEN '67c387fc8c42e9bc67125e82'::text THEN 'Paula Vieira Moreira Lazzeri'::text
                             WHEN '67c2203e431318f93331df44'::text THEN 'Paulo Cesar Correia de Melo'::text
                             WHEN '67c23b9d722bb335f27c86c4'::text THEN 'Rafael Santos'::text
                             WHEN '67c21a89cb2eae3fccbd0d14'::text THEN 'Renata Ceschin'::text
                             WHEN '67c24ce4b40d8d49e68a6eaa'::text THEN 'Rodrigo Simioli Costa'::text
                             WHEN '67c26cb5ea468e15d4c4bf9c'::text THEN 'Suelen Silva'::text
                             WHEN '67c22f81ea468e15d4c20a90'::text THEN 'Tiago Rosa'::text
                             WHEN '67c46b779b10421f836d1b74'::text THEN 'Valter Viana'::text
                             WHEN '67c2537f682a1df62de5a95b'::text THEN 'Victor França'::text
                             ELSE NULL::text
                             END);";

        try
        {
            using var connection = new NpgsqlConnection(_postgresSettings.ConnectionString);
            await connection.OpenAsync();

            using var checkCommand = new NpgsqlCommand(checkViewQuery, connection);
            checkCommand.Parameters.AddWithValue("@viewName", viewName);
            var viewExists = (bool)await checkCommand.ExecuteScalarAsync();

            if (!viewExists)
            {
                using var createCommand = new NpgsqlCommand(createViewQuery, connection);
                await createCommand.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar/criar view vw_total_pontos_ff");
            throw;
        }
    }

    private async Task<string> GenerateMongoDbInstructionsAsync(string filePath, HashSet<string> uniqueLedgerCustomerIds)
    {
        var mongoOutputPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileNameWithoutExtension(filePath)}_mongo_instructions.js"
        );

        try
        {
            // Consulta a view para obter CPFs e pontos
            var cpfsAndPoints = await GetCpfsAndPointsFromViewAsync(uniqueLedgerCustomerIds);

            using var writer = new StreamWriter(mongoOutputPath);
            await writer.WriteLineAsync("// Instruções para atualizar pontos no MongoDB");
            
            // Instruções para a collection ledgers
            await writer.WriteLineAsync("\n// Atualizando pontos na collection ledgers");
            foreach (var (cpf, pontos) in cpfsAndPoints)
            {
                await writer.WriteLineAsync($"// Atualizando pontos para o CPF: {cpf}");
                await writer.WriteLineAsync("db.ledgers.updateOne(");
                await writer.WriteLineAsync("    {");
                await writer.WriteLineAsync($"        cpf: \"{cpf}\"");
                await writer.WriteLineAsync("    },");
                await writer.WriteLineAsync("    {");
                await writer.WriteLineAsync("        $set: {");
                await writer.WriteLineAsync($"            points: {pontos},");
                await writer.WriteLineAsync("            pointsBlocked: 0");
                await writer.WriteLineAsync("        }");
                await writer.WriteLineAsync("    }");
                await writer.WriteLineAsync(");\n");
            }

            // Instruções para a collection balances
            await writer.WriteLineAsync("\n// Atualizando pontos na collection balances");
            foreach (var (cpf, pontos) in cpfsAndPoints)
            {
                await writer.WriteLineAsync($"// Atualizando pontos para o CPF: {cpf}");
                await writer.WriteLineAsync("db.balances.updateOne(");
                await writer.WriteLineAsync("    {");
                await writer.WriteLineAsync($"        cpf: \"{cpf}\"");
                await writer.WriteLineAsync("    },");
                await writer.WriteLineAsync("    {");
                await writer.WriteLineAsync("        $set: {");
                await writer.WriteLineAsync($"            points: {pontos},");
                await writer.WriteLineAsync($"            pointsAvailable: {pontos},");
                await writer.WriteLineAsync("            pointsBlocked: 0");
                await writer.WriteLineAsync("        }");
                await writer.WriteLineAsync("    }");
                await writer.WriteLineAsync(");\n");
            }

            return mongoOutputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar instruções do MongoDB");
            throw;
        }
    }

    private async Task<Dictionary<string, decimal>> GetCpfsAndPointsFromViewAsync(HashSet<string> ledgerCustomerIds)
    {
        var result = new Dictionary<string, decimal>();
        
        try
        {
            using var connection = new NpgsqlConnection(_postgresSettings.ConnectionString);
            await connection.OpenAsync();

            var idsList = string.Join(",", ledgerCustomerIds.Select(id => $"'{id}'"));
            var query = $@"
                SELECT cpf, pontos, ledger_customer_id 
                FROM vw_total_pontos_ff 
                WHERE ledger_customer_id IN ({idsList})";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            var foundIds = new HashSet<string>();
            while (await reader.ReadAsync())
            {
                var cpf = reader.GetString(0);
                var pontos = reader.GetDecimal(1);
                var ledgerId = reader.GetString(2);
                result[cpf] = pontos;
                foundIds.Add(ledgerId);
            }

            // Verifica se algum ledger_customer_id não foi encontrado
            var missingIds = ledgerCustomerIds.Except(foundIds).ToList();
            if (missingIds.Any())
            {
                _logger.LogWarning("Os seguintes ledger_customer_ids não foram encontrados na view: {MissingIds}", 
                    string.Join(", ", missingIds));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar view vw_total_pontos_ff");
            throw;
        }

        return result;
    }
} 