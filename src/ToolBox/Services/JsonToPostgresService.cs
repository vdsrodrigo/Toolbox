using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ToolBox.Domain.Entities;
using ToolBox.Models;

namespace ToolBox.Services;

public interface IJsonToPostgresService
{
    Task<ImportResult> ImportJsonlToPostgresAsync(string jsonlFilePath);
}

public class JsonToPostgresService : IJsonToPostgresService
{
    private readonly IJsonReaderService _jsonReaderService;
    private readonly IMemberRepository _memberRepository;
    private readonly IProgressBarService _progressBarService;
    private readonly ILogger<JsonToPostgresService> _logger;
    private readonly int _batchSize;

    public JsonToPostgresService(
        IJsonReaderService jsonReaderService,
        IMemberRepository memberRepository,
        IProgressBarService progressBarService,
        ILogger<JsonToPostgresService> logger,
        int batchSize = 1000)
    {
        _jsonReaderService = jsonReaderService;
        _memberRepository = memberRepository;
        _progressBarService = progressBarService;
        _logger = logger;
        _batchSize = batchSize;
    }

    public async Task<ImportResult> ImportJsonlToPostgresAsync(string jsonlFilePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult();

        try
        {
            _logger.LogInformation($"Starting import from JSONL file: {jsonlFilePath}");

            // Ensure table and index exist
            await _memberRepository.CreateTableIfNotExistsAsync();
            await _memberRepository.CreateIndexIfNotExistsAsync();

            // Get total records for progress tracking
            long totalRecords = await _jsonReaderService.CountLinesAsync(jsonlFilePath);
            _logger.LogInformation($"Total records to process: {totalRecords}");

            // Initialize progress bar
            _progressBarService.Initialize(totalRecords, "Importing members to PostgreSQL");

            // Read and process JSONL in batches
            var jsonMembers = _jsonReaderService.ReadJsonMembersAsync(jsonlFilePath);
            var batch = new List<Member>();
            long processedRecords = 0;
            long totalBatches = 0;
            long failedBatches = 0;

            await foreach (var jsonMember in jsonMembers)
            {
                if (jsonMember != null && !string.IsNullOrWhiteSpace(jsonMember.Cpf))
                {
                    try
                    {
                        string ledgerCustomerId = jsonMember.Id?.Oid ?? string.Empty;
                        string ledgerTypeId = ConvertLedgerTypeIdToString(jsonMember.LedgerTypeId);

                        var member = new Member
                        {
                            LedgerCustomerId = ledgerCustomerId,
                            ExternalId = Member.GenerateUUIDv7(),
                            Cpf = jsonMember.Cpf,
                            LedgerTypeId = ledgerTypeId.ToLowerInvariant(), // Converter para minúsculo
                            Points = jsonMember.Points ?? 0, // Usar 0 como padrão quando Points for nulo
                            PointsBlocked = jsonMember.PointsBlocked,
                            Status = (jsonMember.Status ?? "ativo").ToLowerInvariant(), // Usar "ativo" como padrão e converter para minúsculo
                            CreatedAt = jsonMember.CreatedAt?.ToDateTime() ?? DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        batch.Add(member);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error creating member from JSONL: {ex.Message}");
                    }
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

    private string ConvertLedgerTypeIdToString(int ledgerTypeId)
    {
        return ledgerTypeId switch
        {
            1 => "stix", // Valor em minúsculo
            _ => "stix"  // Default em minúsculo
        };
    }
}
