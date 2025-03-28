﻿using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using ToolBox.Configuration;
using ToolBox.Domain.Entities;

namespace ToolBox.Services;

public class MongoDbService : ILedgerRepository
{
    private readonly IMongoCollection<Ledger> _ledgerCollection;
    private readonly ILogger<MongoDbService> _logger;

    public MongoDbService(MongoDbSettings settings, ILogger<MongoDbService> logger)
    {
        _logger = logger;

        settings.Validate();

        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _ledgerCollection = database.GetCollection<Ledger>(settings.CollectionName);

        _logger.LogInformation("MongoDB connection established");
    }

    public async Task InsertManyAsync(List<Ledger> ledgers)
    {
        if (ledgers.Count == 0)
            return;

        try
        {
            var insertOptions = new InsertManyOptions { IsOrdered = false };
            await _ledgerCollection.InsertManyAsync(ledgers, insertOptions);
            _logger.LogInformation($"Successfully inserted {ledgers.Count} records");
        }
        catch (MongoBulkWriteException ex)
        {
            HandleBulkWriteException(ex, ledgers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error inserting batch of {ledgers.Count} records: {ex.Message}");
            throw;
        }
    }

    private void HandleBulkWriteException(MongoBulkWriteException ex, int totalRecords)
    {
        int insertedCount = totalRecords - ex.WriteErrors.Count;

        _logger.LogWarning($"Partial batch insertion: {ex.WriteErrors.Count} errors out of {totalRecords} records");
        _logger.LogInformation($"Succeeded in writing approximately {insertedCount} records");

        foreach (var error in ex.WriteErrors)
        {
            _logger.LogDebug($"Error at index {error.Index}: {error.Message}");
        }
    }

    public async Task CreateIndexIfNotExistsAsync()
    {
        var indexKeysDefinition = Builders<Ledger>.IndexKeys.Ascending(l => l.Cpf);
        var indexOptions = new CreateIndexOptions { Unique = true, Background = true };
        var indexModel = new CreateIndexModel<Ledger>(indexKeysDefinition, indexOptions);

        try
        {
            await _ledgerCollection.Indexes.CreateOneAsync(indexModel);
            _logger.LogInformation("CPF index created successfully");
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
        {
            _logger.LogInformation("CPF index already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating CPF index: {ex.Message}");
            throw;
        }
    }
}