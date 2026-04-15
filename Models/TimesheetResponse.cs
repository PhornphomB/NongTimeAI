namespace NongTimeAI.Models;

public class TimesheetResponse
{
    public bool Success { get; set; }
    public TimesheetEntry? Data { get; set; }
    public string? Message { get; set; }
    public string? BotReply { get; set; }
}
