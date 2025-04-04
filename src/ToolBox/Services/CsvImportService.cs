using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Globalization;
using ToolBox.Configuration;
using ToolBox.Models;
using ToolBox.Domain.Entities;

namespace ToolBox.Services;

public class CsvImportService : ICsvImportService
{
    private readonly IMongoCollection<Ledger> _collection;
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<CsvImportService> _logger;

    public CsvImportService(
        IMongoDatabase database,
        IOptions<MongoDbSettings> mongoDbSettings,
        IProgressBarService progressBarService,
        ILogger<CsvImportService> logger)
    {
        _collection = database.GetCollection<Ledger>(mongoDbSettings.Value.CollectionName);
        _progressBarService = progressBarService;
        _logger = logger;
    }

    public async Task<ImportResult> ImportCsvToMongoAsync(string csvFilePath)
    {
        var result = new ImportResult();
        var batchSize = 1000;
        var batch = new List<Ledger>();
        var totalLines = File.ReadLines(csvFilePath).Count();
        var totalBatches = (int)Math.Ceiling(totalLines / (double)batchSize);
        var currentBatch = 0;
        var startTime = DateTime.Now;

        _progressBarService.InitializeProgressBar(totalBatches, "Importando CSV para MongoDB");

        try
        {
            using var reader = new StreamReader(csvFilePath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                HeaderValidated = null,
                PrepareHeaderForMatch = args => args.Header.ToUpper()
            };

            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<CsvMemberMap>();

            await foreach (var record in csv.GetRecordsAsync<CsvMember>())
            {
                var ledger = Ledger.Create(record.MemberPeoMemNum, DateTime.UtcNow);
                batch.Add(ledger);
                result.TotalRecords++;

                if (batch.Count >= batchSize)
                {
                    await InsertBatchAsync(batch, result);
                    result.InsertedRecords += batch.Count;
                    result.TotalBatches++;
                    batch.Clear();
                    currentBatch++;
                    _progressBarService.UpdateProgress(currentBatch, $"Processado {result.TotalRecords:N0} registros");
                }
            }

            if (batch.Any())
            {
                await InsertBatchAsync(batch, result);
                result.InsertedRecords += batch.Count;
                result.TotalBatches++;
                currentBatch++;
                _progressBarService.UpdateProgress(currentBatch, $"Processado {result.TotalRecords:N0} registros");
            }

            result.DurationInSeconds = (DateTime.Now - startTime).TotalSeconds;
            result.RecordsPerSecond = result.TotalRecords / result.DurationInSeconds;

            return result;
        }
        finally
        {
            _progressBarService.Dispose();
        }
    }

    private async Task InsertBatchAsync(List<Ledger> batch, ImportResult result)
    {
        try
        {
            await _collection.InsertManyAsync(batch);
        }
        catch (Exception ex)
        {
            result.FailedBatches++;
            _logger.LogError(ex, "Erro ao inserir lote de {Count} registros", batch.Count);
            throw;
        }
    }
}

public sealed class CsvMemberMap : ClassMap<CsvMember>
{
    public CsvMemberMap()
    {
        Map(m => m.LoyMemberId).Name("LOYMEMBERID");
        Map(m => m.MemberPeoMemNum).Name("MEMBERPEOMEMNUM");
    }
}