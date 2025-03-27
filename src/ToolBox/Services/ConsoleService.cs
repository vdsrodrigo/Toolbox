using System.Text;
using ToolBox.Models;

namespace ToolBox.Services;

public class ConsoleService : IConsoleService
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
    
    public void UpdateProgress(double percentage, int matchesFound, int linesProcessed, TimeSpan remainingTime)
    {
        Console.CursorVisible = false;
    
        int consoleWidth = Console.WindowWidth - 10;
        int progressChars = (int)(percentage / 100 * consoleWidth);
    
        StringBuilder progressBar = new StringBuilder("[");
        for (int i = 0; i < consoleWidth; i++)
        {
            if (i < progressChars)
                progressBar.Append('=');
            else
                progressBar.Append(' ');
        }
        progressBar.Append(']');
    
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write($"{progressBar} {percentage:F1}%");
    
        Console.SetCursorPosition(0, Console.CursorTop + 1);
        Console.Write($"Linhas processadas: {linesProcessed:N0} | Correspondências: {matchesFound:N0} | Tempo restante: {remainingTime.ToString(@"hh\:mm\:ss")}");
    
        Console.SetCursorPosition(0, Console.CursorTop - 1);
    }
}