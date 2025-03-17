namespace ToolBox.Models;

public record ImportResult
{
    public long TotalRecords { get; set; }
    public long InsertedRecords { get; set; }
    public long TotalBatches { get; set; }
    public long FailedBatches { get; set; }
    public double DurationMs { get; set; }

    public double DurationInSeconds => DurationMs / 1000;
    public double RecordsPerSecond => TotalRecords / (DurationMs / 1000);
}