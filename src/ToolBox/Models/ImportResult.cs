namespace ToolBox.Models;

public class ImportResult
{
    public long TotalRecords { get; set; }
    public long InsertedRecords { get; set; }
    public long FailedRecords { get; set; }
    public long TotalBatches { get; set; }
    public long FailedBatches { get; set; }
    public double DurationInSeconds { get; set; }
    public double RecordsPerSecond { get; set; }
}