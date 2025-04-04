using ToolBox.Models;

namespace ToolBox.Services;

public interface ICsvImportService
{
    Task<ImportResult> ImportCsvToMongoAsync(string csvFilePath);
} 