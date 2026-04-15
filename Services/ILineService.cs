using NongTimeAI.Models;

namespace NongTimeAI.Services;

public interface ILineService
{
    Task ReplyMessageAsync(string replyToken, string message);
    Task PushMessageAsync(string userId, string message);
    Task SaveTimesheetAsync(string userId, TimesheetEntry entry);
    Task<List<TimesheetEntry>> GetUserTimesheetsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
}
