using Microsoft.Extensions.Logging;
using Npgsql;
using ToolBox.Configuration;
using ToolBox.Domain.Entities;

namespace ToolBox.Services;

public class PostgresDbService : IMemberRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresDbService> _logger;

    public PostgresDbService(IPostgresSettings settings, ILogger<PostgresDbService> logger)
    {
        _connectionString = settings.ConnectionString;
        _logger = logger;
        _logger.LogInformation("PostgreSQL repository initialized");
    }

    public async Task CreateTableIfNotExistsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        string createEnumTypeQuery = @"            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'e_member_ledger_type') THEN
                    CREATE TYPE e_member_ledger_type AS ENUM ('stix');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'e_member_status') THEN
                    CREATE TYPE e_member_status AS ENUM ('ativo');
                END IF;
            END $$;";

        string createTableQuery = @"
CREATE TABLE IF NOT EXISTS member (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ledger_customer_id TEXT NULL,
    external_id UUID DEFAULT gen_random_uuid() NOT NULL,
    cpf TEXT NOT NULL,
    ledger_type_id e_member_ledger_type NOT NULL,
    points INTEGER NOT NULL,
    points_blocked INTEGER NOT NULL DEFAULT 0,
    status e_member_status NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT uk_member_cpf UNIQUE (cpf) 
    );";

        using (var cmd = new NpgsqlCommand(createEnumTypeQuery, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        using (var cmd = new NpgsqlCommand(createTableQuery, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Table 'member' created or already exists");
    }

    public async Task CreateIndexIfNotExistsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create index on ledger_customer_id
        string createLedgerCustomerIdIndexQuery = @"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = 'idx_member_ledger_customer_id'
                    AND n.nspname = 'public'
                ) THEN
                    CREATE INDEX idx_member_ledger_customer_id ON member(ledger_customer_id);
                END IF;
            END $$;";

        using (var cmd = new NpgsqlCommand(createLedgerCustomerIdIndexQuery, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Index 'idx_member_ledger_customer_id' created or already exists");

        // Create index on external_id
        string createExternalIdIndexQuery = @"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = 'idx_member_external_id'
                    AND n.nspname = 'public'
                ) THEN
                    CREATE INDEX idx_member_external_id ON member(external_id);
                END IF;
            END $$;";

        using (var cmd = new NpgsqlCommand(createExternalIdIndexQuery, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Index 'idx_member_external_id' created or already exists");

        // Create index on cpf
        string createCpfIndexQuery = @"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relname = 'idx_member_cpf'
                    AND n.nspname = 'public'
                ) THEN
                    CREATE INDEX idx_member_cpf ON member(cpf);
                END IF;
            END $$;";

        using (var cmd = new NpgsqlCommand(createCpfIndexQuery, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Index 'idx_member_cpf' created or already exists");
    }

    public async Task InsertManyAsync(List<Member> members)
    {
        if (members.Count == 0)
            return;

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Begin transaction
            using var transaction = await connection.BeginTransactionAsync();

            // Use COPY command for bulk insert which is much faster than individual inserts
            using (var writer = connection.BeginBinaryImport(@"
                COPY member (ledger_customer_id, external_id, cpf, ledger_type_id, points, points_blocked, status, created_at, updated_at)
                FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var member in members)
                {
                    writer.StartRow();
                    writer.Write(member.LedgerCustomerId, NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(member.ExternalId, NpgsqlTypes.NpgsqlDbType.Uuid);
                    writer.Write(member.Cpf, NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(member.LedgerTypeId, NpgsqlTypes.NpgsqlDbType.Text);

                    if (member.Points.HasValue)
                        writer.Write(member.Points.Value, NpgsqlTypes.NpgsqlDbType.Integer);
                    else
                        writer.WriteNull();

                    writer.Write(member.PointsBlocked, NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(member.Status, NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(member.CreatedAt, NpgsqlTypes.NpgsqlDbType.TimestampTz);
                    writer.Write(DateTime.UtcNow, NpgsqlTypes.NpgsqlDbType.TimestampTz); // updated_at
                }                // Complete the COPY operation
                ulong rowsAffected = await writer.CompleteAsync();

                _logger.LogInformation($"Successfully bulk inserted {rowsAffected} records in batch of {members.Count}");
            }            // Commit transaction - deve ser chamado APÓS o fechamento do bloco using do writer
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error bulk inserting batch of {members.Count} records: {ex.Message}");

            // If the error is due to duplicate CPF, try inserting with ON CONFLICT DO NOTHING
            if (ex.Message.Contains("duplicate key") || ex.Message.Contains("unique constraint"))
            {
                _logger.LogWarning("Retrying with individual inserts and ON CONFLICT DO NOTHING strategy");
                await InsertWithConflictHandlingAsync(members);
            }
            else
            {
                throw;
            }
        }
    }

    private async Task InsertWithConflictHandlingAsync(List<Member> members)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Begin transaction
            using var transaction = await connection.BeginTransactionAsync();

            string insertQuery = @"
                INSERT INTO member (ledger_customer_id, external_id, cpf, ledger_type_id, points, points_blocked, status, created_at, updated_at)
                VALUES (@ledgerCustomerId, @externalId, @cpf, @ledgerTypeId::e_member_ledger_type, @points, @pointsBlocked, @status::e_member_status, @createdAt, @updatedAt)
                ON CONFLICT (cpf) DO NOTHING;";

            // Prepare the command once but execute for each row
            using var cmd = new NpgsqlCommand(insertQuery, connection, transaction);

            // Prepare parameters
            cmd.Parameters.Add(new NpgsqlParameter("@ledgerCustomerId", NpgsqlTypes.NpgsqlDbType.Text));
            cmd.Parameters.Add(new NpgsqlParameter("@externalId", NpgsqlTypes.NpgsqlDbType.Uuid));
            cmd.Parameters.Add(new NpgsqlParameter("@cpf", NpgsqlTypes.NpgsqlDbType.Text));
            cmd.Parameters.Add(new NpgsqlParameter("@ledgerTypeId", NpgsqlTypes.NpgsqlDbType.Text));
            cmd.Parameters.Add(new NpgsqlParameter("@points", NpgsqlTypes.NpgsqlDbType.Integer));
            cmd.Parameters.Add(new NpgsqlParameter("@pointsBlocked", NpgsqlTypes.NpgsqlDbType.Integer));
            cmd.Parameters.Add(new NpgsqlParameter("@status", NpgsqlTypes.NpgsqlDbType.Text));
            cmd.Parameters.Add(new NpgsqlParameter("@createdAt", NpgsqlTypes.NpgsqlDbType.TimestampTz));
            cmd.Parameters.Add(new NpgsqlParameter("@updatedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz));

            // Use batching to minimize roundtrips
            cmd.Prepare();
            int inserted = 0;

            foreach (var member in members)
            {
                cmd.Parameters["@ledgerCustomerId"].Value = member.LedgerCustomerId;
                cmd.Parameters["@externalId"].Value = member.ExternalId;
                cmd.Parameters["@cpf"].Value = member.Cpf;
                cmd.Parameters["@ledgerTypeId"].Value = member.LedgerTypeId.ToLowerInvariant(); // Converter para minúsculo
                cmd.Parameters["@points"].Value = member.Points.HasValue ? (object)member.Points.Value : DBNull.Value;
                cmd.Parameters["@pointsBlocked"].Value = member.PointsBlocked;
                cmd.Parameters["@status"].Value = member.Status.ToLowerInvariant();
                cmd.Parameters["@createdAt"].Value = member.CreatedAt;
                cmd.Parameters["@updatedAt"].Value = DateTime.UtcNow;

                inserted += await cmd.ExecuteNonQueryAsync();
            }

            // Commit transaction
            await transaction.CommitAsync();

            _logger.LogInformation($"Successfully inserted {inserted} records out of {members.Count} with conflict handling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error inserting batch with conflict handling: {ex.Message}");
            throw;
        }
    }
}