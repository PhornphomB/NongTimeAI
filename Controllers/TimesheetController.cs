using Microsoft.AspNetCore.Mvc;
using NongTimeAI.Models;
using NongTimeAI.Services;

namespace NongTimeAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimesheetController : ControllerBase
{
    private readonly ITimesheetAIService _timesheetAIService;
    private readonly ILogger<TimesheetController> _logger;

    public TimesheetController(ITimesheetAIService timesheetAIService, ILogger<TimesheetController> logger)
    {
        _timesheetAIService = timesheetAIService;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<ActionResult<TimesheetResponse>> ProcessMessage([FromBody] TimesheetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new TimesheetResponse
            {
                Success = false,
                Message = "Message is required",
                BotReply = "?????????????????????????"
            });
        }

        _logger.LogInformation("Processing timesheet message: {Message}", request.Message);
        var result = await _timesheetAIService.ProcessTimesheetMessageAsync(request.Message);
        return Ok(result);
    }

    /// <summary>
    /// Test AI endpoint - สำหรับทดสอบ AI โดยตรง พร้อม Debug info
    /// POST /api/timesheet/test-ai
    /// Body: { "message": "ทำการอัพเดตข้อมูล Email 2 ชม. วันศุกร์" }
    /// </summary>
    [HttpPost("test-ai")]
    public async Task<ActionResult> TestAI([FromBody] TimesheetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        _logger.LogInformation("🧪 [TEST AI] Starting test for message: {Message}", request.Message);

        var startTime = DateTime.UtcNow;

        try
        {
            var result = await _timesheetAIService.ProcessTimesheetMessageAsync(request.Message);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogInformation(
                "✅ [TEST AI] Completed in {Duration}s: Success={Success}, IsComplete={IsComplete}, Detail={Detail}, Hours={Hours}, IssueType={IssueType}",
                duration,
                result.Success,
                result.Data?.IsComplete,
                result.Data?.Detail,
                result.Data?.Hours,
                result.Data?.IssueType
            );

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                duration_seconds = Math.Round(duration, 2),
                bot_reply = result.BotReply,
                data = result.Data != null ? new
                {
                    detail = result.Data.Detail,
                    hours = result.Data.Hours,
                    issue_type = result.Data.IssueType,
                    date = result.Data.Date,
                    is_complete = result.Data.IsComplete
                } : null,
                timing = new
                {
                    start_time = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    end_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    duration_ms = (DateTime.UtcNow - startTime).TotalMilliseconds
                }
            });
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogError(
                ex,
                "❌ [TEST AI] Failed after {Duration}s: {ErrorMessage}",
                duration,
                ex.Message
            );

            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                error_type = ex.GetType().Name,
                duration_seconds = Math.Round(duration, 2),
                stack_trace = ex.StackTrace,
                timing = new
                {
                    start_time = startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    end_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    duration_ms = (DateTime.UtcNow - startTime).TotalMilliseconds
                }
            });
        }
    }

    [HttpPost("reminder")]
    public async Task<ActionResult<string>> GenerateReminder([FromBody] ReminderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeName) || string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return BadRequest("Employee name and project name are required");
        }

        _logger.LogInformation("Generating reminder for {Employee} on project {Project}", 
            request.EmployeeName, request.ProjectName);

        var message = await _timesheetAIService.GenerateReminderMessageAsync(
            request.EmployeeName, 
            request.ProjectName);

        return Ok(new { message });
    }
}

public class ReminderRequest
{
    public string EmployeeName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
}
