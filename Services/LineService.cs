using Line.Messaging;
using NongTimeAI.Models;
using NongTimeAI.Data;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Services;

public class LineService : ILineService
{
    private readonly LineMessagingClient _lineClient;
    private readonly TimesheetDbContext _dbContext;
    private readonly ILogger<LineService> _logger;

    public LineService(
        LineMessagingClient lineClient,
        TimesheetDbContext dbContext,
        ILogger<LineService> logger)
    {
        _lineClient = lineClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ReplyMessageAsync(string replyToken, string message)
    {
        try
        {
            var messages = new List<ISendMessage>
            {
                new TextMessage(message)
            };

            await _lineClient.ReplyMessageAsync(replyToken, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply message");
            throw;
        }
    }

    public async Task PushMessageAsync(string userId, string message)
    {
        try
        {
            var messages = new List<ISendMessage>
            {
                new TextMessage(message)
            };

            await _lineClient.PushMessageAsync(userId, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push message to user {UserId}", userId);
            throw;
        }
    }

    public async Task SaveTimesheetAsync(string userId, TimesheetEntry entry)
    {
        try
        {
            var timesheet = new Timesheet
            {
                UserId = userId,
                Detail = entry.Detail,
                Hours = entry.Hours,
                IssueType = entry.IssueType,
                CreatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow.Date
            };

            _dbContext.Timesheets.Add(timesheet);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Timesheet saved: User={UserId}, Detail={Detail}, Hours={Hours}",
                userId,
                entry.Detail,
                entry.Hours
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save timesheet for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<TimesheetEntry>> GetUserTimesheetsAsync(
        string userId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            var query = _dbContext.Timesheets
                .Where(t => t.UserId == userId);

            if (startDate.HasValue)
            {
                query = query.Where(t => t.Date >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.Date <= endDate.Value);
            }

            var timesheets = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return timesheets.Select(t => new TimesheetEntry
            {
                Detail = t.Detail,
                Hours = t.Hours,
                IssueType = t.IssueType,
                IsComplete = true
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get timesheets for user {UserId}", userId);
            throw;
        }
    }
}
