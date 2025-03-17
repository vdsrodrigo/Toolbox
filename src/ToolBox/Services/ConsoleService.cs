using ToolBox.Models;

namespace ToolBox.Services;

public class ConsoleService
{
    public void DisplayHeader()
    {
        Console.WriteLine("====================================================");
        Console.WriteLine("  CSV to MongoDB Ledger Importer");
        Console.WriteLine("====================================================");
    }

    public void DisplayInputFile(string csvFilePath)
    {
        Console.WriteLine($"CSV File: {csvFilePath}");
    }

    public void DisplayImportResults(ImportResult result)
    {
        Console.WriteLine("\nImport Statistics:");
        Console.WriteLine("----------------------------------------------------");
        Console.WriteLine($"Total records processed: {result.TotalRecords:N0}");
        Console.WriteLine($"Total records imported: {result.InsertedRecords:N0}");
        Console.WriteLine($"Total batches: {result.TotalBatches:N0}");
        Console.WriteLine($"Failed batches: {result.FailedBatches:N0}");
        Console.WriteLine($"Duration: {result.DurationInSeconds:N2} seconds");
        Console.WriteLine($"Average rate: {result.RecordsPerSecond:N0} records/second");
        Console.WriteLine("====================================================");
    }

    public void DisplayError(string message)
    {
        Console.WriteLine($"ERROR: {message}");
    }
}