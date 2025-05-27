using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ToolBox.Domain.Entities;
using ToolBox.Models;

namespace ToolBox.Services;

public interface ICsvReaderService
{
    IAsyncEnumerable<Ledger> ReadLedgersAsync(string filePath);
    Task<long> CountLinesAsync(string filePath);
    Task<IAsyncEnumerable<T>> ReadCsvAsync<T>(string filePath);
}

public class CsvReaderService : ICsvReaderService
{
    private readonly DateTime _defaultCreatedAt = DateTime.UtcNow;

    public async IAsyncEnumerable<Ledger> ReadLedgersAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found", filePath);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<CsvMemberMap>();
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var record = csv.GetRecord<CsvMember>();

            if (!string.IsNullOrEmpty(record.MemberPeoMemNum))
            {
                yield return Ledger.Create(record.MemberPeoMemNum, _defaultCreatedAt);
            }
        }
    }

    public async Task<long> CountLinesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found", filePath);
        }

        long lineCount = 0;

        using (var reader = new StreamReader(filePath))
        {
            while (await reader.ReadLineAsync() != null)
            {
                lineCount++;
            }
        }

        // Subtract 1 for the header line if the file has a header
        return lineCount > 0 ? lineCount - 1 : 0;
    }

    public Task<IAsyncEnumerable<T>> ReadCsvAsync<T>(string filePath)
    {
        return Task.FromResult<IAsyncEnumerable<T>>(ReadCsvRecordsAsync<T>(filePath));
    }

    private async IAsyncEnumerable<T> ReadCsvRecordsAsync<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV file not found", filePath);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var record = csv.GetRecord<T>();
            if (record != null)
            {
                yield return record;
            }
        }
    }
}