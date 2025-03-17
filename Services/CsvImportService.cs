using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ToolBox.Domain.Entities;
using ToolBox.Models;

namespace ToolBox.Services;

public class CsvImportService(
    ILedgerRepository ledgerRepository,
    ICsvReaderService csvReader,
    ILogger<CsvImportService> logger,
    IConfiguration configuration)
{
    private readonly int _batchSize = configuration.GetValue("BatchSize", 1000);

    public async Task<ImportResult> ImportCsvToMongoAsync(string csvFilePath)
    {
        var startTime = DateTime.Now;
        var importResult = new ImportResult
        {
            TotalRecords = 0,
            InsertedRecords = 0,
            TotalBatches = 0,
            FailedBatches = 0,
            DurationMs = 0
        };

        logger.LogInformation($"Starting import from {csvFilePath}");
        logger.LogInformation($"Batch size: {_batchSize}");

        await ledgerRepository.CreateIndexIfNotExistsAsync();

        var batch = new List<Ledger>();
        long recordCount = 0;
        long batchCount = 0;

        await foreach (var ledger in csvReader.ReadLedgersAsync(csvFilePath))
        {
            recordCount++;
            batch.Add(ledger);

            if (batch.Count >= _batchSize)
            {
                batchCount = await ProcessBatch(batch, batchCount, importResult);
            }

            if (recordCount % (_batchSize * 10) == 0)
            {
                logger.LogInformation($"Processed {recordCount:N0} records so far");
            }
        }

        if (batch.Count > 0)
        {
            batchCount = await ProcessBatch(batch, batchCount, importResult);
        }

        importResult.TotalRecords = recordCount;
        importResult.TotalBatches = batchCount;
        importResult.DurationMs = (DateTime.Now - startTime).TotalMilliseconds;

        LogImportResults(importResult);

        return importResult;
    }

    private async Task<long> ProcessBatch(List<Ledger> batch, long batchCount, ImportResult importResult)
    {
        try
        {
            await ledgerRepository.InsertManyAsync(batch);
            importResult.InsertedRecords += batch.Count;
            logger.LogDebug($"Batch {batchCount} with {batch.Count} records inserted successfully");
        }
        catch (Exception ex)
        {
            importResult.FailedBatches++;
            logger.LogError(ex, $"Failed to insert batch {batchCount}");
        }
        finally
        {
            batch.Clear();
        }
        return ++batchCount;
    }

    private void LogImportResults(ImportResult result)
    {
        logger.LogInformation($"Import completed in {result.DurationMs / 1000:N2} seconds");
        logger.LogInformation($"Total records processed: {result.TotalRecords:N0}");
        logger.LogInformation($"Total records imported: {result.InsertedRecords:N0}");
        logger.LogInformation($"Total batches: {result.TotalBatches:N0}");
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