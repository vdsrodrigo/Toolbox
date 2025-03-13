using System;
using System.Collections.Generic;
using ToolBox.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace ToolBox.Services;

public class CsvImportService
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<CsvImportService> _logger;
    private readonly IConfiguration _configuration;
    private int _batchSize;
    private readonly DateTime _defaultCreatedAt = DateTime.UtcNow;

    public CsvImportService(
        MongoDbService mongoDbService,
        ILogger<CsvImportService> logger,
        IConfiguration configuration)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
        _configuration = configuration;
        _batchSize = _configuration.GetValue<int>("ImportSettings:BatchSize", 5000);
    }

    public async Task<ImportResult> ImportCsvToMongoAsync(string csvFilePath)
    {
        var startTime = DateTime.Now;
        var importResult = new ImportResult();

        if (!File.Exists(csvFilePath))
        {
            _logger.LogError($"CSV file not found: {csvFilePath}");
            throw new FileNotFoundException($"CSV file not found", csvFilePath);
        }

        _logger.LogInformation($"Starting import from {csvFilePath}");
        _logger.LogInformation($"Batch size: {_batchSize}");

        // Create index for better performance
        await _mongoDbService.CreateIndexIfNotExistsAsync();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using (var reader = new StreamReader(csvFilePath))
        using (var csv = new CsvReader(reader, config))
        {
            var batch = new List<Ledger>(_batchSize);
            long recordCount = 0;
            long batchCount = 0;

            // Configure CSV reader
            csv.Context.RegisterClassMap<CsvMemberMap>();

            await csv.ReadAsync();
            csv.ReadHeader();

            // Process the data row by row
            while (await csv.ReadAsync())
            {
                var record = csv.GetRecord<CsvMember>();
                recordCount++;

                if (!string.IsNullOrEmpty(record.MEMBERPEOMEMNUM))
                {
                    var ledger = new Ledger
                    {
                        Cpf = record.MEMBERPEOMEMNUM,
                        CreatedAt = _defaultCreatedAt,
                        LedgerTypeId = 1,
                        Points = null,
                        PointsBlocked = 0,
                        Status = "Ativo"
                    };

                    batch.Add(ledger);
                }

                // If batch is full, insert to MongoDB
                if (batch.Count >= _batchSize)
                {
                  batchCount =  await ProcessBatch(batch, batchCount, importResult);
                }

                // Log progress periodically
                if (recordCount % (_batchSize * 10) == 0)
                {
                    _logger.LogInformation($"Processed {recordCount:N0} records so far");
                }
            }

            // Process remaining records
            if (batch.Count > 0)
            {
             batchCount = await ProcessBatch(batch, batchCount, importResult);
            }

            importResult.TotalRecords = recordCount;
            importResult.TotalBatches = batchCount;
            importResult.DurationMs = (DateTime.Now - startTime).TotalMilliseconds;

            _logger.LogInformation($"Import completed in {importResult.DurationMs / 1000:N2} seconds");
            _logger.LogInformation($"Total records processed: {importResult.TotalRecords:N0}");
            _logger.LogInformation($"Total records imported: {importResult.InsertedRecords:N0}");
            _logger.LogInformation($"Total batches: {importResult.TotalBatches:N0}");

            return importResult;
        }
    }

    private async Task<long> ProcessBatch(List<Ledger> batch, long batchCount, ImportResult importResult)
    {
        try
        {
            await _mongoDbService.InsertManyAsync(batch);
            importResult.InsertedRecords += batch.Count;
            _logger.LogDebug($"Batch {batchCount} with {batch.Count} records inserted successfully");
        }
        catch (Exception ex)
        {
            importResult.FailedBatches++;
            _logger.LogError(ex, $"Failed to insert batch {batchCount}");
        }
        finally
        {
            batch.Clear();
        }
        return batchCount;
    }
}

public sealed class CsvMemberMap : ClassMap<CsvMember>
{
    public CsvMemberMap()
    {
        Map(m => m.LOYMEMBERID).Name("LOYMEMBERID");
        Map(m => m.MEMBERPEOMEMNUM).Name("MEMBERPEOMEMNUM");
    }
}

public class ImportResult
{
    public long TotalRecords { get; set; }
    public long InsertedRecords { get; set; }
    public long TotalBatches { get; set; }
    public long FailedBatches { get; set; }
    public double DurationMs { get; set; }
}