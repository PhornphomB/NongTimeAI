namespace NongTimeAI.Models;

public class TimesheetEntry
{
    public string Detail { get; set; } = string.Empty;
    public float Hours { get; set; } = 0.0f;
    public string IssueType { get; set; } = "Task";
    public DateTime? Date { get; set; } // วันที่ทำงาน (null = วันนี้)
    public bool IsComplete { get; set; } = false;
}
