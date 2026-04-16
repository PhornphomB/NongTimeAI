using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NongTimeAI.Models;
using NongTimeAI.Data;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Services;

public class TimesheetAIService : ITimesheetAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TimesheetAIService> _logger;
    private readonly TimesheetDbContext _dbContext;
    private readonly string _ollamaBaseUrl;
    private readonly string _modelName;
    private List<string>? _cachedIssueTypes = null;

    private const string ExtractionSystemPromptTemplate = @"คุณคือผู้ช่วยจัดการข้อมูล Timesheet หน้าที่ของคุณคือรับข้อความภาษาไทยแล้วแปลงเป็น JSON เท่านั้น

กฎเหล็ก:
- ตอบกลับเป็น JSON Object แถวเดียว ห้ามมีคำบรรยายอื่น ห้ามมี Markdown (```json)
- ข้อมูลต้องครบถ้วน: detail, hours > 0, issue_type ถึงจะ is_complete = true
- date เป็น optional สามารถไม่ระบุได้ (จะใช้วันนี้)
- **สำคัญ:** detail ต้องเป็นเฉพาะ ""รายละเอียดงาน"" เท่านั้น ห้ามรวมจำนวนชั่วโมงหรือวันที่

กฎเพิ่มเติมสำหรับความแม่นยำ:
- ""ครึ่งชั่วโมง"" หรือ ""30 นาที"" = 0.5 ชั่วโมง
- ""ชั่วโมงครึ่ง"" หรือ ""1 ชม.ครึ่ง"" = 1.5 ชั่วโมง
- หากข้อความสั้นเกินไป (เช่น แค่ ""ประชุม"" หรือ ""แก้บั๊ก"") ให้ถือว่า is_complete = false จนกว่าจะมีรายละเอียดเพิ่มเติม
- ห้ามเดาข้อมูลที่ผู้ใช้ไม่ได้ระบุมา ยกเว้น 'date' ที่อนุญาตให้ใช้วันนี้เป็นค่าเริ่มต้น
- หากไม่แน่ใจ issue_type จริงๆ หรือข้อมูลไม่สื่อถึงประเภทงานใดเลย ให้ใส่ issue_type: ""Other"" และตั้งค่า is_complete: false เพื่อให้ระบบถามซ้ำ

รูปแบบ JSON ที่ต้องการ:
{{
  ""detail"": ""รายละเอียดงาน (string, required, ห้ามรวมชั่วโมงและวันที่)"",
  ""hours"": ""จำนวนชั่วโมง (float, required, ต้อง > 0)"",
  ""issue_type"": ""ประเภทงาน เช่น Bug, Develop, Meeting, Other (string, required)"",
  ""date"": ""วันที่ทำงาน รูปแบบ YYYY-MM-DD (string, optional, null = วันนี้)"",
  ""is_complete"": ""true เมื่อมีครบ detail, hours > 0, issue_type; false เมื่อข้อมูลไม่ครบ (boolean)""
}}

เงื่อนไข is_complete = true:
- มี detail (ไม่ว่าง และมีรายละเอียดเพียงพอ)
- มี hours > 0
- มี issue_type (ต้องระบุชัดเจน จากรายการที่กำหนด)
- date สามารถเป็น null ได้ (จะใช้วันนี้)

**สำคัญ:** ถ้าข้อมูลครบ 3 ข้อแรก (detail + hours + issue_type) → is_complete = true
ไม่ว่า date จะมีหรือไม่ก็ตาม

ข้อมูลวันที่ปัจจุบัน (เวลาไทย ICT):
- วันนี้: {TODAY_DATE} ({TODAY_DAY})
- เมื่อวาน: {YESTERDAY_DATE}

วิธีแปลงวันที่ (รองรับการลงข้อมูลย้อนหลัง):
- ไม่ระบุ หรือ ""วันนี้"" → null
- ""เมื่อวาน"" / ""ทำเมื่อวาน"" / ""เมื่อวานนี้"" → {YESTERDAY_DATE}
- ""13/04/{CURRENT_YEAR}"" หรือ ""13/4/{CURRENT_YEAR}"" → ""{CURRENT_YEAR}-04-13""
- ""13/04"" หรือ ""13/4"" → ""{CURRENT_YEAR}-04-13""
- ""13 เม.ย."" / ""13 เมษายน"" / ""13 เมย"" → ""{CURRENT_YEAR}-04-13""
- ""วันจันทร์"" / ""จันทร์"" → จันทร์สัปดาห์ที่ผ่านมา (คำนวณจากวันนี้)
- ""วันอังคาร"" / ""อังคาร"" → อังคารสัปดาห์ที่ผ่านมา (คำนวณจากวันนี้)

**หมายเหตุ:** ถ้าพบคำว่า ""ทำเมื่อวาน"" ให้แยกเป็น:
  - issue_type ตามบริบท (เช่น ""แก้ไข"" → Develop)
  - date = {YESTERDAY_DATE}

**วิธีแยก detail, hours, date:**
- ตัดส่วนที่เป็น ""ชั่วโมง"", ""ชม."", ""ชม"", ""hours"" ออกจาก detail
- ตัดส่วนที่เป็นวันที่ เช่น ""วันนี้"", ""เมื่อวาน"", ""13/01"" ออกจาก detail
- detail ต้องเหลือเฉพาะ ""รายละเอียดงาน"" เท่านั้น

ตัวอย่างการแยก:
Input: ""ทำการศึกษาการใช้งาน react mobile 2 ชม. วันนี้""
→ detail: ""ทำการศึกษาการใช้งาน react mobile""
→ hours: 2.0
→ date: null (วันนี้)

Input: ""แก้บั๊ก login 3 ชั่วโมง เมื่อวาน""
→ detail: ""แก้บั๊ก login""
→ hours: 3.0
→ date: ""{YESTERDAY_DATE}""

Input: ""ประชุมทีม ครึ่งชั่วโมง""
→ detail: ""ประชุมทีม""
→ hours: 0.5
→ date: null

ประเภทงาน (issue_type) - ต้องระบุชัดเจน จากรายการที่กำหนดเท่านั้น:
{ISSUE_TYPES_MAPPING}

ตัวอย่างครบถ้วน (is_complete = true):
User: ""แก้บั๊กหน้าล็อกอิน 2 ชม.""
AI: {{""detail"": ""แก้บั๊กหน้าล็อกอิน"", ""hours"": 2.0, ""issue_type"": ""Bug"", ""date"": null, ""is_complete"": true}}

User: ""ประชุมทีม 1.5 ชั่วโมง เมื่อวาน""
AI: {{""detail"": ""ประชุมทีม"", ""hours"": 1.5, ""issue_type"": ""Meeting"", ""date"": ""{YESTERDAY_DATE}"", ""is_complete"": true}}

User: ""แก้บั๊ก WM3 3 ชม. {EXAMPLE_DAY_OF_MONTH}/04""
AI: {{""detail"": ""แก้บั๊ก WM3"", ""hours"": 3.0, ""issue_type"": ""Bug"", ""date"": ""{CURRENT_YEAR}-04-{EXAMPLE_DAY_OF_MONTH}"", ""is_complete"": true}}

User: ""ศึกษา PostgreSQL 8 ชม. วันจันทร์""
AI: {{""detail"": ""ศึกษา PostgreSQL"", ""hours"": 8.0, ""issue_type"": ""Training"", ""date"": ""{LAST_MONDAY_DATE}"", ""is_complete"": true}}

User: ""พัฒนา API Customer 5 ชม. 10 เม.ย.""
AI: {{""detail"": ""พัฒนา API Customer"", ""hours"": 5.0, ""issue_type"": ""Develop"", ""date"": ""{CURRENT_YEAR}-04-10"", ""is_complete"": true}}

User: ""ทำการแก้ไขชื่อและ email ให้ลูกค้า 2 ชม. ทำเมื่อวาน""
AI: {{""detail"": ""ทำการแก้ไขชื่อและ email ให้ลูกค้า"", ""hours"": 2.0, ""issue_type"": ""Develop"", ""date"": ""{YESTERDAY_DATE}"", ""is_complete"": true}}

User: ""แก้ไขหน้า login 3 ชม. ประเภท bug เมื่อวาน""
AI: {{""detail"": ""แก้ไขหน้า login"", ""hours"": 3.0, ""issue_type"": ""Bug"", ""date"": ""{YESTERDAY_DATE}"", ""is_complete"": true}}

User: ""ทำการศึกษาการใช้งาน react mobile 2 ชม. วันนี้""
AI: {{""detail"": ""ทำการศึกษาการใช้งาน react mobile"", ""hours"": 2.0, ""issue_type"": ""Training"", ""date"": null, ""is_complete"": true}}

User: ""ศึกษาการทำงานด้วย User Define View Manager 5 ชม.""
AI: {{""detail"": ""ศึกษาการทำงานด้วย User Define View Manager"", ""hours"": 5.0, ""issue_type"": ""Training"", ""date"": null, ""is_complete"": true}}

User: ""เรียนรู้ Docker และ Kubernetes 3 ชม.""
AI: {{""detail"": ""เรียนรู้ Docker และ Kubernetes"", ""hours"": 3.0, ""issue_type"": ""Training"", ""date"": null, ""is_complete"": true}}

User: ""ดูเอกสาร API Documentation 2 ชม.""
AI: {{""detail"": ""ดูเอกสาร API Documentation"", ""hours"": 2.0, ""issue_type"": ""Training"", ""date"": null, ""is_complete"": true}}

User: ""ประชุมลูกค้า ชั่วโมงครึ่ง""
AI: {{""detail"": ""ประชุมลูกค้า"", ""hours"": 1.5, ""issue_type"": ""Meeting"", ""date"": null, ""is_complete"": true}}

ตัวอย่างไม่ครบถ้วน (is_complete = false):
User: ""ทำ API""
AI: {{""detail"": ""ทำ API"", ""hours"": 0.0, ""issue_type"": ""Develop"", ""date"": null, ""is_complete"": false}}

User: ""แก้บั๊ก""
AI: {{""detail"": ""แก้บั๊ก"", ""hours"": 0.0, ""issue_type"": ""Bug"", ""date"": null, ""is_complete"": false}}

User: ""ประชุม""
AI: {{""detail"": ""ประชุม"", ""hours"": 0.0, ""issue_type"": ""Meeting"", ""date"": null, ""is_complete"": false}}

User: ""ทำงานต่างๆ 3 ชม.""
AI: {{""detail"": ""ทำงานต่างๆ"", ""hours"": 3.0, ""issue_type"": ""Other"", ""date"": null, ""is_complete"": false}}";

    /// <summary>
    /// ดึงรายการ issue types จากฐานข้อมูล
    /// </summary>
    private async Task<List<string>> GetIssueTypesAsync()
    {
        // ใช้ cache เพื่อลดการ query ฐานข้อมูล
        if (_cachedIssueTypes != null && _cachedIssueTypes.Any())
        {
            return _cachedIssueTypes;
        }

        try
        {
            _cachedIssueTypes = await _dbContext.ComboboxItems
                .Where(c => c.GroupName == "issue_type" && c.IsActive == "YES")
                .OrderBy(c => c.DisplaySequence)
                .Select(c => c.ValueMember!)
                .ToListAsync();

            _logger.LogInformation("✅ Loaded {Count} issue types from database: {Types}", 
                _cachedIssueTypes.Count, string.Join(", ", _cachedIssueTypes));

            return _cachedIssueTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load issue types from database, using default");
            // Fallback ถ้า query ฐานข้อมูลไม่สำเร็จ
            return new List<string> { "Bug", "Develop", "Meeting", "Training", "Support", "Request", "Issue", "Error", "Other" };
        }
    }

    /// <summary>
    /// สร้าง mapping text สำหรับ issue types ที่จะใช้ใน AI prompt
    /// </summary>
    private string GenerateIssueTypeMapping(List<string> issueTypes)
    {
        var mapping = new System.Text.StringBuilder();

        // สร้าง mapping แบบอัตโนมัติตามข้อมูลที่ได้จากฐานข้อมูล
        var keywordMappings = new Dictionary<string, List<string>>
        {
            { "Bug", new List<string> { "แก้บั๊ก", "บั๊ก", "แก้ไข bug", "fix bug", "bug", "แก้", "ซ่อม", "แก้ปัญหา" } },
            { "Develop", new List<string> { "ทำ", "พัฒนา", "develop", "development", "เขียน", "สร้าง", "code", "coding", "เพิ่ม", "ปรับปรุง", "update" } },
            { "Meeting", new List<string> { "ประชุม", "meeting", "meet", "หารือ", "พูดคุย", "discussion" } },
            { "Training", new List<string> { 
                "ศึกษา", "ทำการศึกษา", "การศึกษา", "ฝึกอบรม", "training", "อบรม", 
                "เรียนรู้", "workshop", "เรียน", "ดูเอกสาร", "อ่าน", "ทดสอบการใช้งาน", 
                "ลองใช้", "ศึกษาการทำงาน", "ทดลอง", "research" 
            } },
            { "Support", new List<string> { "สนับสนุน", "support", "ช่วย", "ช่วยเหลือ", "ดูแล", "แนะนำ" } },
            { "Request", new List<string> { "ร้องขอ", "request", "ขอ", "เสนอ" } },
            { "Issue", new List<string> { "ปัญหา", "issue", "problem", "ติดปัญหา" } },
            { "Error", new List<string> { "ข้อผิดพลาด", "error", "ผิดพลาด", "พลาด" } },
            { "Other", new List<string> { "อื่นๆ", "other", "งานอื่น" } }
        };

        // สร้าง mapping แบบละเอียด
        mapping.AppendLine("**วิธีจับ issue_type จากคำสำคัญ:**");
        mapping.AppendLine();

        foreach (var issueType in issueTypes)
        {
            if (keywordMappings.ContainsKey(issueType))
            {
                var keywords = string.Join("\", \"", keywordMappings[issueType]);
                mapping.AppendLine($"**{issueType}:** ถ้าเจอคำว่า \"{keywords}\"");
            }
            else
            {
                // ถ้าไม่มีใน mapping ให้ใช้ชื่อเดียวกัน
                mapping.AppendLine($"**{issueType}:** ถ้าเจอคำว่า \"{issueType.ToLower()}\", \"{issueType}\"");
            }
        }

        mapping.AppendLine();
        mapping.AppendLine("**กฎพิเศษ:**");
        mapping.AppendLine("- ถ้าเจอคำว่า \"ศึกษา\", \"ทำการศึกษา\", \"เรียนรู้\", \"อบรม\" → **Training**");
        mapping.AppendLine("- ถ้าเจอคำว่า \"แก้บั๊ก\", \"บั๊ก\", \"fix\" → **Bug**");
        mapping.AppendLine("- ถ้าเจอคำว่า \"พัฒนา\", \"เขียน\", \"สร้าง\" → **Develop**");
        mapping.AppendLine("- ถ้าเจอคำว่า \"ประชุม\" → **Meeting**");
        mapping.AppendLine("- ถ้าไม่แน่ใจหรือไม่มีคำสำคัญ → **Other**");

        mapping.AppendLine();
        mapping.AppendLine("**รายการ issue_type ที่ใช้ได้เท่านั้น:**");
        mapping.AppendLine(string.Join(", ", issueTypes));

        return mapping.ToString();
    }

    private async Task<string> GetExtractionSystemPromptAsync()
    {
        // ✅ ใช้เวลาไทย (ICT) แทน server time เพื่อความแม่นยำ
        var thaiZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, thaiZone);
        var yesterday = now.AddDays(-1);
        var currentYear = now.Year;
        var exampleDay = Math.Min(now.Day, 13); // ใช้วันที่ 13 หรือวันปัจจุบันถ้าน้อยกว่า

        // หา Monday ที่ผ่านมา
        var daysUntilMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7; // ถ้าวันนี้เป็น Monday ให้ใช้ Monday สัปดาห์ก่อน
        var lastMonday = now.AddDays(-daysUntilMonday);

        var todayDay = now.ToString("dddd", new System.Globalization.CultureInfo("th-TH"));

        // ✅ ดึง issue types จากฐานข้อมูล
        var issueTypes = await GetIssueTypesAsync();
        var issueTypesMapping = GenerateIssueTypeMapping(issueTypes);

        return ExtractionSystemPromptTemplate
            .Replace("{TODAY_DATE}", now.ToString("yyyy-MM-dd"))
            .Replace("{TODAY_DAY}", todayDay)
            .Replace("{YESTERDAY_DATE}", yesterday.ToString("yyyy-MM-dd"))
            .Replace("{CURRENT_YEAR}", currentYear.ToString())
            .Replace("{EXAMPLE_DAY_OF_MONTH}", exampleDay.ToString("00"))
            .Replace("{LAST_MONDAY_DATE}", lastMonday.ToString("yyyy-MM-dd"))
            .Replace("{ISSUE_TYPES_MAPPING}", issueTypesMapping);
    }

    /// <summary>
    /// Pre-process ข้อความเพื่อแปลงรูปแบบวันที่และเวลาให้ชัดเจนก่อนส่งให้ AI
    /// รองรับการเขียนวันแบบย่อและหลากหลายรูปแบบ เช่น วันพฤ., พฤ, พฤหัส, จ., อ., ฯลฯ
    /// </summary>
    private string PreprocessDateInMessage(string message)
    {
        // ✅ ใช้เวลาไทย (ICT) แทน server time
        var thaiZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, thaiZone);
        var yesterday = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        var processedMessage = message;

        // ✅ แปลงเวลา - จัดการกรณีต่างๆ
        // รองรับ "45 นาที" -> 0.75 ชม.
        processedMessage = Regex.Replace(processedMessage, @"45\s*นาที", "0.75 ชม.", RegexOptions.IgnoreCase);

        // รองรับ "15 นาที" -> 0.25 ชม.
        processedMessage = Regex.Replace(processedMessage, @"15\s*นาที", "0.25 ชม.", RegexOptions.IgnoreCase);

        // แปลง "ครึ่งชั่วโมง", "ชั่วโมงครึ่ง", "30 นาที" เป็นตัวเลข
        processedMessage = Regex.Replace(processedMessage, @"ครึ่ง\s*ชั่วโมง|ครึ่ง\s*ชม\.?", "0.5 ชม.", RegexOptions.IgnoreCase);
        processedMessage = Regex.Replace(processedMessage, @"(\d+)\s*ชั่วโมง\s*ครึ่ง|(\d+)\s*ชม\.?\s*ครึ่ง", m => 
        {
            var hours = int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            return $"{hours}.5 ชม.";
        }, RegexOptions.IgnoreCase);
        processedMessage = Regex.Replace(processedMessage, @"30\s*นาที", "0.5 ชม.", RegexOptions.IgnoreCase);

        // จัดการ "ชั่วโมง" "ชม." ให้เป็น Format เดียวกัน
        processedMessage = Regex.Replace(processedMessage, @"ชั่วโมง", "ชม.", RegexOptions.IgnoreCase);

        // ✅ แปลงวันที่ - เรียงจากคำยาวไปคำสั้น เพื่อป้องกัน Regex Match ผิด
        var patterns = new Dictionary<string, string>
        {
            // เมื่อวาน (รองรับหลายรูปแบบ)
            { @"\s*ทำ\s*เมื่อ\s*วาน\s*นี้\s*", $" วันที่ {yesterday:dd/MM/yyyy}" },
            { @"\s*ทำ\s*เมื่อ\s*วาน\s*", $" วันที่ {yesterday:dd/MM/yyyy}" },
            { @"\s*เมื่อ\s*วาน\s*นี้\s*", $" วันที่ {yesterday:dd/MM/yyyy}" },
            { @"\s*เมื่อ\s*วาน\s*", $" วันที่ {yesterday:dd/MM/yyyy}" },
            { @"\s*มะ\s*วาน\s*", $" วันที่ {yesterday:dd/MM/yyyy}" },

            // วันนี้
            { @"\s*วัน\s*นี้\s*", $" วันที่ {now:dd/MM/yyyy}" },

            // วันก่อน (2 วันที่แล้ว)
            { @"\s*วัน\s*ก่อน\s*", $" วันที่ {twoDaysAgo:dd/MM/yyyy}" },

            // วันจันทร์ (รองรับ: วันจันทร์, จันทร์, วันจ., จ., จันทร์ที่แล้ว)
            { @"\s*วัน\s*จันทร์\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Monday):dd/MM/yyyy}" },
            { @"\s*วัน\s*จันทร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Monday):dd/MM/yyyy}" },
            { @"\s*วัน\s*จ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Monday):dd/MM/yyyy}" },
            { @"\s*จันทร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Monday):dd/MM/yyyy}" },
            { @"\s*จ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Monday):dd/MM/yyyy}" },

            // วันอังคาร (รองรับ: วันอังคาร, อังคาร, วันอ., อ., ต.)
            { @"\s*วัน\s*อังคาร\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },
            { @"\s*วัน\s*อังคาร\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },
            { @"\s*วัน\s*อ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },
            { @"\s*อังคาร\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },
            { @"\s*อ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },
            { @"\s*ต\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Tuesday):dd/MM/yyyy}" },

            // วันพุธ (รองรับ: วันพุธ, พุธ, วันพ., พ.)
            { @"\s*วัน\s*พุธ\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Wednesday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พุธ\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Wednesday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Wednesday):dd/MM/yyyy}" },
            { @"\s*พุธ\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Wednesday):dd/MM/yyyy}" },

            // วันพฤหัสบดี (รองรับ: วันพฤหัสบดี, พฤหัสบดี, วันพฤหัส, พฤหัส, วันพฤ., พฤ., พฤ, พฤหัสฯ)
            { @"\s*วัน\s*พฤหัสบดี\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พฤหัสบดี\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พฤหัสฯ\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พฤหัส\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พฤ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*วัน\s*พฤ\s+", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy} " }, // พฤ + space
            { @"\s*พฤหัสบดี\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*พฤหัสฯ\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*พฤหัส\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },
            { @"\s*พฤ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Thursday):dd/MM/yyyy}" },

            // วันศุกร์ (รองรับ: วันศุกร์, ศุกร์, วันศ., ศ.)
            { @"\s*วัน\s*ศุกร์\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Friday):dd/MM/yyyy}" },
            { @"\s*วัน\s*ศุกร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Friday):dd/MM/yyyy}" },
            { @"\s*วัน\s*ศ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Friday):dd/MM/yyyy}" },
            { @"\s*ศุกร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Friday):dd/MM/yyyy}" },
            { @"\s*ศ\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Friday):dd/MM/yyyy}" },

            // วันเสาร์ (รองรับ: วันเสาร์, เสาร์, วันส., ส.)
            { @"\s*วัน\s*เสาร์\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Saturday):dd/MM/yyyy}" },
            { @"\s*วัน\s*เสาร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Saturday):dd/MM/yyyy}" },
            { @"\s*วัน\s*ส\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Saturday):dd/MM/yyyy}" },
            { @"\s*เสาร์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Saturday):dd/MM/yyyy}" },
            { @"\s*ส\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Saturday):dd/MM/yyyy}" },

            // วันอาทิตย์ (รองรับ: วันอาทิตย์, อาทิตย์, วันอา., อา., อท.)
            { @"\s*วัน\s*อาทิตย์\s*ที่\s*แล้ว\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*วัน\s*อาทิตย์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*วัน\s*อา\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*วัน\s*อท\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*อาทิตย์\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*อา\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" },
            { @"\s*อท\.\s*", $" วันที่ {GetLastWeekday(DayOfWeek.Sunday):dd/MM/yyyy}" }
        };

        foreach (var pattern in patterns)
        {
            processedMessage = Regex.Replace(processedMessage, pattern.Key, pattern.Value, RegexOptions.IgnoreCase);
        }

        return processedMessage.Trim();
    }

    /// <summary>
    /// หาวันในสัปดาห์ที่ผ่านมาล่าสุด (ใช้เวลาไทย)
    /// </summary>
    private DateTime GetLastWeekday(DayOfWeek targetDay)
    {
        // ✅ ใช้เวลาไทย (ICT) แทน server time
        var thaiZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, thaiZone);
        var daysUntilTarget = ((int)now.DayOfWeek - (int)targetDay + 7) % 7;

        // ถ้าวันนี้คือวันเป้าหมาย ให้ใช้สัปดาห์ก่อน
        if (daysUntilTarget == 0) 
            daysUntilTarget = 7;

        return now.AddDays(-daysUntilTarget).Date;
    }

    /// <summary>
    /// Pre-detect Issue Type จาก keywords เพื่อช่วย AI ตัดสินใจ
    /// </summary>
    private string? PreDetectIssueType(string message)
    {
        var lowerMessage = message.ToLower();

        // Training keywords (เรียงจากชัดเจน → กำกวม)
        var trainingKeywords = new[] { 
            "ศึกษา", "ทำการศึกษา", "การศึกษา", "ศึกษาการทำงาน",
            "เรียนรู้", "ดูเอกสาร", "อ่านเอกสาร", "ทดลอง", "ทดสอบการใช้งาน",
            "ฝึกอบรม", "อบรม", "เรียน", "workshop", "training", "research"
        };

        foreach (var keyword in trainingKeywords)
        {
            if (lowerMessage.Contains(keyword.ToLower()))
            {
                _logger.LogDebug("🎯 Matched Training keyword: {Keyword}", keyword);
                return "Training";
            }
        }

        // Bug keywords
        var bugKeywords = new[] { "แก้บั๊ก", "บั๊ก", "fix bug", "bug", "ซ่อม", "แก้ปัญหา" };
        foreach (var keyword in bugKeywords)
        {
            if (lowerMessage.Contains(keyword.ToLower()))
            {
                _logger.LogDebug("🎯 Matched Bug keyword: {Keyword}", keyword);
                return "Bug";
            }
        }

        // Meeting keywords
        var meetingKeywords = new[] { "ประชุม", "meeting", "หารือ", "พูดคุย", "discussion" };
        foreach (var keyword in meetingKeywords)
        {
            if (lowerMessage.Contains(keyword.ToLower()))
            {
                _logger.LogDebug("🎯 Matched Meeting keyword: {Keyword}", keyword);
                return "Meeting";
            }
        }

        // Develop keywords
        var developKeywords = new[] { "พัฒนา", "develop", "เขียน", "สร้าง", "code", "coding" };
        foreach (var keyword in developKeywords)
        {
            if (lowerMessage.Contains(keyword.ToLower()))
            {
                _logger.LogDebug("🎯 Matched Develop keyword: {Keyword}", keyword);
                return "Develop";
            }
        }

        // Support keywords
        var supportKeywords = new[] { "สนับสนุน", "support", "ช่วย", "ช่วยเหลือ", "ดูแล" };
        foreach (var keyword in supportKeywords)
        {
            if (lowerMessage.Contains(keyword.ToLower()))
            {
                _logger.LogDebug("🎯 Matched Support keyword: {Keyword}", keyword);
                return "Support";
            }
        }

        _logger.LogDebug("❓ No issue type keyword matched");
        return null;
    }

    public TimesheetAIService(
        IConfiguration configuration, 
        ILogger<TimesheetAIService> logger,
        TimesheetDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
        _ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _modelName = configuration["Ollama:Model"] ?? "llama3.2";

        // ✅ เพิ่ม Timeout เป็น 5 นาที สำหรับ AI ที่ประมวลผลช้า
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_ollamaBaseUrl),
            Timeout = TimeSpan.FromMinutes(5) // เปลี่ยนจาก 2 → 5 นาที (300 วินาที)
        };

        _logger.LogInformation("🤖 TimesheetAIService initialized: BaseUrl={BaseUrl}, Model={Model}, Timeout={Timeout}s", 
            _ollamaBaseUrl, _modelName, _httpClient.Timeout.TotalSeconds);
    }

    public async Task<TimesheetResponse> ProcessTimesheetMessageAsync(string userMessage)
    {
        try
        {
            // ✅ Pre-process ข้อความเพื่อแปลงวันที่ให้ชัดเจนก่อนส่งให้ AI
            var processedMessage = PreprocessDateInMessage(userMessage);
            _logger.LogInformation("📝 Original message: {Original}", userMessage);
            _logger.LogInformation("📝 Processed message: {Processed}", processedMessage);

            // ✅ Pre-detect Issue Type จาก keywords เพื่อช่วย AI
            var preDetectedIssueType = PreDetectIssueType(userMessage);
            if (!string.IsNullOrEmpty(preDetectedIssueType))
            {
                _logger.LogInformation("🎯 Pre-detected issue type: {IssueType}", preDetectedIssueType);
            }

            // ✅ ใช้ Dynamic Prompt ที่คำนวณวันที่แบบ Real-time และดึง issue types จากฐานข้อมูล
            var systemPrompt = await GetExtractionSystemPromptAsync();

            // ✅ ถ้า Pre-detect ได้ ให้เพิ่ม hint ใน prompt
            var hintMessage = !string.IsNullOrEmpty(preDetectedIssueType)
                ? $"\n\n**IMPORTANT HINT:** The message contains keyword that strongly suggests issue_type should be \"{preDetectedIssueType}\". Please use this unless there is a clear reason not to."
                : "";

            var prompt = $"{systemPrompt}{hintMessage}\n\nUser Message: '{processedMessage}'";

            var ollamaRequest = new OllamaRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.0,  // ✅ เปลี่ยนเป็น 0 เพื่อให้ deterministic
                    TopP = 0.1
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(ollamaRequest, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/api/generate", jsonContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            if (ollamaResponse?.Response == null)
            {
                return new TimesheetResponse
                {
                    Success = false,
                    Message = "ไม่สามารถติดต่อ AI ได้",
                    BotReply = "ขอโทษครับ ระบบมีปัญหา กรุณาลองใหม่อีกครั้ง"
                };
            }

            var cleanedJson = CleanJsonResponse(ollamaResponse.Response);

            _logger.LogInformation("🤖 AI Response (cleaned): {Json}", cleanedJson);

            // Parse JSON manually เพื่อจัดการ date field ที่อาจเป็น string "YYYY-MM-DD" หรือ null
            var timesheetEntry = ParseTimesheetEntry(cleanedJson);

            if (timesheetEntry == null)
            {
                return new TimesheetResponse
                {
                    Success = false,
                    Message = "ไม่สามารถแปลงข้อมูลได้",
                    BotReply = "ขอโทษครับ ไม่เข้าใจข้อความ กรุณาลองใหม่อีกครั้ง"
                };
            }

            _logger.LogInformation("📊 Parsed Entry: Detail={Detail}, Hours={Hours}, IssueType={IssueType}, Date={Date}, IsComplete={IsComplete}",
                timesheetEntry.Detail, timesheetEntry.Hours, timesheetEntry.IssueType, 
                timesheetEntry.Date?.ToString("yyyy-MM-dd") ?? "null", timesheetEntry.IsComplete);

            // ✅ ถ้า Pre-detect ได้แต่ AI ตอบมาเป็น "Other" ให้บังคับใช้ Pre-detected Issue Type
            if (!string.IsNullOrEmpty(preDetectedIssueType) && 
                timesheetEntry.IssueType == "Other")
            {
                _logger.LogWarning("⚠️ AI returned 'Other' but we pre-detected '{PreDetected}', overriding...", preDetectedIssueType);
                timesheetEntry.IssueType = preDetectedIssueType;
            }

            // ✅ ตรวจสอบว่า issue_type ที่ได้มาตรงกับรายการในฐานข้อมูลหรือไม่
            var validIssueTypes = await GetIssueTypesAsync();
            if (!string.IsNullOrWhiteSpace(timesheetEntry.IssueType) && 
                !validIssueTypes.Contains(timesheetEntry.IssueType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("⚠️ Invalid issue type from AI: {IssueType}, valid types: {ValidTypes}", 
                    timesheetEntry.IssueType, string.Join(", ", validIssueTypes));

                // พยายามหา issue type ที่ใกล้เคียงที่สุด
                var matchedIssueType = validIssueTypes.FirstOrDefault(t => 
                    t.Equals(timesheetEntry.IssueType, StringComparison.OrdinalIgnoreCase));

                if (matchedIssueType != null)
                {
                    _logger.LogInformation("✅ Matched issue type: {Original} -> {Matched}", 
                        timesheetEntry.IssueType, matchedIssueType);
                    timesheetEntry.IssueType = matchedIssueType;
                }
                else
                {
                    // ถ้าไม่เจอเลย ให้ใช้ค่า default เป็น "Other"
                    _logger.LogWarning("⚠️ No match found, setting to 'Other'");
                    timesheetEntry.IssueType = validIssueTypes.Contains("Other") ? "Other" : validIssueTypes.FirstOrDefault() ?? "Other";
                    timesheetEntry.IsComplete = false; // ต้องให้ user ยืนยันอีกครั้ง
                }
            }

            var botReply = GenerateBotReply(timesheetEntry);

            return new TimesheetResponse
            {
                Success = true,
                Data = timesheetEntry,
                Message = "ประมวลผลสำเร็จ",
                BotReply = botReply
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing timesheet message: {Message}", userMessage);
            return new TimesheetResponse
            {
                Success = false,
                Message = $"เกิดข้อผิดพลาด: {ex.Message}",
                BotReply = "ขอโทษครับ ระบบขัดข้อง กรุณาลองใหม่อีกครั้ง"
            };
        }
    }

    public async Task<string> GenerateReminderMessageAsync(string employeeName, string projectName)
    {
        try
        {
            var prompt = $@"วันนี้ {employeeName} ยังไม่ได้ลงเวลางานในโปรเจกต์ {projectName} 
ช่วยเขียนประโยคทักทายสั้นๆ เป็นกันเอง 1 ประโยค เพื่อเตือนให้เขาลงเวลาหน่อย (แนว Cozy/Friendly)
ตอบเฉพาะข้อความเดียว ไม่ต้องมีคำอธิบายอื่น";

            var ollamaRequest = new OllamaRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.7,
                    TopP = 0.9
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(ollamaRequest, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                }),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/api/generate", jsonContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            return ollamaResponse?.Response?.Trim() ?? 
                   $"สวัสดีครับคุณ {employeeName} อย่าลืมมาบันทึกเวลาของโปรเจกต์ {projectName} วันนี้ด้วยนะครับ 😊";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reminder message");
            return $"สวัสดีครับคุณ {employeeName} อย่าลืมมาบันทึกเวลาของโปรเจกต์ {projectName} วันนี้ด้วยนะครับ 😊";
        }
    }

    private string CleanJsonResponse(string response)
    {
        var trimmed = response.Trim();

        var jsonMatch = Regex.Match(trimmed, @"\{[^}]+\}", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            return jsonMatch.Value;
        }

        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed.Substring(7);
        }
        if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3);
        }
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }

    private TimesheetEntry? ParseTimesheetEntry(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var entry = new TimesheetEntry
            {
                Detail = root.TryGetProperty("detail", out var detailProp) 
                    ? detailProp.GetString() ?? string.Empty 
                    : string.Empty,

                Hours = root.TryGetProperty("hours", out var hoursProp)
                    ? hoursProp.GetSingle()
                    : 0.0f,

                IssueType = root.TryGetProperty("issue_type", out var issueTypeProp)
                    ? issueTypeProp.GetString() ?? "Task"
                    : "Task",

                IsComplete = root.TryGetProperty("is_complete", out var isCompleteProp)
                    ? isCompleteProp.GetBoolean()
                    : false
            };

            // ✅ Fallback: ทำความสะอาด detail โดยตัดส่วนชั่วโมงและวันที่ออก
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                entry.Detail = CleanDetailField(entry.Detail);
            }

            // Parse date field - รองรับ null, string "YYYY-MM-DD", หรือไม่มี field
            if (root.TryGetProperty("date", out var dateProp))
            {
                if (dateProp.ValueKind == JsonValueKind.String)
                {
                    var dateStr = dateProp.GetString();
                    if (!string.IsNullOrWhiteSpace(dateStr))
                    {
                        // Parse รูปแบบ YYYY-MM-DD
                        if (DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            entry.Date = parsedDate.Date;
                            _logger.LogDebug("✅ Parsed date: {DateStr} -> {Date:yyyy-MM-dd}", dateStr, entry.Date.Value);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Failed to parse date: {DateStr}", dateStr);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("📅 Date is null or empty string");
                    }
                }
                else if (dateProp.ValueKind == JsonValueKind.Null)
                {
                    _logger.LogDebug("📅 Date is null");
                }
            }
            else
            {
                _logger.LogDebug("📅 Date field not found in JSON");
            }

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse TimesheetEntry from JSON: {Json}", json);
            return null;
        }
    }

    /// <summary>
    /// ทำความสะอาด detail field โดยตัดส่วนชั่วโมงและวันที่ออก
    /// </summary>
    private string CleanDetailField(string detail)
    {
        // ตัดส่วนที่เป็นชั่วโมงออก - รองรับหลายรูปแบบ
        var patterns = new[]
        {
            @"\s*\d+\.?\d*\s*(ชม\.?|ชั่วโมง|hours?|hrs?)\s*ครึ่ง\s*",  // "1 ชม.ครึ่ง", "2 ชั่วโมงครึ่ง"
            @"\s*ครึ่ง\s*(ชม\.?|ชั่วโมง|hours?)\s*",                  // "ครึ่งชม.", "ครึ่งชั่วโมง"
            @"\s*\d+\.?\d*\s*(ชม\.?|ชั่วโมง|hours?|hrs?)\s*",        // "2 ชม.", "1.5 ชั่วโมง", "3 hours"
            @"\s*\d+\s*นาที\s*",                                      // "30 นาที"
            @"\s*(วันนี้|เมื่อวาน|เมื่อวานนี้)\s*$",                 // "วันนี้", "เมื่อวาน"
            @"\s*\d{1,2}\/\d{1,2}(\/\d{4})?\s*$",                    // "13/04", "13/04/2026"
            @"\s*\d{1,2}\s*(ม\.ค\.|ก\.พ\.|มี\.ค\.|เม\.ย\.|พ\.ค\.|มิ\.ย\.|ก\.ค\.|ส\.ค\.|ก\.ย\.|ต\.ค\.|พ\.ย\.|ธ\.ค\.)\s*$",  // "13 เม.ย."
            @"\s*วัน(จันทร์|อังคาร|พุธ|พฤหัสบดี|ศุกร์|เสาร์|อาทิตย์)\s*$",  // "วันจันทร์"
            @"\s*(จันทร์|อังคาร|พุธ|พฤหัสบดี|ศุกร์|เสาร์|อาทิตย์)\s*$"      // "จันทร์" (ไม่มีคำว่า "วัน")
        };

        var cleaned = detail;
        foreach (var pattern in patterns)
        {
            cleaned = Regex.Replace(cleaned, pattern, "", RegexOptions.IgnoreCase);
        }

        cleaned = cleaned.Trim();

        if (cleaned != detail)
        {
            _logger.LogInformation("🧹 Cleaned detail: \"{Original}\" -> \"{Cleaned}\"", detail, cleaned);
        }

        return cleaned;
    }

    private string GenerateBotReply(TimesheetEntry entry)
    {
        if (!entry.IsComplete)
        {
            var missingFields = new List<string>();

            if (string.IsNullOrEmpty(entry.Detail))
                missingFields.Add("รายละเอียดงาน");

            if (entry.Hours <= 0)
                missingFields.Add("จำนวนชั่วโมง");

            if (string.IsNullOrEmpty(entry.IssueType) || entry.IssueType == "Other")
                missingFields.Add("ประเภทงาน (เช่น Bug, Develop, Meeting)");

            if (missingFields.Any())
            {
                var fieldsText = string.Join(", ", missingFields);
                return $"ข้อมูลยังไม่ครบครับ ❌\n\nต้องระบุ: {fieldsText}\n\nตัวอย่าง:\n\"แก้บั๊ก login 2 ชม.\"\n\"ประชุมทีม 1.5 ชั่วโมง\"";
            }
        }

        // แสดงวันที่ถ้ามี
        var dateDisplay = entry.Date.HasValue 
            ? $" ({entry.Date.Value:dd/MM/yyyy})" 
            : " (วันนี้)";

        return $"✅ รับทราบข้อมูลแล้วครับ:\n\n📝 {entry.Detail}\n⏱️ {entry.Hours} ชั่วโมง{dateDisplay}\n🏷️ {entry.IssueType}\n\n💡 กรุณาเลือกงานที่จะบันทึก";
    }
}
