namespace ToolBox.Services;

using Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MongoDbService
{
       private readonly IMongoCollection<Ledger> _ledgerCollection;
        private readonly ILogger<MongoDbService> _logger;

        public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
        {
            _logger = logger;
            
            var connectionString = configuration["MongoDB:ConnectionString"];
            var databaseName = configuration["MongoDB:DatabaseName"];
            var collectionName = configuration["MongoDB:CollectionName"];

            if (string.IsNullOrEmpty(connectionString) || 
                string.IsNullOrEmpty(databaseName) || 
                string.IsNullOrEmpty(collectionName))
            {
                throw new ArgumentException("MongoDB configuration is incomplete. Check appsettings.json");
            }

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _ledgerCollection = database.GetCollection<Ledger>(collectionName);
            
            _logger.LogInformation("MongoDB connection established");
        }

        public async Task InsertManyAsync(List<Ledger> ledgers)
        {
            if (ledgers == null || ledgers.Count == 0)
                return;

            try
            {
                var insertOptions = new InsertManyOptions { IsOrdered = false };
                await _ledgerCollection.InsertManyAsync(ledgers, insertOptions);
            }
            catch (MongoBulkWriteException ex)
            {
                // Calcular quantos documentos foram realmente inseridos
                int insertedCount = ledgers.Count - ex.WriteErrors.Count;
        
                _logger.LogWarning($"Partial batch insertion: {ex.WriteErrors.Count} errors out of {ledgers.Count} records");
                _logger.LogInformation($"Succeeded in writing approximately {insertedCount} records");
        
                // Opcional: logar os detalhes dos erros
                foreach (var error in ex.WriteErrors)
                {
                    _logger.LogDebug($"Error at index {error.Index}: {error.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error inserting batch of {ledgers.Count} records: {ex.Message}");
                throw;
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