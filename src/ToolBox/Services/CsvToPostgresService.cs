using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ToolBox.Domain.Entities;
using ToolBox.Models;

namespace ToolBox.Services;

public interface ICsvToPostgresService
{
    Task<ImportResult> ImportCsvToPostgresAsync(string csvFilePath);
}

public class CsvToPostgresService : ICsvToPostgresService
{
    private readonly ICsvReaderService _csvReaderService;
    private readonly IMemberRepository _memberRepository;
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<CsvToPostgresService> _logger;
    private readonly int _batchSize;

    public CsvToPostgresService(
        ICsvReaderService csvReaderService,
        IMemberRepository memberRepository,
        IProgressBarService progressBarService,
        ILogger<CsvToPostgresService> logger,
        int batchSize = 1000)
    {
        _csvReaderService = csvReaderService;
        _memberRepository = memberRepository;
        _progressBarService = progressBarService;
        _logger = logger;
        _batchSize = batchSize;
    }

    public async Task<ImportResult> ImportCsvToPostgresAsync(string csvFilePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult();

        try
        {
            _logger.LogInformation($"Starting import from CSV file: {csvFilePath}");

            // Ensure table and index exist
            await _memberRepository.CreateTableIfNotExistsAsync();
            await _memberRepository.CreateIndexIfNotExistsAsync();

            // Get total records for progress tracking
            long totalRecords = await _csvReaderService.CountLinesAsync(csvFilePath);
            _logger.LogInformation($"Total records to process: {totalRecords}");

            // Initialize progress bar
            _progressBarService.Initialize(totalRecords, "Importing members to PostgreSQL");            // Read and process CSV in batches
            var records = await _csvReaderService.ReadCsvAsync<CsvMember>(csvFilePath);
            var batch = new List<Member>();
            long processedRecords = 0;
            long totalBatches = 0;
            long failedBatches = 0;

            await foreach (var record in records)
            {
                if (!string.IsNullOrWhiteSpace(record.MemberPeoMemNum))
                {
                    var member = Member.Create(record.MemberPeoMemNum, record.LoyMemberId);
                    batch.Add(member);
                }
                processedRecords++;
                _progressBarService.Report(processedRecords);

                // Process batch when it reaches the batch size
                if (batch.Count >= _batchSize)
                {
                    totalBatches++;
                    try
                    {
                        await _memberRepository.InsertManyAsync(batch);
                        result.InsertedRecords += batch.Count;
                    }
                    catch (Exception ex)
                    {
                        failedBatches++;
                        _logger.LogError(ex, $"Error inserting batch {totalBatches}: {ex.Message}");
                    }
                    finally
                    {
                        batch.Clear();
                    }
                }
            }

            // Process remaining records
            if (batch.Count > 0)
            {
                totalBatches++;
                try
                {
                    await _memberRepository.InsertManyAsync(batch);
                    result.InsertedRecords += batch.Count;
                }
                catch (Exception ex)
                {
                    failedBatches++;
                    _logger.LogError(ex, $"Error inserting final batch: {ex.Message}");
                }
            }

            // Complete progress bar
            _progressBarService.Complete();

            // Update result statistics
            stopwatch.Stop();
            result.TotalRecords = processedRecords;
            result.TotalBatches = totalBatches;
            result.FailedBatches = failedBatches;
            result.DurationInSeconds = stopwatch.Elapsed.TotalSeconds;
            result.RecordsPerSecond = processedRecords / result.DurationInSeconds;

            _logger.LogInformation($"Import completed in {result.DurationInSeconds:N2} seconds. " +
                                  $"Processed {result.TotalRecords:N0} records, inserted {result.InsertedRecords:N0}");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, $"Error during import: {ex.Message}");

            result.DurationInSeconds = stopwatch.Elapsed.TotalSeconds;

            throw;
        }
    }
}
