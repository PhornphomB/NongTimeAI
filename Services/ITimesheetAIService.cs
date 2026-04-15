using NongTimeAI.Models;

namespace NongTimeAI.Services;

public interface ITimesheetAIService
{
    Task<TimesheetResponse> ProcessTimesheetMessageAsync(string userMessage);
    Task<string> GenerateReminderMessageAsync(string employeeName, string projectName);
}
