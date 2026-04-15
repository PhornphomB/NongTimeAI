# NongTimeAI - Timesheet AI Bot
# NongTimeAI - Timesheet AI Bot

ระบบ Timesheet Bot ที่ใช้ AI (Llama) ในการสกัดข้อมูลจากข้อความภาษาไทยและแปลงเป็นข้อมูล Timesheet

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED)](docker-compose.yml)

## 📚 เอกสารทั้งหมด

### 🚀 Quick Start
- **[QUICK_SETUP.md](QUICK_SETUP.md)** ⭐ เริ่มต้น LINE Bot + PostgreSQL ใน 15 นาที
- **[LINE_BOT_SETUP.md](LINE_BOT_SETUP.md)** ⭐⭐⭐ ตั้งค่า LINE Bot ทีละขั้นตอน (มีภาพ)
- **[LINE_BOT_ARCHITECTURE.md](LINE_BOT_ARCHITECTURE.md)** - Architecture & Flow
- **[LINE_USER_ID_COMMAND.md](LINE_USER_ID_COMMAND.md)** ⭐ วิธีดู LINE User ID (command "id")
- **[WEBHOOK_500_TROUBLESHOOTING.md](WEBHOOK_500_TROUBLESHOOTING.md)** - แก้ปัญหา Webhook 500 Error
- **[QUICKSTART.md](QUICKSTART.md)** - เริ่มต้นใช้งาน API ใน 5 นาที

### 📖 Setup Guides
- **[SETUP_GUIDE.md](SETUP_GUIDE.md)** - คู่มือการเชื่อมต่อ LINE & PostgreSQL แบบละเอียด
- **[TASK_REMINDER_GUIDE.md](TASK_REMINDER_GUIDE.md)** ⭐ คู่มือระบบแจ้งเตือนงานอัตโนมัติ
- **[LINE_POSTGRESQL_SUMMARY.md](LINE_POSTGRESQL_SUMMARY.md)** - สรุปการเชื่อมต่อทั้งหมด
- **[LINE_INTEGRATION.md](LINE_INTEGRATION.md)** - การเชื่อมต่อกับ LINE Bot (code examples)

### 🐳 Deployment
- **[DOCKER.md](DOCKER.md)** - วิธีการรันด้วย Docker
- **[PERFORMANCE.md](PERFORMANCE.md)** - Performance optimization guide

### 📋 Project Info
- **[PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)** - สรุปโปรเจกต์ทั้งหมด
- **[MISSING_FEATURES.md](MISSING_FEATURES.md)** - Roadmap และ features ที่ยังขาด
- **[CHANGELOG.md](CHANGELOG.md)** - Version history
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Contribution guidelines

### 🎨 API Documentation
- **[SCALAR_QUICK_START.md](SCALAR_QUICK_START.md)** ⭐ Scalar UI คู่มือใช้งาน
- **[SCALAR_MIGRATION_SUMMARY.md](SCALAR_MIGRATION_SUMMARY.md)** - Swagger → Scalar migration

## คุณสมบัติ

✅ **สกัดข้อมูล Timesheet จากข้อความภาษาไทย** - ระบุรายละเอียดงาน, จำนวนชั่วโมง, ประเภทงาน, และวันที่ทำงานอัตโนมัติ  
✅ **รองรับวันที่ทำงาน** ⭐⭐ ระบุวันที่ได้ (เมื่อวาน, วันจันทร์, DD/MM/YYYY) หรือไม่ระบุ = วันนี้  
✅ **ตรวจสอบความสมบูรณ์ของข้อมูล** - ระบบจะถามคำถามเพิ่มเติมหากข้อมูลไม่ครบ  
✅ **สร้างข้อความแจ้งเตือน** - ใช้ AI สร้างข้อความเตือนที่เป็นมิตรและน่ารัก  
✅ **LINE Bot Integration** - รับส่งข้อความผ่าน LINE อัตโนมัติ  
✅ **PostgreSQL Database** - บันทึกและจัดการข้อมูล timesheet  
✅ **Docker Support** - รัน all-in-one ด้วย Docker Compose  
✅ **ดูงานค้าง On-Demand** ⭐ กดปุ่มดูงานเอง ไม่เปลือง LINE quota  
✅ **Quick Reply Buttons** ⭐ ปุ่มกดเร็วสะดวก (งานของฉัน, สรุปงาน, help)  
✅ **เลือกงานก่อนบันทึก** ⭐⭐ Session-based task selection - แม่นยำ ชัดเจน  
✅ **บันทึก Task Tracking** ⭐ บันทึกลง `t_tmt_project_task_tracking` อัตโนมัติ  
✅ **รองรับ TMT Database** ⭐ เชื่อมต่อกับระบบ Task Management (TMT)

## การติดตั้งและรัน

### ข้อกำหนดเบื้องต้น

1. ติดตั้ง [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. ติดตั้ง [Ollama](https://ollama.ai/)
3. ติดตั้ง [PostgreSQL](https://www.postgresql.org/)
4. ดาวน์โหลด Llama model:
   ```bash
   ollama pull llama3.2
   ```

### การตั้งค่า

1. **สร้างไฟล์ `.env`** (จากตัวอย่าง):
   ```bash
   cp .env.example .env
   ```

2. **แก้ไขค่าใน `.env`**:
   ```env
   # Database
   DATABASE_HOST=localhost
   DATABASE_PORT=5432
   DATABASE_NAME=postgres
   DATABASE_USER=postgres
   DATABASE_PASSWORD=your_password_here

   # Ollama
   OLLAMA_BASE_URL=http://localhost:11434
   OLLAMA_MODEL=llama3.2

   # LINE Bot
   LINE_CHANNEL_ACCESS_TOKEN=your_line_token_here
   LINE_CHANNEL_SECRET=your_line_secret_here
   ```

3. **Restore packages**:
   ```bash
   dotnet restore
   ```

4. **รันโปรเจกต์**:
   ```bash
   dotnet run
   ```

5. **เข้า Scalar UI (API Documentation)**:
   ```
   http://localhost:5000/scalar/v1
   ```

   หรือ OpenAPI JSON:
   ```
   http://localhost:5000/openapi/v1.json
   ```

### Quick Start (Docker) 🐳

```bash
docker-compose up -d
```

ดู [DOCKER.md](DOCKER.md) สำหรับรายละเอียด

## การใช้งาน API

### 1. ประมวลผลข้อความ Timesheet

**Endpoint:** `POST /api/timesheet/process`

**Request:**
```json
{
  "message": "แก้บั๊กหน้าล็อกอิน 2 ชม."
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "detail": "แก้บั๊กหน้าล็อกอิน",
    "hours": 2.0,
    "issueType": "Issue",
    "isComplete": true
  },
  "message": "ประมวลผลสำเร็จ",
  "botReply": "บันทึกเรียบร้อยแล้วครับ: แก้บั๊กหน้าล็อกอิน (2 ชม.) - Issue ✅"
}
```

### 2. สร้างข้อความแจ้งเตือน

**Endpoint:** `POST /api/timesheet/reminder`

**Request:**
```json
{
  "employeeName": "สมชาย",
  "projectName": "ระบบ CRM"
}
```

**Response:**
```json
{
  "message": "เหนื่อยไหมครับวันนี้? อย่าลืมแวะมาบอกน้องไทม์นิดนึงนะว่าวันนี้ลุยงาน ระบบ CRM ไปเท่าไหร่แล้ว จะได้พักผ่อนยาวๆ ครับ"
}
```

## ตัวอย่างการใช้งาน

### กรณีข้อมูลครบถ้วน
**Input:** "ประชุมทีม 1.5 ชม."  
**Output:** 
```json
{
  "detail": "ประชุมทีม",
  "hours": 1.5,
  "issueType": "Meeting",
  "isComplete": true
}
```
**Bot Reply:** "บันทึกเรียบร้อยแล้วครับ: ประชุมทีม (1.5 ชม.) - Meeting ✅"

### กรณีข้อมูลไม่ครบ
**Input:** "แก้บั๊กหน้าหลัก"  
**Output:**
```json
{
  "detail": "แก้บั๊กหน้าหลัก",
  "hours": 0.0,
  "issueType": "Issue",
  "isComplete": false
}
```
**Bot Reply:** "รับทราบครับ งาน 'แก้บั๊กหน้าหลัก' ทำไปกี่ชั่วโมงแล้วดีครับ?"

## โครงสร้างโปรเจกต์

```
NongTimeAI/
├── Controllers/
│   ├── TimesheetController.cs      # API endpoints
│   ├── HealthController.cs         # Health check endpoints
│   └── WeatherForecastController.cs
├── Models/
│   ├── TimesheetEntry.cs           # โครงสร้างข้อมูล Timesheet
│   ├── TimesheetRequest.cs         # Request model
│   ├── TimesheetResponse.cs        # Response model
│   ├── OllamaRequest.cs            # Ollama API request
│   └── OllamaResponse.cs           # Ollama API response
├── Services/
│   ├── ITimesheetAIService.cs      # Service interface
│   └── TimesheetAIService.cs       # หลักการทำงานหลัก
├── Program.cs
├── appsettings.json
├── Dockerfile
├── docker-compose.yml
└── Docs/
    ├── README.md                   # เอกสารนี้
    ├── QUICKSTART.md               # Quick start guide
    ├── DOCKER.md                   # Docker guide
    ├── LINE_INTEGRATION.md         # LINE Bot guide
    ├── PERFORMANCE.md              # Performance tips
    ├── CONTRIBUTING.md             # Contribution guide
    ├── MISSING_FEATURES.md         # Feature roadmap
    └── CHANGELOG.md                # Version history
```

## การทำงานภายใน

1. **รับข้อความจากผู้ใช้** ผ่าน API endpoint
2. **ส่ง System Prompt + ข้อความไปยัง Ollama** พร้อมตั้งค่า temperature ต่ำ (0.1) เพื่อความแม่นยำ
3. **ทำความสะอาด JSON response** - ตัดข้อความที่ไม่ใช่ JSON ออก
4. **แปลง JSON เป็น Object** และตรวจสอบความสมบูรณ์
5. **สร้าง Bot Reply** - หากข้อมูลไม่ครบจะถามคำถามเพิ่มเติม

## การผสานกับ LINE Bot

```csharp
// ตัวอย่างการเรียกใช้ใน LINE Webhook handler
var request = new TimesheetRequest { Message = lineMessage };
var response = await httpClient.PostAsJsonAsync("http://your-api/api/timesheet/process", request);
var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>();

// ส่งข้อความกลับไปยัง LINE
await lineClient.ReplyMessageAsync(replyToken, result.BotReply);
```

## การปรับแต่ง

### เปลี่ยน Model
แก้ไขใน `appsettings.json`:
```json
{
  "Ollama": {
    "Model": "llama3"  // หรือ model อื่นๆ ที่รองรับ
  }
}
```

### ปรับแต่ง Temperature
แก้ไขใน `TimesheetAIService.cs` ในส่วน `ProcessTimesheetMessageAsync()`:
```csharp
Options = new OllamaOptions
{
    Temperature = 0.1,  // ปรับค่าตามต้องการ (0.0 - 1.0)
    TopP = 0.1
}
```

## License

MIT License
