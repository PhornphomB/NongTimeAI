using Microsoft.AspNetCore.Mvc;
using NongTimeAI.Services;
using NongTimeAI.Models;
using NongTimeAI.Helpers;

namespace NongTimeAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly ITaskNotificationService _notificationService;
    private readonly ILogger<TaskController> _logger;

    public TaskController(
        ITaskNotificationService notificationService,
        ILogger<TaskController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get pending tasks for a user
    /// </summary>
    [HttpGet("pending/{userId}")]
    public async Task<ActionResult<List<PendingTaskDto>>> GetPendingTasks(string userId)
    {
        try
        {
            var tasks = await _notificationService.GetPendingTasksAsync(userId);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending tasks for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve pending tasks" });
        }
    }

    /// <summary>
    /// Get pending tasks as TaskItem format (for Flex Message)
    /// </summary>
    [HttpGet("pending-items/{userId}")]
    public async Task<ActionResult<List<TaskItem>>> GetPendingTaskItems(string userId)
    {
        try
        {
            var tasks = await _notificationService.GetPendingTaskItemsAsync(userId);
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending task items for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve pending tasks" });
        }
    }

    /// <summary>
    /// Save task tracking
    /// </summary>
    [HttpPost("tracking")]
    public async Task<IActionResult> SaveTracking([FromBody] SaveTrackingRequest request)
    {
        try
        {
            var entry = new TimesheetEntry
            {
                Detail = request.Detail,
                Hours = request.Hours,
                IssueType = request.IssueType,
                IsComplete = true
            };

            var success = await _notificationService.SaveTaskTrackingAsync(
                request.UserId,
                request.LineUserId,
                entry,
                request.ProjectTaskId
            );

            if (success)
            {
                return Ok(new { message = "Task tracking saved successfully" });
            }

            return BadRequest(new { error = "Failed to save task tracking" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task tracking");
            return StatusCode(500, new { error = "Failed to save task tracking" });
        }
    }
}

public class SaveTrackingRequest
{
    public string UserId { get; set; } = string.Empty;
    public string LineUserId { get; set; } = string.Empty;
    public long ProjectTaskId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public float Hours { get; set; }
    public string IssueType { get; set; } = "Other";
}
