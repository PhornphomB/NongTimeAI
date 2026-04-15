using Line.Messaging;
using System.Collections.Generic;
using System.Linq;
using Line.Messaging;

namespace NongTimeAI.Helpers;

public static class LineMessageHelper
{
    /// <summary>
    /// สร้าง Quick Reply สำหรับเลือก Issue Type
    /// </summary>
    public static QuickReply GetIssueTypeQuickReply(List<string> issueTypes)
    {
        var items = new List<QuickReplyButtonObject>();

        // แสดงได้สูงสุด 13 items (ข้อจำกัดของ LINE)
        var maxItems = Math.Min(issueTypes.Count, 12); // เหลือ 1 ช่องสำหรับ "ยกเลิก"

        // Icon mapping สำหรับแต่ละ issue type
        var iconMap = new Dictionary<string, string>
        {
            { "Bug", "🐛" },
            { "Develop", "💻" },
            { "Meeting", "👥" },
            { "Training", "📚" },
            { "Support", "🆘" },
            { "Request", "📮" },
            { "Issue", "⚠️" },
            { "Error", "❌" },
            { "Other", "📌" }
        };

        foreach (var issueType in issueTypes.Take(maxItems))
        {
            var icon = iconMap.ContainsKey(issueType) ? iconMap[issueType] : "📋";
            var label = $"{icon} {issueType}";

            items.Add(new QuickReplyButtonObject(
                new MessageTemplateAction(label, $"ISSUE_TYPE:{issueType}")
            ));
        }

        // เพิ่มปุ่ม "ยกเลิก"
        items.Add(new QuickReplyButtonObject(
            new MessageTemplateAction("❌ ยกเลิก", "cancel")
        ));

        return new QuickReply { Items = items };
    }

    /// <summary>
    /// สร้าง Quick Reply สำหรับดูงานค้าง
    /// </summary>
    public static QuickReply GetTaskQuickReply()
    {
        return new QuickReply
        {
            Items = new List<QuickReplyButtonObject>
            {
                new QuickReplyButtonObject(new MessageTemplateAction("📋 งานของฉัน", "งานของฉัน")),
                new QuickReplyButtonObject(new MessageTemplateAction("📊 สรุปงาน", "สรุปงานสัปดาห์นี้")),
                new QuickReplyButtonObject(new MessageTemplateAction("✅ งานวันนี้", "งานวันนี้")),
                new QuickReplyButtonObject(new MessageTemplateAction("❓ ช่วยเหลือ", "help"))
            }
        };
    }

    /// <summary>
    /// สร้าง Quick Reply สำหรับเลือกงานที่จะบันทึก
    /// LINE จำกัด Quick Reply ไว้ที่ 13 items สูงสุด
    /// </summary>
    public static QuickReply GetTaskSelectionQuickReply(List<TaskItem> tasks)
    {
        var items = new List<QuickReplyButtonObject>();

        // แสดงได้สูงสุด 11 งาน (เหลือ 2 ช่องสำหรับปุ่ม "ดูทั้งหมด" และ "ยกเลิก")
        var maxTasksToShow = Math.Min(tasks.Count, 11);

        // จัดลำดับความสำคัญ: High -> Medium -> Low
        var sortedTasks = tasks
            .OrderByDescending(t => t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : 1)
            .ThenBy(t => t.EndDate ?? DateTime.MaxValue)
            .Take(maxTasksToShow)
            .ToList();

        foreach (var task in sortedTasks)
        {
            var priorityIcon = task.Priority switch
            {
                "High" => "🔴",
                "Medium" => "🟡",
                "Low" => "🟢",
                _ => "📌"
            };

            var label = $"{priorityIcon} {task.TaskName}";
            if (label.Length > 20) // LINE Quick Reply label limit = 20 characters
            {
                label = label.Substring(0, 17) + "...";
            }

            items.Add(new QuickReplyButtonObject(
                new MessageTemplateAction(label, $"SELECT_TASK:{task.TaskId}")
            ));
        }

        // เพิ่มปุ่ม "ดูทั้งหมด" ถ้ามีงานมากกว่าที่แสดง
        if (tasks.Count > maxTasksToShow)
        {
            items.Add(new QuickReplyButtonObject(
                new MessageTemplateAction("📋 ดูทั้งหมด", "งานของฉัน")
            ));
        }

        // เพิ่มปุ่ม "ยกเลิก"
        items.Add(new QuickReplyButtonObject(
            new MessageTemplateAction("❌ ยกเลิก", "cancel")
        ));

        return new QuickReply { Items = items };
    }

    /// <summary>
    /// สร้างข้อความแสดงรายการงานที่ค้าง (Text Message)
    /// </summary>
    public static string CreateTaskListMessage(List<TaskItem> tasks, string userName)
    {
        if (!tasks.Any())
        {
            return $"✅ ยินดีด้วยครับคุณ {userName}!\n\nคุณไม่มีงานที่ค้างอยู่แล้ว 🎉\n\nพิมพ์ \"help\" เพื่อดูคำสั่งที่ใช้ได้";
        }

        var message = $"📋 **งานค้างของคุณ {userName}**\n";
        message += $"━━━━━━━━━━━━━━━━━━━━\n\n";
        message += $"รวมทั้งหมด: **{tasks.Count} งาน**\n\n";

        // แยกตาม Priority
        var highTasks = tasks.Where(t => t.Priority == "High").ToList();
        var mediumTasks = tasks.Where(t => t.Priority == "Medium").ToList();
        var lowTasks = tasks.Where(t => t.Priority == "Low").ToList();
        var otherTasks = tasks.Where(t => t.Priority != "High" && t.Priority != "Medium" && t.Priority != "Low").ToList();

        if (highTasks.Any())
        {
            message += "🔴 **งานเร่งด่วน (High):**\n";
            message += FormatTaskList(highTasks);
            message += "\n";
        }

        if (mediumTasks.Any())
        {
            message += "🟡 **งานปานกลาง (Medium):**\n";
            message += FormatTaskList(mediumTasks, maxItems: 3);
            message += "\n";
        }

        if (lowTasks.Any())
        {
            message += $"🟢 **งานไม่เร่งด่วน:** {lowTasks.Count} งาน\n\n";
        }

        message += "💡 **วิธีบันทึก Timesheet:**\n";
        message += "📝 ส่งข้อความพร้อมข้อมูล:\n";
        message += "   1️⃣ รายละเอียดงาน (required)\n";
        message += "   2️⃣ จำนวนชั่วโมง (required)\n";
        message += "   3️⃣ ประเภทงาน (required)\n";
        message += "   4️⃣ วันที่ (optional - ไม่ระบุ = วันนี้)\n\n";
        message += "✏️ ตัวอย่าง:\n";
        message += "• \"แก้บั๊ก login 2 ชม.\"\n";
        message += "• \"ประชุมทีม 1.5 ชม. เมื่อวาน\"\n";
        message += "• \"พัฒนา API 4 ชม. 13/01\"\n";
        message += "• \"ศึกษา PostgreSQL 8 ชม. วันจันทร์\"\n\n";
        message += "🎯 ระบบจะให้เลือกงานที่จะบันทึก\n";
        message += "🗓️ สามารถลงข้อมูลย้อนหลังได้\n";
        message += "📊 กดปุ่มด้านล่างเพื่อดูข้อมูลเพิ่มเติม";

        return message;
    }

    private static string FormatTaskList(List<TaskItem> tasks, int maxItems = 5)
    {
        var result = "";
        var count = 0;

        foreach (var task in tasks.Take(maxItems))
        {
            count++;
            var daysLeft = task.EndDate.HasValue
                ? (task.EndDate.Value.Date - DateTime.Now.Date).Days
                : 0;

            var dueDateText = daysLeft < 0
                ? $"⚠️ เลย {Math.Abs(daysLeft)} วัน"
                : daysLeft == 0
                    ? "🚨 วันนี้!"
                    : $"⏰ {daysLeft} วัน";

            result += $"\n{count}. {task.TaskName}\n";
            result += $"   📁 {task.ProjectName}\n";
            result += $"   🏷️ {task.IssueType} | {dueDateText}\n";
        }

        if (tasks.Count > maxItems)
        {
            result += $"\n...และอีก {tasks.Count - maxItems} งาน\n";
        }

        return result;
    }

    /// <summary>
    /// สร้างข้อความพร้อม Quick Reply
    /// </summary>
    public static TextMessage CreateTextWithQuickReply(string text)
    {
        return new TextMessage(text, GetTaskQuickReply());
    }
}

public class TaskItem
{
    public long TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string IssueType { get; set; } = "Other";
    public DateTime? EndDate { get; set; }
}
