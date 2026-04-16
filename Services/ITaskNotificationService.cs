using NongTimeAI.Models;
using NongTimeAI.Helpers;

namespace NongTimeAI.Services;

public interface ITaskNotificationService
{
    Task<List<PendingTaskDto>> GetPendingTasksAsync(string userId);
    Task<List<TaskItem>> GetPendingTaskItemsAsync(string userId);
    Task<string> GenerateTaskSummaryMessageAsync(List<PendingTaskDto> tasks, string userName);
    Task<bool> SaveTaskTrackingAsync(string userId, string lineUserId, TimesheetEntry entry, int projectTaskId);
    Task<List<string>> GetIssueTypesAsync();
}
