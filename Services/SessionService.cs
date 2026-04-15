using System.Collections.Concurrent;
using NongTimeAI.Models;

namespace NongTimeAI.Services;

/// <summary>
/// Session Manager สำหรับเก็บข้อมูล conversation state
/// </summary>
public interface ISessionService
{
    void SetSelectedTask(string lineUserId, long projectTaskId, string taskName);
    (long? taskId, string? taskName) GetSelectedTask(string lineUserId);
    void ClearSelectedTask(string lineUserId);
    bool HasSelectedTask(string lineUserId);

    // ✅ เพิ่ม: เก็บข้อมูล Timesheet ที่รอบันทึก
    void SetPendingTimesheetEntry(string lineUserId, TimesheetEntry entry);
    TimesheetEntry? GetPendingTimesheetEntry(string lineUserId);
    void ClearPendingTimesheetEntry(string lineUserId);
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, TaskSession> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public void SetSelectedTask(string lineUserId, long projectTaskId, string taskName)
    {
        if (_sessions.TryGetValue(lineUserId, out var existingSession))
        {
            // อัปเดต Task แต่เก็บ PendingEntry ไว้
            existingSession.ProjectTaskId = projectTaskId;
            existingSession.TaskName = taskName;
            existingSession.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _sessions[lineUserId] = new TaskSession
            {
                ProjectTaskId = projectTaskId,
                TaskName = taskName,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public (long? taskId, string? taskName) GetSelectedTask(string lineUserId)
    {
        if (_sessions.TryGetValue(lineUserId, out var session))
        {
            // ตรวจสอบว่า session หมดอายุหรือยัง
            if (DateTime.UtcNow - session.CreatedAt < _sessionTimeout)
            {
                return (session.ProjectTaskId, session.TaskName);
            }

            // หมดอายุแล้ว ลบทิ้ง
            _sessions.TryRemove(lineUserId, out _);
        }

        return (null, null);
    }

    public void ClearSelectedTask(string lineUserId)
    {
        _sessions.TryRemove(lineUserId, out _);
    }

    public bool HasSelectedTask(string lineUserId)
    {
        var (taskId, _) = GetSelectedTask(lineUserId);
        return taskId.HasValue;
    }

    // ✅ เพิ่ม: จัดการ Pending Timesheet Entry
    public void SetPendingTimesheetEntry(string lineUserId, TimesheetEntry entry)
    {
        if (_sessions.TryGetValue(lineUserId, out var existingSession))
        {
            existingSession.PendingEntry = entry;
            existingSession.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _sessions[lineUserId] = new TaskSession
            {
                PendingEntry = entry,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    public TimesheetEntry? GetPendingTimesheetEntry(string lineUserId)
    {
        if (_sessions.TryGetValue(lineUserId, out var session))
        {
            if (DateTime.UtcNow - session.CreatedAt < _sessionTimeout)
            {
                return session.PendingEntry;
            }

            _sessions.TryRemove(lineUserId, out _);
        }

        return null;
    }

    public void ClearPendingTimesheetEntry(string lineUserId)
    {
        if (_sessions.TryGetValue(lineUserId, out var session))
        {
            session.PendingEntry = null;
        }
    }

    private class TaskSession
    {
        public long ProjectTaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public TimesheetEntry? PendingEntry { get; set; } // ✅ เพิ่ม: เก็บข้อมูล Timesheet ที่รอบันทึก
    }
}
