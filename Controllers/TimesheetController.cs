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
