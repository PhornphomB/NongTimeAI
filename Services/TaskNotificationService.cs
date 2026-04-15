using Microsoft.EntityFrameworkCore;
using NongTimeAI.Data;
using NongTimeAI.Models;
using NongTimeAI.Helpers;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Services;

public class TaskNotificationService : ITaskNotificationService
{
    private readonly TimesheetDbContext _dbContext;
    private readonly ILogger<TaskNotificationService> _logger;

    public TaskNotificationService(
        TimesheetDbContext dbContext,
        ILogger<TaskNotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<PendingTaskDto>> GetPendingTasksAsync(string userId)
    {
        try
        {
            return await _dbContext.GetPendingTasksAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending tasks for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TaskItem>> GetPendingTaskItemsAsync(string userId)
    {
        try
        {
            var tasks = await GetPendingTasksAsync(userId);

            return tasks.Select(t => new TaskItem
            {
                TaskId = t.ProjectTaskId,
                TaskName = t.TaskName ?? "ไม่ระบุชื่องาน",
                ProjectName = t.ProjectName ?? "ไม่ระบุโปรเจกต์",
                Priority = t.Priority ?? "Medium",
                IssueType = t.IssueType ?? "Other",
                EndDate = t.EndDateExtend ?? t.EndDate
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending task items for user {UserId}", userId);
            return new List<TaskItem>();
        }
    }

    public async Task<string> GenerateTaskSummaryMessageAsync(List<PendingTaskDto> tasks, string userName)
    {
        if (!tasks.Any())
        {
            return $"สวัสดีครับคุณ {userName} 😊\nวันนี้คุณไม่มีงานที่ค้างอยู่แล้วครับ เยี่ยมมาก! 🎉";
        }

        var highPriorityTasks = tasks.Where(t => t.Priority == "High").ToList();
        var totalTasks = tasks.Count;

        var message = $"📋 **งานค้างของคุณ {userName}**\n\n";
        message += $"รวมทั้งหมด: **{totalTasks} งาน**\n\n";

        if (highPriorityTasks.Any())
        {
            message += "🔴 **งานเร่งด่วน (High Priority):**\n";
            foreach (var task in highPriorityTasks.Take(3))
            {
                var dueDate = task.EndDateExtend ?? task.EndDate;
                var daysLeft = dueDate.HasValue ? (dueDate.Value.Date - DateTime.Now.Date).Days : 0;
                var dueDateText = daysLeft < 0 ? $"เลยกำหนด {Math.Abs(daysLeft)} วัน ⚠️" :
                                 daysLeft == 0 ? "ครบกำหนดวันนี้! 🚨" :
                                 $"เหลือ {daysLeft} วัน";

                message += $"\n📌 {task.TaskName}\n";
                message += $"   โปรเจกต์: {task.ProjectName}\n";
                message += $"   กำหนดเสร็จ: {dueDateText}\n";
            }
            message += "\n";
        }

        var otherTasks = tasks.Where(t => t.Priority != "High").Take(2).ToList();
        if (otherTasks.Any())
        {
            message += "📝 **งานอื่นๆ:**\n";
            foreach (var task in otherTasks)
            {
                message += $"• {task.TaskName} ({task.ProjectName})\n";
            }
            message += "\n";
        }

        message += "💬 **วิธีการบันทึก Timesheet:**\n";
        message += "📝 ต้องระบุ: งาน + ชั่วโมง + ประเภท (+ วันที่)\n\n";
        message += "✏️ ตัวอย่าง:\n";
        message += "• \"แก้บั๊ก login 2 ชม.\" (วันนี้)\n";
        message += "• \"ประชุมทีม 1.5 ชม. เมื่อวาน\"\n";
        message += "• \"ศึกษา AI 8 ชม. 13/01\"\n";
        message += "• \"พัฒนา API 5 ชม. วันจันทร์\"\n\n";
        message += "📌 หมายเหตุ:\n";
        message += "- ระบบจะให้เลือกงานที่จะบันทึก\n";
        message += "- สามารถลงข้อมูลย้อนหลังได้\n";
        message += "- ไม่ระบุวันที่ = วันนี้\n\n";

        return message;
    }

    public async Task<bool> SaveTaskTrackingAsync(
        string userId,
        string lineUserId,
        TimesheetEntry entry,
        long projectTaskId)
    {
        try
        {
            _logger.LogInformation("💾 SaveTaskTrackingAsync called: UserId={UserId}, LineUserId={LineUserId}, TaskId={TaskId}, Detail={Detail}, Hours={Hours}, IssueType={IssueType}",
                userId, lineUserId, projectTaskId, entry.Detail, entry.Hours, entry.IssueType);

            // Get user info
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UserId == userId || u.LineUserId == lineUserId);

            _logger.LogInformation("👤 User found: {Found}, UserId={UserId}", user != null, user?.UserId);

            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}/{LineUserId}", userId, lineUserId);
                return false;
            }

            // Get project task
            var projectTask = await _dbContext.ProjectTasks
                .Include(t => t.ProjectHeader)
                .FirstOrDefaultAsync(t => t.ProjectTaskId == projectTaskId);

            if (projectTask == null)
            {
                _logger.LogWarning("📛 Project task not found: {ProjectTaskId}", projectTaskId);
                return false;
            }

            _logger.LogInformation("📋 Project task found: TaskId={TaskId}, ProjectHeaderId={ProjectHeaderId}",
                projectTask.ProjectTaskId, projectTask.ProjectHeaderId);

            // ✅ แปลงวันที่ให้เป็น UTC
            DateTime actualDate;
            if (entry.Date.HasValue)
            {
                // ถ้ามีวันที่ระบุมา แต่เป็น Unspecified ให้ถือว่าเป็นเวลาท้องถิ่น แล้วแปลงเป็น UTC
                if (entry.Date.Value.Kind == DateTimeKind.Unspecified)
                {
                    actualDate = DateTime.SpecifyKind(entry.Date.Value, DateTimeKind.Local).ToUniversalTime();
                }
                else if (entry.Date.Value.Kind == DateTimeKind.Local)
                {
                    actualDate = entry.Date.Value.ToUniversalTime();
                }
                else
                {
                    actualDate = entry.Date.Value; // Already UTC
                }
            }
            else
            {
                // ไม่มีวันที่ระบุ ใช้วันนี้ (UTC)
                actualDate = DateTime.UtcNow;
            }

            _logger.LogInformation("📅 Actual date (UTC): {ActualDate}", actualDate);

            // Create tracking entry
            var tracking = new ProjectTaskTracking
            {
                ProjectTaskId = projectTaskId,
                ProjectHeaderId = projectTask.ProjectHeaderId,
                ProcessUpdate = entry.Detail,
                IssueType = entry.IssueType,
                ActualDate = actualDate, // ✅ ใช้วันที่ที่แปลงเป็น UTC แล้ว
                ActualWork = (decimal)entry.Hours,
                Assignee = user.UserId,
                AssigneeFirstName = user.FirstName,
                AssigneeLastName = user.LastName,
                CreateBy = user.UserId,
                CreateDate = DateTime.UtcNow
            };

            _logger.LogInformation("💾 Adding tracking to DbContext: TaskId={TaskId}, Detail={Detail}, Hours={Hours}",
                tracking.ProjectTaskId, tracking.ProcessUpdate, tracking.ActualWork);

            _dbContext.ProjectTaskTrackings.Add(tracking);

            _logger.LogInformation("💾 Calling SaveChangesAsync...");
            var changes = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("✅ SaveChangesAsync completed: {Changes} record(s) affected", changes);

            _logger.LogInformation(
                "Task tracking saved: User={UserId}, Task={TaskId}, Hours={Hours}, Date={Date}",
                user.UserId,
                projectTaskId,
                entry.Hours,
                tracking.ActualDate
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task tracking");
            return false;
        }
    }

    public async Task<List<string>> GetIssueTypesAsync()
    {
        try
        {
            var issueTypes = await _dbContext.ComboboxItems
                .Where(c => c.GroupName == "issue_type" && c.IsActive == "YES")
                .OrderBy(c => c.DisplaySequence)
                .Select(c => c.ValueMember!)
                .ToListAsync();

            _logger.LogInformation("✅ Retrieved {Count} issue types from database", issueTypes.Count);
            return issueTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve issue types from database");
            return new List<string>();
        }
    }
}
