using Line.Messaging;
using Line.Messaging;
using Line.Messaging.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NongTimeAI.Data;
using NongTimeAI.Services;
using NongTimeAI.Helpers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace NongTimeAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LineWebhookController : ControllerBase
{
    private readonly ITimesheetAIService _timesheetService;
    private readonly ILineService _lineService;
    private readonly ITaskNotificationService _taskNotificationService;
    private readonly ISessionService _sessionService;
    private readonly TimesheetDbContext _dbContext;
    private readonly LineMessagingClient _lineClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LineWebhookController> _logger;
    private readonly string _channelSecret;

    public LineWebhookController(
        ITimesheetAIService timesheetService,
        ILineService lineService,
        ITaskNotificationService taskNotificationService,
        ISessionService sessionService,
        TimesheetDbContext dbContext,
        LineMessagingClient lineClient,
        IConfiguration configuration,
        ILogger<LineWebhookController> logger)
    {
        _timesheetService = timesheetService;
        _lineService = lineService;
        _taskNotificationService = taskNotificationService;
        _sessionService = sessionService;
        _dbContext = dbContext;
        _lineClient = lineClient;
        _configuration = configuration;
        _logger = logger;
        _channelSecret = configuration["Line:ChannelSecret"] ?? throw new InvalidOperationException("LINE Channel Secret not configured");
    }

    [HttpPost]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            _logger.LogInformation("📩 Received LINE webhook request");

            // เปิดให้อ่าน Request.Body ซ้ำได้
            Request.EnableBuffering();

            // อ่าน request body
            string requestBody;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0; // รีเซ็ต position สำหรับการอ่านครั้งต่อไป
            }

            _logger.LogInformation("📝 Request body length: {Length}", requestBody.Length);
            _logger.LogDebug("📄 Request body: {Body}", requestBody);

            // ตรวจสอบว่า request body ไม่ว่าง
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("⚠️ Empty request body");
                return BadRequest("Empty request body");
            }

            // ตรวจสอบ signature
            var signature = Request.Headers["X-Line-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("⚠️ Missing X-Line-Signature header");
                return BadRequest("Missing signature");
            }

            if (!VerifySignature(requestBody, signature))
            {
                _logger.LogWarning("❌ Invalid LINE signature");
                return Unauthorized("Invalid signature");
            }

            _logger.LogInformation("✅ Signature verified");

            // Parse webhook events
            List<WebhookEvent> events = new List<WebhookEvent>();
            try
            {
                // LINE webhook JSON มี structure: {"destination":"...", "events": [...]}
                var webhookData = JObject.Parse(requestBody);
                var eventsArray = webhookData["events"] as JArray;

                if (eventsArray == null || eventsArray.Count == 0)
                {
                    _logger.LogWarning("⚠️ No events found in webhook JSON");
                    return Ok();
                }

                _logger.LogInformation("📦 Found {Count} events in webhook", eventsArray.Count);

                // Parse แต่ละ event ใน array
                foreach (var eventToken in eventsArray)
                {
                    try
                    {
                        var eventType = eventToken["type"]?.ToString();
                        _logger.LogDebug("📄 Parsing event type: {EventType}", eventType);

                        WebhookEvent? webhookEvent = null;

                        // สำหรับ MessageEvent ต้อง manual parse เพราะ Message property ซับซ้อน
                        if (eventType == "message")
                        {
                            var messageType = eventToken["message"]?["type"]?.ToString();
                            var messageId = eventToken["message"]?["id"]?.ToString() ?? "";
                            var messageText = eventToken["message"]?["text"]?.ToString() ?? "";

                            _logger.LogDebug("📝 Message type: {MessageType}, text: {Text}", messageType, messageText);

                            // สร้าง TextEventMessage ด้วย constructor
                            if (messageType == "text" && !string.IsNullOrEmpty(messageText))
                            {
                                var textMessage = new TextEventMessage(messageId, messageText);

                                // ใช้ reflection เพื่อสร้าง MessageEvent (เพราะ constructor มี required parameters)
                                var source = eventToken["source"]?.ToObject<WebhookEventSource>();
                                var timestamp = eventToken["timestamp"]?.ToObject<long>() ?? 0;
                                var replyToken = eventToken["replyToken"]?.ToString() ?? "";

                                if (source != null)
                                {
                                    webhookEvent = new MessageEvent(source, timestamp, textMessage, replyToken);
                                    _logger.LogDebug("✅ Created MessageEvent with text: {Text}", messageText);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("⚠️ Non-text message type: {MessageType}, skipping", messageType);
                            }
                        }
                        else
                        {
                            // สำหรับ event type อื่นๆ ใช้ ToObject ได้เลย
                            webhookEvent = eventType switch
                            {
                                "follow" => eventToken.ToObject<FollowEvent>(),
                                "unfollow" => eventToken.ToObject<UnfollowEvent>(),
                                "join" => eventToken.ToObject<JoinEvent>(),
                                "postback" => eventToken.ToObject<PostbackEvent>(),
                                "leave" => eventToken.ToObject<LeaveEvent>(),
                                "memberJoined" => eventToken.ToObject<MemberJoinEvent>(),
                                "memberLeft" => eventToken.ToObject<MemberLeaveEvent>(),
                                "accountLink" => eventToken.ToObject<AccountLinkEvent>(),
                                "beacon" => eventToken.ToObject<BeaconEvent>(),
                                _ => null
                            };
                        }

                        if (webhookEvent != null)
                        {
                            events.Add(webhookEvent);
                            _logger.LogDebug("✅ Successfully parsed event type: {EventType}", webhookEvent.GetType().Name);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Unknown event type or failed to parse: {EventType}", eventType);
                        }
                    }
                    catch (Exception eventEx)
                    {
                        _logger.LogWarning(eventEx, "⚠️ Failed to parse individual event, skipping...");
                        continue;
                    }
                }

                _logger.LogInformation("📦 Successfully parsed {Count} events", events.Count);
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "❌ Failed to parse webhook events. Request body: {Body}", requestBody);
                return Ok(); // Return 200 to prevent LINE from retrying
            }

            foreach (var ev in events)
            {
                try
                {
                    _logger.LogInformation("🔄 Processing event: {EventType}", ev.GetType().Name);

                    switch (ev)
                    {
                        case MessageEvent messageEvent:
                            await HandleMessageEvent(messageEvent);
                            break;

                        case FollowEvent followEvent:
                            await HandleFollowEvent(followEvent);
                            break;

                        case UnfollowEvent unfollowEvent:
                            await HandleUnfollowEvent(unfollowEvent);
                            break;

                        case JoinEvent joinEvent:
                            await HandleJoinEvent(joinEvent);
                            break;

                        case PostbackEvent postbackEvent:
                            await HandlePostbackEvent(postbackEvent);
                            break;

                        default:
                            _logger.LogInformation("ℹ️ Unhandled event type: {EventType}", ev.GetType().Name);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing event: {EventType}", ev.GetType().Name);
                    // Continue processing other events
                }
            }

            _logger.LogInformation("✅ Webhook processed successfully");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Fatal error processing LINE webhook");
            // Return 200 anyway to prevent LINE from retrying
            return Ok();
        }
    }

    private async Task HandleMessageEvent(MessageEvent messageEvent)
    {
        if (messageEvent.Message is not TextEventMessage textMessage)
        {
            return;
        }

        var userId = messageEvent.Source.UserId;
        var messageText = textMessage.Text;
        var replyToken = messageEvent.ReplyToken;

        _logger.LogInformation("Received message from LINE user {UserId}: {Message}", userId, messageText);

        try
        {
            // ตรวจสอบ command พิเศษ (ไม่ต้อง login)
            var lowerMessage = messageText.ToLower().Trim();

            // 🆔 คำสั่งดู LINE User ID (สำหรับ Developer/Admin)
            if (lowerMessage == "id" || lowerMessage == "/id")
            {
                _logger.LogInformation("📋 Processing 'id' command for user {UserId}", userId);

                var idMessage = "🆔 LINE User ID\n\n";
                idMessage += $"{userId}\n\n";
                idMessage += "📝 วิธีใช้:\n";
                idMessage += "Copy ID นี้ไปใส่ในตาราง Users\n";
                idMessage += "ช่อง line_user_id\n\n";
                idMessage += "💡 ตัวอย่าง SQL:\n";
                idMessage += "UPDATE sec.t_com_user\n";
                idMessage += $"SET line_user_id = '{userId}'\n";
                idMessage += "WHERE user_id = 'YOUR_USER_ID';";

                _logger.LogInformation("📤 Sending reply to user {UserId}", userId);

                try
                {
                    // สร้าง Quick Reply พร้อมปุ่ม Default
                    var quickReply = LineMessageHelper.CreateTextWithQuickReply(idMessage);
                    await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { quickReply });
                    _logger.LogInformation("✅ Sent LINE User ID with Quick Reply to user {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send LINE User ID to user {UserId}", userId);
                    throw;
                }

                return;
            }

            // ค้นหา user จาก LINE user ID
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.LineUserId == userId);

            if (user == null)
            {
                _logger.LogWarning("⚠️ User not found for LINE User ID: {UserId}", userId);

                // แสดงข้อความแนะนำพร้อม ID
                var notFoundMessage = "ขออภัยครับ ไม่พบข้อมูลผู้ใช้ของคุณในระบบ\n\n";
                notFoundMessage += "🆔 LINE User ID ของคุณ:\n";
                notFoundMessage += $"{userId}\n\n";
                notFoundMessage += "📝 วิธีแก้ไข:\n";
                notFoundMessage += "1. Copy ID ด้านบน\n";
                notFoundMessage += "2. ส่งให้ผู้ดูแลระบบ\n";
                notFoundMessage += "3. ให้ผู้ดูแลเพิ่ม ID นี้ในระบบ\n\n";
                notFoundMessage += "💡 หรือ:\n";
                notFoundMessage += "พิมพ์ 'id' เพื่อดู ID อีกครั้ง";

                try
                {
                    // สร้าง Quick Reply พร้อมปุ่ม Default
                    var quickReply = LineMessageHelper.CreateTextWithQuickReply(notFoundMessage);
                    await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { quickReply });
                    _logger.LogInformation("✅ Sent 'user not found' message with Quick Reply to {UserId}", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send 'user not found' message to {UserId}", userId);
                    throw;
                }

                return;
            }

            // ตรวจสอบว่าเป็นการเลือกงานหรือไม่
            if (messageText.StartsWith("SELECT_TASK:"))
            {
                await HandleTaskSelection(user.UserId, userId, messageText, replyToken);
                return;
            }

            // ✅ ตรวจสอบว่าเป็นการยืนยันบันทึกหรือไม่
            if (messageText.StartsWith("CONFIRM_SAVE:"))
            {
                await HandleConfirmSave(user.UserId, userId, messageText, replyToken);
                return;
            }

            // ✅ ตรวจสอบว่าเป็นการยกเลิกบันทึกหรือไม่
            if (messageText == "CANCEL_SAVE")
            {
                _sessionService.ClearPendingTimesheetEntry(userId);
                _sessionService.ClearSelectedTask(userId);
                var cancelMsg = LineMessageHelper.CreateTextWithQuickReply("❌ ยกเลิกการบันทึก Timesheet แล้วครับ");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { cancelMsg });
                return;
            }

            // ✅ ตรวจสอบว่าเป็นการเลือก Issue Type หรือไม่
            if (messageText.StartsWith("ISSUE_TYPE:"))
            {
                await HandleIssueTypeSelection(user.UserId, userId, messageText, replyToken);
                return;
            }

            // คำสั่งยกเลิกการเลือกงาน
            if (lowerMessage == "cancel" || lowerMessage == "ยกเลิก")
            {
                _sessionService.ClearSelectedTask(userId);
                var msg = LineMessageHelper.CreateTextWithQuickReply("ยกเลิกการเลือกงานแล้วครับ");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msg });
                return;
            }

            if (lowerMessage == "งานของฉัน" || lowerMessage == "my tasks" || lowerMessage == "งาน")
            {
                await HandleMyTasksCommand(user.UserId, userId, replyToken);
                return;
            }

            if (lowerMessage == "help" || lowerMessage == "ช่วยเหลือ")
            {
                await HandleHelpCommand(replyToken);
                return;
            }

            if (lowerMessage == "สรุปงานสัปดาห์นี้" || lowerMessage == "สรุปงาน")
            {
                await HandleWeeklySummary(user.UserId, replyToken);
                return;
            }

            if (lowerMessage == "งานวันนี้")
            {
                await HandleTodayTasks(user.UserId, replyToken);
                return;
            }

            // ประมวลผลข้อความด้วย AI (บันทึก timesheet)
            _logger.LogInformation("🤖 Processing message with AI: {Message}", messageText);
            var result = await _timesheetService.ProcessTimesheetMessageAsync(messageText);

            if (!result.Success)
            {
                _logger.LogWarning("⚠️ AI processing failed: {Message}", result.Message);
                await _lineService.ReplyMessageAsync(replyToken, result.Message ?? "เกิดข้อผิดพลาดในการประมวลผล");
                return;
            }

            _logger.LogInformation("✅ AI processed: Detail={Detail}, Hours={Hours}, IssueType={IssueType}, IsComplete={IsComplete}",
                result.Data?.Detail, result.Data?.Hours, result.Data?.IssueType, result.Data?.IsComplete);

            // ตรวจสอบว่าข้อมูลครบถ้วนหรือไม่
            if (result.Data?.IsComplete == true)
            {
                // Validate ข้อมูลครบถ้วนอีกครั้ง
                if (string.IsNullOrWhiteSpace(result.Data.Detail) ||
                    result.Data.Hours <= 0 ||
                    string.IsNullOrWhiteSpace(result.Data.IssueType))
                {
                    _logger.LogWarning("Incomplete data detected: Detail={Detail}, Hours={Hours}, IssueType={IssueType}",
                        result.Data.Detail, result.Data.Hours, result.Data.IssueType);

                    // ส่งข้อความตอบกลับพร้อม Quick Reply
                    var errorMessage = "ขออภัยครับ ข้อมูลไม่ครบถ้วน กรุณาลองใหม่อีกครั้ง";
                    var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(errorMessage);
                    await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
                    return;
                }

                // ดึงรายการงานที่ค้าง
                var pendingTasks = await _taskNotificationService.GetPendingTaskItemsAsync(user.UserId);

                if (pendingTasks.Count == 0)
                {
                    var noTaskMessage = "❌ ไม่พบงานที่ค้างครับ\n\nไม่สามารถบันทึก Timesheet ได้\n\n💡 กรุณาติดต่อผู้ดูแลระบบเพื่อเพิ่มงาน";
                    var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(noTaskMessage);
                    await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
                    return;
                }

                // ตรวจสอบว่ามีการเลือกงานไว้ไหม
                var (selectedTaskId, selectedTaskName) = _sessionService.GetSelectedTask(userId);
                _logger.LogInformation("📋 Session check: LineUserId={LineUserId}, SelectedTaskId={TaskId}, TaskName={TaskName}",
                    userId, selectedTaskId, selectedTaskName);

                if (selectedTaskId.HasValue)
                {
                    // ✅ ตรวจสอบว่า AI ส่ง issue_type เป็น "Other" หรือไม่
                    if (result.Data.IssueType == "Other")
                    {
                        _logger.LogInformation("⚠️ AI returned 'Other' issue type, prompting user to select");

                        // เก็บ pending entry และให้เลือก issue type
                        _sessionService.SetPendingTimesheetEntry(userId, result.Data);

                        var issueTypes = await _taskNotificationService.GetIssueTypesAsync();
                        var quickReply = LineMessageHelper.GetIssueTypeQuickReply(issueTypes);

                        var promptMessage = $"📝 รับทราบข้อมูลแล้วครับ:\n\n";
                        promptMessage += $"📋 งาน: {selectedTaskName}\n";
                        promptMessage += $"💼 {result.Data.Detail}\n";
                        promptMessage += $"⏱️ {result.Data.Hours} ชั่วโมง\n";
                        promptMessage += $"📅 {(result.Data.Date.HasValue ? result.Data.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}\n\n";
                        promptMessage += "🏷️ **กรุณาเลือกประเภทงาน:**\n";
                        promptMessage += "👇 กดเลือกจากปุ่มด้านล่าง";

                        var msgWithSelection = new TextMessage(promptMessage, quickReply);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msgWithSelection });

                        _logger.LogInformation("✅ Sent issue type selection to user {UserId}", user.UserId);
                        return;
                    }

                    // มีการเลือกงานไว้ - บันทึกทันที
                    _logger.LogInformation("💾 Attempting to save tracking: UserId={UserId}, LineUserId={LineUserId}, TaskId={TaskId}",
                        user.UserId, userId, selectedTaskId.Value);

                    var saveSuccess = await _taskNotificationService.SaveTaskTrackingAsync(
                        user.UserId,
                        userId,
                        result.Data,
                        selectedTaskId.Value
                    );

                    _logger.LogInformation("💾 Save result: {Success}", saveSuccess);

                    if (saveSuccess)
                    {
                        // ลบ session หลังบันทึกเสร็จ
                        _sessionService.ClearSelectedTask(userId);

                        var successMsg = $"✅ บันทึก Timesheet สำเร็จ!\n\n";
                        successMsg += $"📋 งาน: {selectedTaskName}\n";
                        successMsg += $"📝 {result.Data.Detail}\n";
                        successMsg += $"⏱️ {result.Data.Hours} ชั่วโมง\n";
                        successMsg += $"🏷️ {result.Data.IssueType}\n";
                        successMsg += $"📅 {(result.Data.Date.HasValue ? result.Data.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}";

                        // ✅ แสดงปุ่ม Quick Reply เมนูปกติหลังบันทึกสำเร็จ
                        var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(successMsg);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });

                        _logger.LogInformation(
                            "✅ Timesheet saved: User={UserId}, Task={TaskId}, Hours={Hours}, IssueType={IssueType}",
                            user.UserId, selectedTaskId.Value, result.Data.Hours, result.Data.IssueType
                        );
                    }
                    else
                    {
                        var errorMessage = "❌ เกิดข้อผิดพลาดในการบันทึก กรุณาลองใหม่อีกครั้ง";
                        var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(errorMessage);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
                    }
                }
                else if (pendingTasks.Count == 1)
                {
                    // ✅ ตรวจสอบว่า AI ส่ง issue_type เป็น "Other" หรือไม่
                    if (result.Data.IssueType == "Other")
                    {
                        _logger.LogInformation("⚠️ AI returned 'Other' issue type for single task, prompting user to select");

                        // เก็บ pending entry และงานที่เลือก
                        _sessionService.SetPendingTimesheetEntry(userId, result.Data);
                        _sessionService.SetSelectedTask(userId, pendingTasks[0].TaskId, pendingTasks[0].TaskName);

                        var issueTypes = await _taskNotificationService.GetIssueTypesAsync();
                        var quickReply = LineMessageHelper.GetIssueTypeQuickReply(issueTypes);

                        var promptMessage = $"📝 รับทราบข้อมูลแล้วครับ:\n\n";
                        promptMessage += $"📋 งาน: {pendingTasks[0].TaskName}\n";
                        promptMessage += $"💼 {result.Data.Detail}\n";
                        promptMessage += $"⏱️ {result.Data.Hours} ชั่วโมง\n";
                        promptMessage += $"📅 {(result.Data.Date.HasValue ? result.Data.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}\n\n";
                        promptMessage += "🏷️ **กรุณาเลือกประเภทงาน:**\n";
                        promptMessage += "👇 กดเลือกจากปุ่มด้านล่าง";

                        var msgWithSelection = new TextMessage(promptMessage, quickReply);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msgWithSelection });

                        _logger.LogInformation("✅ Sent issue type selection to user {UserId}", user.UserId);
                        return;
                    }

                    // มีงานเดียว - บันทึกทันที
                    var saveSuccess = await _taskNotificationService.SaveTaskTrackingAsync(
                        user.UserId,
                        userId,
                        result.Data,
                        pendingTasks[0].TaskId
                    );

                    if (saveSuccess)
                    {
                        var successMsg = $"✅ บันทึก Timesheet สำเร็จ!\n\n";
                        successMsg += $"📋 งาน: {pendingTasks[0].TaskName}\n";
                        successMsg += $"📝 {result.Data.Detail}\n";
                        successMsg += $"⏱️ {result.Data.Hours} ชั่วโมง\n";
                        successMsg += $"🏷️ {result.Data.IssueType}\n";
                        successMsg += $"📅 {(result.Data.Date.HasValue ? result.Data.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}";

                        // ✅ แสดงปุ่ม Quick Reply เมนูปกติหลังบันทึกสำเร็จ
                        var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(successMsg);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });

                        _logger.LogInformation(
                            "✅ Timesheet saved (auto): User={UserId}, Task={TaskId}, Hours={Hours}",
                            user.UserId, pendingTasks[0].TaskId, result.Data.Hours
                        );
                    }
                    else
                    {
                        var errorMessage = "❌ เกิดข้อผิดพลาดในการบันทึก กรุณาลองใหม่อีกครั้ง";
                        var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(errorMessage);
                        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
                    }
                }
                else
                {
                    // ✅ มีหลายงาน - เก็บข้อมูล Timesheet แล้วส่งรายการงานให้เลือก (ใช้ Reply Token)
                    _logger.LogInformation("💾 Saving timesheet entry to session: Detail={Detail}, Hours={Hours}",
                        result.Data.Detail, result.Data.Hours);

                    _sessionService.SetPendingTimesheetEntry(userId, result.Data);

                    _logger.LogInformation("📋 User has {Count} pending tasks, preparing task selection...", pendingTasks.Count);

                    // สร้างข้อความยืนยันข้อมูล + รายการงาน
                    var confirmMessage = $"✅ รับทราบข้อมูลแล้วครับ:\n\n";
                    confirmMessage += $"📝 {result.Data.Detail}\n";
                    confirmMessage += $"⏱️ {result.Data.Hours} ชั่วโมง\n";
                    confirmMessage += $"🏷️ {result.Data.IssueType}\n";
                    confirmMessage += $"📅 {(result.Data.Date.HasValue ? result.Data.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}\n\n";
                    confirmMessage += $"📋 คุณมี {pendingTasks.Count} งานที่ค้าง\n";
                    confirmMessage += "🎯 กรุณาเลือกงานที่ต้องการบันทึก Timesheet:\n\n";

                    int i = 1;
                    foreach (var task in pendingTasks.Take(10))
                    {
                        var dueText = task.EndDate.HasValue
                            ? $" (ครบ {task.EndDate.Value:dd/MM})"
                            : "";
                        confirmMessage += $"{i}. {task.TaskName}{dueText}\n";
                        i++;
                    }

                    if (pendingTasks.Count > 10)
                    {
                        confirmMessage += $"\n... และอีก {pendingTasks.Count - 10} งาน\n";
                    }

                    confirmMessage += "\n👇 กดเลือกจากปุ่มด้านล่าง";

                    // ✅ ส่ง Reply พร้อม Quick Reply ของรายการงาน
                    var quickReply = LineMessageHelper.GetTaskSelectionQuickReply(pendingTasks);
                    _logger.LogInformation("📤 Generated Quick Reply with {Count} items for {TaskCount} tasks", 
                        quickReply?.Items?.Count ?? 0, pendingTasks.Count);

                    if (quickReply?.Items != null)
                    {
                        foreach (var item in quickReply.Items)
                        {
                            if (item.Action is MessageTemplateAction action)
                            {
                                _logger.LogDebug("  - Quick Reply Button: {Label} -> {Text}", action.Label, action.Text);
                            }
                        }
                    }

                    var msgWithSelection = new TextMessage(
                        confirmMessage,
                        quickReply
                    );

                    await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msgWithSelection });

                    _logger.LogInformation(
                        "✅ Sent task selection message with {Count} quick reply buttons to user {UserId}",
                        quickReply?.Items?.Count ?? 0, user.UserId
                    );
                }
            }
            else
            {
                // ข้อมูลไม่ครบ - ส่งข้อความตอบกลับพร้อม Quick Reply เมนูปกติ
                var replyMessage = result.BotReply ?? "ขอโทษครับ เกิดข้อผิดพลาด";
                var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(replyMessage);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from LINE user {UserId}", userId);
            await _lineService.ReplyMessageAsync(
                replyToken,
                "ขออภัยครับ เกิดข้อผิดพลาดในการประมวลผล กรุณาลองใหม่อีกครั้ง"
            );
        }
    }

    private async Task HandleMyTasksCommand(string userId, string lineUserId, string replyToken)
    {
        try
        {
            var taskItems = await _taskNotificationService.GetPendingTaskItemsAsync(userId);
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return;

            var userName = $"{user.FirstName} {user.LastName}".Trim();

            // สร้างข้อความแสดงรายการงาน (Text พร้อม Quick Reply)
            var message = LineMessageHelper.CreateTaskListMessage(taskItems, userName);
            var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(message);

            await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle 'my tasks' command");
            await _lineService.ReplyMessageAsync(
                replyToken,
                "ขออภัยครับ ไม่สามารถดึงข้อมูลงานได้ในขณะนี้"
            );
        }
    }

    private async Task HandleHelpCommand(string replyToken)
    {
        // ✅ ดึง issue types จากฐานข้อมูล
        var issueTypes = await _taskNotificationService.GetIssueTypesAsync();
        var issueTypesText = issueTypes.Any() 
            ? string.Join(", ", issueTypes) 
            : "Bug, Develop, Meeting, Training, Support, Request, Issue, Error, Other";

        var helpMessage = "📚 **Nong Time AI - คู่มือการใช้งาน**\n";
        helpMessage += "━━━━━━━━━━━━━━━━━━━━\n\n";
        helpMessage += "📋 **คำสั่งหลัก:**\n";
        helpMessage += "• **งานของฉัน** - ดูรายการงานที่ค้าง\n";
        helpMessage += "• **สรุปงานสัปดาห์นี้** - ดูสรุปงานที่ทำ\n";
        helpMessage += "• **งานวันนี้** - ดูงานที่บันทึกวันนี้\n\n";
        helpMessage += "💬 **วิธีบันทึก Timesheet:**\n";
        helpMessage += "📝 ต้องระบุข้อมูล:\n";
        helpMessage += "   1️⃣ รายละเอียดงาน (required)\n";
        helpMessage += "   2️⃣ จำนวนชั่วโมง (required)\n";
        helpMessage += "   3️⃣ ประเภทงาน (required)\n";
        helpMessage += "   4️⃣ วันที่ (optional - ไม่ระบุ = วันนี้)\n\n";
        helpMessage += "✏️ ตัวอย่าง:\n";
        helpMessage += "• แก้บั๊ก login 2 ชม.\n";
        helpMessage += "• ประชุมทีม 1.5 ชม. เมื่อวาน\n";
        helpMessage += "• พัฒนา API 3 ชม. 13/01\n";
        helpMessage += "• ศึกษา PostgreSQL 8 ชม. วันจันทร์\n\n";
        helpMessage += "🎯 **ขั้นตอนการบันทึก:**\n";
        helpMessage += "1. พิมพ์ข้อความตามตัวอย่าง\n";
        helpMessage += "2. ระบบจะให้เลือกงานที่จะบันทึก\n";
        helpMessage += "3. กดเลือกงานจากปุ่ม\n";
        helpMessage += "4. พิมพ์ข้อความอีกครั้ง (หรือใช้ข้อความเดิม)\n";
        helpMessage += "5. บันทึกสำเร็จ! ✅\n\n";
        helpMessage += "🗓️ **รูปแบบวันที่ที่รองรับ:**\n";
        helpMessage += "• วันนี้ (ไม่ต้องระบุ)\n";
        helpMessage += "• เมื่อวาน\n";
        helpMessage += "• 13/01 หรือ 13/1\n";
        helpMessage += "• 13 ม.ค. หรือ 13 มกราคม\n";
        helpMessage += "• วันจันทร์, วันอังคาร, ฯลฯ\n\n";
        helpMessage += $"🏷️ **ประเภทงานที่รองรับ:**\n{issueTypesText}\n\n";
        helpMessage += "🔧 **คำสั่งพิเศษ (Admin):**\n";
        helpMessage += "• **id** - ดู LINE User ID ของคุณ";

        var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(helpMessage);
        await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
    }

    private async Task HandleWeeklySummary(string userId, string replyToken)
    {
        try
        {
            // ✅ ใช้เวลาท้องถิ่น (เอเชีย/กรุงเทพ GMT+7) เพื่อให้ตรงกับข้อมูลที่บันทึก
            var bangkokTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // GMT+7
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bangkokTimeZone);

            // คำนวณวันจันทร์และศุกร์ตามเวลาท้องถิ่น
            var startDateLocal = nowLocal.Date.AddDays(-(int)nowLocal.DayOfWeek + 1); // จันทร์ (เวลาท้องถิ่น)
            var endDateLocal = startDateLocal.AddDays(4); // ศุกร์ (เวลาท้องถิ่น)
            var endDateInclusiveLocal = endDateLocal.AddDays(1); // เสาร์ (เวลาท้องถิ่น)

            // แปลงกลับเป็น UTC สำหรับ query
            var startDate = TimeZoneInfo.ConvertTimeToUtc(startDateLocal, bangkokTimeZone);
            var endDateInclusive = TimeZoneInfo.ConvertTimeToUtc(endDateInclusiveLocal, bangkokTimeZone);

            // 🔍 Debug logging
            _logger.LogInformation("📅 Weekly Summary Query - UserId: {UserId}", userId);
            _logger.LogInformation("   Now (Local): {NowLocal}", nowLocal);
            _logger.LogInformation("   StartDate (Local): {StartDateLocal} -> UTC: {StartDateUtc}", startDateLocal, startDate);
            _logger.LogInformation("   EndDate (Local): {EndDateLocal} -> UTC: {EndDateUtc}", endDateInclusiveLocal, endDateInclusive);

            var trackings = await _dbContext.ProjectTaskTrackings
                .Where(t => t.Assignee == userId && 
                           t.ActualDate >= startDate && 
                           t.ActualDate < endDateInclusive)
                .OrderBy(t => t.ActualDate)
                .ToListAsync();

            _logger.LogInformation("📊 Found {Count} trackings for weekly summary", trackings.Count);
            if (trackings.Any())
            {
                _logger.LogInformation("   First record ActualDate: {Date}", trackings.First().ActualDate);
                _logger.LogInformation("   Last record ActualDate: {Date}", trackings.Last().ActualDate);
            }

            var totalHours = trackings.Sum(t => t.ActualWork ?? 0);
            var totalTasks = trackings.Count;

            var message = $"📊 **สรุปงานสัปดาห์นี้**\n";
            message += $"({startDateLocal:dd/MM/yyyy} - {endDateLocal:dd/MM/yyyy})\n\n";
            message += $"✅ ทำงานไป: **{totalTasks} งาน**\n";
            message += $"⏱️ รวมเวลา: **{totalHours:F1} ชั่วโมง**\n\n";

            if (totalTasks > 0)
            {
                var groupedByIssueType = trackings
                    .GroupBy(t => t.IssueType ?? "Other")
                    .Select(g => new { IssueType = g.Key, Count = g.Count(), Hours = g.Sum(t => t.ActualWork ?? 0) })
                    .OrderByDescending(g => g.Hours);

                message += "📈 **แยกตาม Issue Type:**\n";
                foreach (var group in groupedByIssueType)
                {
                    message += $"• {group.IssueType}: {group.Count} งาน ({group.Hours:F1} ชม.)\n";
                }
            }

            var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(message);
            await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate weekly summary");
            await _lineService.ReplyMessageAsync(replyToken, "ขออภัยครับ ไม่สามารถสร้างรายงานได้");
        }
    }

    private async Task HandleTodayTasks(string userId, string replyToken)
    {
        try
        {
            // ✅ ใช้เวลาท้องถิ่น (เอเชีย/กรุงเทพ GMT+7)
            var bangkokTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // GMT+7
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, bangkokTimeZone);
            var todayLocal = nowLocal.Date; // วันนี้ 00:00:00 (เวลาท้องถิ่น)
            var tomorrowLocal = todayLocal.AddDays(1); // พรุ่งนี้ 00:00:00 (เวลาท้องถิ่น)

            // แปลงเป็น UTC สำหรับ query
            var today = TimeZoneInfo.ConvertTimeToUtc(todayLocal, bangkokTimeZone);
            var tomorrow = TimeZoneInfo.ConvertTimeToUtc(tomorrowLocal, bangkokTimeZone);

            // 🔍 Debug logging
            _logger.LogInformation("📅 Today Tasks Query - UserId: {UserId}", userId);
            _logger.LogInformation("   Today (Local): {TodayLocal} -> UTC: {TodayUtc}", todayLocal, today);
            _logger.LogInformation("   Tomorrow (Local): {TomorrowLocal} -> UTC: {TomorrowUtc}", tomorrowLocal, tomorrow);

            var trackings = await _dbContext.ProjectTaskTrackings
                .Where(t => t.Assignee == userId && 
                           t.ActualDate >= today && 
                           t.ActualDate < tomorrow)
                .OrderBy(t => t.CreateDate)
                .ToListAsync();

            _logger.LogInformation("📊 Found {Count} trackings for today", trackings.Count);
            if (trackings.Any())
            {
                foreach (var t in trackings)
                {
                    _logger.LogInformation("   - ActualDate: {Date}, Detail: {Detail}", t.ActualDate, t.ProcessUpdate);
                }
            }

            var totalHours = trackings.Sum(t => t.ActualWork ?? 0);

            var message = $"✅ **งานที่บันทึกวันนี้**\n\n";
            message += $"รวม: **{trackings.Count} งาน** ({totalHours:F1} ชม.)\n\n";

            if (trackings.Any())
            {
                foreach (var tracking in trackings)
                {
                    message += $"• {tracking.ProcessUpdate}\n";
                    message += $"  🏷️ {tracking.IssueType} | ⏱️ {tracking.ActualWork:F1} ชม.\n\n";
                }
            }
            else
            {
                message += "ยังไม่มีงานที่บันทึกวันนี้ครับ\n\n";
            }

            var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(message);
            await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get today tasks");
            await _lineService.ReplyMessageAsync(replyToken, "ขออภัยครับ ไม่สามารถดึงข้อมูลได้");
        }
    }

    private async Task HandleTaskSelection(string userId, string lineUserId, string messageText, string replyToken)
    {
        try
        {
            // Parse: "SELECT_TASK:434"
            var parts = messageText.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var taskId))
            {
                await _lineService.ReplyMessageAsync(replyToken, "รูปแบบข้อมูลไม่ถูกต้อง");
                return;
            }

            // ดึงข้อมูล User
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.UserId == userId || u.LineUserId == lineUserId);

            if (user == null)
            {
                await _lineService.ReplyMessageAsync(replyToken, "ไม่พบข้อมูลผู้ใช้");
                return;
            }

            // ดึงข้อมูลงาน
            var task = await _dbContext.ProjectTasks
                .FirstOrDefaultAsync(t => t.ProjectTaskId == taskId);

            if (task == null)
            {
                await _lineService.ReplyMessageAsync(replyToken, "ไม่พบงานที่เลือก");
                return;
            }

            // ตรวจสอบว่ามี Pending Timesheet Entry หรือไม่
            var pendingEntry = _sessionService.GetPendingTimesheetEntry(lineUserId);

            if (pendingEntry != null && pendingEntry.IsComplete)
            {
                // ✅ มีข้อมูล Timesheet ที่รอบันทึก - แสดง Issue Type ทั้งหมดพร้อมปุ่มบันทึก
                _logger.LogInformation(
                    "📝 Found pending entry, showing all issue types: User={UserId}, Task={TaskId}, Detail={Detail}, IssueType={IssueType}",
                    user.UserId, taskId, pendingEntry.Detail, pendingEntry.IssueType
                );

                // เก็บ Task ที่เลือกไว้
                _sessionService.SetSelectedTask(lineUserId, taskId, task.TaskName ?? "");

                // ดึง Issue Types ทั้งหมด
                var issueTypes = await _taskNotificationService.GetIssueTypesAsync();

                // สร้าง Quick Reply พร้อม Issue Type ทั้งหมด
                var quickReply = LineMessageHelper.GetConfirmTimesheetQuickReply(
                    pendingEntry.IssueType ?? "Other",
                    issueTypes
                );

                var promptMessage = $"📝 **รับทราบข้อมูลแล้วครับ**\n";
                promptMessage += "━━━━━━━━━━━━━━━━━━━━\n\n";
                promptMessage += $"📋 **งาน:** {task.TaskName}\n";
                promptMessage += $"💼 **รายละเอียด:** {pendingEntry.Detail}\n";
                promptMessage += $"⏱️ **ชั่วโมง:** {pendingEntry.Hours} ชม.\n";
                promptMessage += $"🏷️ **ประเภทที่แนะนำ:** {pendingEntry.IssueType ?? "Other"}\n";
                promptMessage += $"📅 **วันที่:** {(pendingEntry.Date.HasValue ? pendingEntry.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}\n\n";
                promptMessage += "━━━━━━━━━━━━━━━━━━━━\n\n";
                promptMessage += "🎯 **กรุณาเลือกประเภทงาน:**\n";
                promptMessage += $"✅ กดปุ่ม \"✅ บันทึก\" เพื่อบันทึกด้วยประเภทที่แนะนำ\n";
                promptMessage += "🔄 หรือเลือกประเภทอื่นจากปุ่มด้านล่าง";

                var msgWithQuickReply = new TextMessage(promptMessage, quickReply);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msgWithQuickReply });

                _logger.LogInformation("✅ Sent confirmation with all issue types to user {UserId}", user.UserId);
            }
            else
            {
                // ❌ ไม่มีข้อมูล Timesheet ที่รอบันทึก - บันทึก session และแจ้งให้พิมพ์ข้อความ
                _sessionService.SetSelectedTask(lineUserId, taskId, task.TaskName ?? "");

                var message = $"✅ **เลือกงาน: {task.TaskName}**\n";
                message += "━━━━━━━━━━━━━━━━━━━━\n\n";
                message += "📝 พร้อมบันทึก Timesheet แล้วครับ\n\n";
                message += "💬 ส่งข้อความตามรูปแบบ:\n";
                message += "[รายละเอียด] + [ชั่วโมง] + [ประเภท] + [วันที่]\n\n";
                message += "✏️ ตัวอย่าง:\n";
                message += "• \"แก้บั๊ก login 2 ชม.\" (วันนี้)\n";
                message += "• \"ประชุมทีม 1.5 ชม. เมื่อวาน\"\n";
                message += "• \"ศึกษา PostgreSQL 8 ชม. 13/01\"\n";
                message += "• \"พัฒนา API 5 ชม. วันจันทร์\"\n\n";
                message += "🏷️ ประเภทงาน: Bug, Develop, Meeting,\nTraining, Support, Request, Other\n\n";
                message += "🗓️ วันที่: ไม่ระบุ = วันนี้ | รองรับย้อนหลัง";

                var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(message);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });

                _logger.LogInformation(
                    "User {UserId} selected task {TaskId} ({TaskName}), waiting for timesheet entry",
                    user.UserId,
                    taskId,
                    task.TaskName
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle task selection");
            await _lineService.ReplyMessageAsync(replyToken, "เกิดข้อผิดพลาดในการเลือกงาน");
        }
    }

    private async Task HandleFollowEvent(FollowEvent followEvent)
    {
        var userId = followEvent.Source.UserId;
        _logger.LogInformation("User followed: {UserId}", userId);

        var welcomeMessage = "🎉 สวัสดีครับ! ยินดีต้อนรับสู่\n" +
                           "**Nong Time AI** 🤖\n\n" +
                           "ผมจะช่วยคุณบันทึก Timesheet ได้ง่ายๆ\n\n" +
                           "📝 **วิธีใช้งาน:**\n" +
                           "ส่งข้อความบอก: งาน + ชั่วโมง + ประเภท (+ วันที่)\n\n" +
                           "✏️ **ตัวอย่าง:**\n" +
                           "• แก้บั๊ก login 2 ชม.\n" +
                           "• ประชุมทีม 1.5 ชม. เมื่อวาน\n" +
                           "• พัฒนา API 3 ชม. 13/01\n" +
                           "• ศึกษา AI 8 ชม. วันจันทร์\n\n" +
                           "🎯 **ขั้นตอน:**\n" +
                           "1. ส่งข้อความตามตัวอย่าง\n" +
                           "2. เลือกงานที่จะบันทึก\n" +
                           "3. เสร็จสิ้น! ✅\n\n" +
                           "🗓️ **วันที่:** ไม่ระบุ = วันนี้, รองรับย้อนหลัง\n\n" +
                           "พิมพ์ **help** เพื่อดูคำสั่งทั้งหมด";

        await _lineService.PushMessageAsync(userId, welcomeMessage);
    }

    private Task HandleUnfollowEvent(UnfollowEvent unfollowEvent)
    {
        var userId = unfollowEvent.Source.UserId;
        _logger.LogInformation("User unfollowed: {UserId}", userId);
        return Task.CompletedTask;
    }

    private async Task HandleJoinEvent(JoinEvent joinEvent)
    {
        _logger.LogInformation("Bot joined group/room");

        var replyToken = joinEvent.ReplyToken;
        var greetingMessage = "สวัสดีครับ! ขอบคุณที่เชิญผมเข้ากลุ่ม 😊\n" +
                            "ผมจะช่วยทุกคนบันทึก timesheet ได้นะครับ";

        await _lineService.ReplyMessageAsync(replyToken, greetingMessage);
    }

    private async Task HandlePostbackEvent(PostbackEvent postbackEvent)
    {
        var userId = postbackEvent.Source.UserId;
        var data = postbackEvent.Postback.Data;

        _logger.LogInformation("Received postback from {UserId}: {Data}", userId, data);

        // จัดการ postback data ตามต้องการ
        // เช่น การกดปุ่มใน Rich Menu หรือ Template Message
    }

    private async Task HandleIssueTypeSelection(string userId, string lineUserId, string messageText, string replyToken)
    {
        try
        {
            // Parse: "ISSUE_TYPE:Bug"
            var parts = messageText.Split(':');
            if (parts.Length != 2)
            {
                await _lineService.ReplyMessageAsync(replyToken, "รูปแบบข้อมูลไม่ถูกต้อง");
                return;
            }

            var selectedIssueType = parts[1].Trim();
            _logger.LogInformation("📋 User {LineUserId} selected issue type: {IssueType}", lineUserId, selectedIssueType);

            // ดึงข้อมูล pending entry และ selected task
            var pendingEntry = _sessionService.GetPendingTimesheetEntry(lineUserId);
            var (selectedTaskId, selectedTaskName) = _sessionService.GetSelectedTask(lineUserId);

            if (pendingEntry == null)
            {
                var msg = LineMessageHelper.CreateTextWithQuickReply("❌ ไม่พบข้อมูลที่รอบันทึก กรุณาส่งข้อความใหม่อีกครั้ง");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msg });
                return;
            }

            if (!selectedTaskId.HasValue)
            {
                var msg = LineMessageHelper.CreateTextWithQuickReply("❌ ไม่พบงานที่เลือก กรุณาเลือกงานใหม่อีกครั้ง");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { msg });
                return;
            }

            // อัปเดต issue_type ให้กับ pending entry
            pendingEntry.IssueType = selectedIssueType;
            pendingEntry.IsComplete = true; // ตอนนี้ครบแล้ว

            _logger.LogInformation("✅ Updated issue type: {IssueType} for pending entry", selectedIssueType);

            // บันทึก Timesheet
            var saveSuccess = await _taskNotificationService.SaveTaskTrackingAsync(
                userId,
                lineUserId,
                pendingEntry,
                selectedTaskId.Value
            );

            // ลบ session
            _sessionService.ClearPendingTimesheetEntry(lineUserId);
            _sessionService.ClearSelectedTask(lineUserId);

            if (saveSuccess)
            {
                var successMsg = $"✅ บันทึก Timesheet สำเร็จ!\n\n";
                successMsg += $"📋 งาน: {selectedTaskName}\n";
                successMsg += $"📝 {pendingEntry.Detail}\n";
                successMsg += $"⏱️ {pendingEntry.Hours} ชั่วโมง\n";
                successMsg += $"🏷️ {pendingEntry.IssueType}\n";
                successMsg += $"📅 {(pendingEntry.Date.HasValue ? pendingEntry.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}";

                var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(successMsg);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });

                _logger.LogInformation(
                    "✅ Timesheet saved with user-selected issue type: User={UserId}, Task={TaskId}, IssueType={IssueType}",
                    userId, selectedTaskId.Value, selectedIssueType
                );
            }
            else
            {
                var errorMessage = "❌ เกิดข้อผิดพลาดในการบันทึก กรุณาลองใหม่อีกครั้ง";
                var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(errorMessage);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle issue type selection");
            await _lineService.ReplyMessageAsync(replyToken, "เกิดข้อผิดพลาดในการเลือกประเภทงาน");
        }
    }

    private async Task HandleConfirmSave(string userId, string lineUserId, string messageText, string replyToken)
    {
        try
        {
            // Parse: "CONFIRM_SAVE:Bug"
            var parts = messageText.Split(':');
            if (parts.Length != 2)
            {
                await _lineService.ReplyMessageAsync(replyToken, "รูปแบบข้อมูลไม่ถูกต้อง");
                return;
            }

            var issueType = parts[1];

            _logger.LogInformation(
                "✅ Confirming save with issue type: User={UserId}, IssueType={IssueType}",
                userId, issueType
            );

            // ดึงข้อมูลจาก Session
            var (selectedTaskId, selectedTaskName) = _sessionService.GetSelectedTask(lineUserId);
            var pendingEntry = _sessionService.GetPendingTimesheetEntry(lineUserId);

            if (!selectedTaskId.HasValue || pendingEntry == null)
            {
                _logger.LogWarning("No task or pending entry found for user {UserId}", userId);
                var errorMsg = LineMessageHelper.CreateTextWithQuickReply("❌ ไม่พบข้อมูลการบันทึก กรุณาลองใหม่อีกครั้ง");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { errorMsg });
                return;
            }

            // อัปเดต IssueType
            pendingEntry.IssueType = issueType;
            pendingEntry.IsComplete = true;

            // บันทึก Timesheet
            var saveSuccess = await _taskNotificationService.SaveTaskTrackingAsync(
                userId,
                lineUserId,
                pendingEntry,
                selectedTaskId.Value
            );

            // ลบ session
            _sessionService.ClearPendingTimesheetEntry(lineUserId);
            _sessionService.ClearSelectedTask(lineUserId);

            if (saveSuccess)
            {
                var successMsg = $"✅ **บันทึก Timesheet สำเร็จ!**\n";
                successMsg += "━━━━━━━━━━━━━━━━━━━━\n\n";
                successMsg += $"📋 **งาน:** {selectedTaskName}\n";
                successMsg += $"📝 **รายละเอียด:** {pendingEntry.Detail}\n";
                successMsg += $"⏱️ **ชั่วโมง:** {pendingEntry.Hours} ชม.\n";
                successMsg += $"🏷️ **ประเภท:** {pendingEntry.IssueType}\n";
                successMsg += $"📅 **วันที่:** {(pendingEntry.Date.HasValue ? pendingEntry.Date.Value.ToString("dd/MM/yyyy") : "วันนี้")}\n\n";
                successMsg += "━━━━━━━━━━━━━━━━━━━━\n";
                successMsg += "✨ บันทึกเรียบร้อยแล้ว";

                var textWithQuickReply = LineMessageHelper.CreateTextWithQuickReply(successMsg);
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { textWithQuickReply });

                _logger.LogInformation(
                    "✅ Timesheet saved successfully: User={UserId}, Task={TaskId}, IssueType={IssueType}",
                    userId, selectedTaskId.Value, issueType
                );
            }
            else
            {
                var errorMsg = LineMessageHelper.CreateTextWithQuickReply("❌ เกิดข้อผิดพลาดในการบันทึก กรุณาลองใหม่อีกครั้ง");
                await _lineClient.ReplyMessageAsync(replyToken, new List<ISendMessage> { errorMsg });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle confirm save");
            await _lineService.ReplyMessageAsync(replyToken, "เกิดข้อผิดพลาดในการบันทึก");
        }
    }

    private bool VerifySignature(string requestBody, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var key = Encoding.UTF8.GetBytes(_channelSecret);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
        var computedSignature = Convert.ToBase64String(hash);

        return signature == computedSignature;
    }
}
