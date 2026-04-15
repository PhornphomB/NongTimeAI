# 🚀 คู่มือการเชื่อมต่อ LINE Bot และ PostgreSQL

## 📋 สารบัญ

1. [การเชื่อมต่อ LINE Bot](#-line-bot-setup)
2. [การติดตั้ง PostgreSQL](#-postgresql-setup)
3. [การ Deploy](#-deployment)
4. [การทดสอบ](#-testing)

---

## 📱 LINE Bot Setup

### Step 1: สร้าง LINE Channel

1. ไปที่ [LINE Developers Console](https://developers.line.biz/console/)
2. สร้าง **Provider** ใหม่ (ถ้ายังไม่มี)
3. สร้าง **Messaging API Channel**
4. ตั้งค่า Channel:
   - Channel name: `NongTime AI`
   - Channel description: `Timesheet AI Bot`
   - Category: `Productivity`

### Step 2: ดึง Channel Access Token และ Channel Secret

1. ไปที่ Tab **Messaging API**
2. คัดลอก **Channel Access Token** (long-lived)
   - กดปุ่ม "Issue" ถ้ายังไม่มี
3. ไปที่ Tab **Basic settings**
4. คัดลอก **Channel Secret**

### Step 3: ตั้งค่าใน appsettings.json

```json
{
  "Line": {
    "ChannelAccessToken": "YOUR_CHANNEL_ACCESS_TOKEN_HERE",
    "ChannelSecret": "YOUR_CHANNEL_SECRET_HERE"
  }
}
```

**หรือ** ใช้ Environment Variables (แนะนำสำหรับ Production):

```bash
# Windows (PowerShell)
$env:Line__ChannelAccessToken="YOUR_TOKEN"
$env:Line__ChannelSecret="YOUR_SECRET"

# Linux/Mac
export Line__ChannelAccessToken="YOUR_TOKEN"
export Line__ChannelSecret="YOUR_SECRET"
```

### Step 4: ตั้งค่า Webhook URL

#### ถ้ารันบน Local (ใช้ ngrok):

1. ติดตั้ง [ngrok](https://ngrok.com/download)

2. รัน API:
```bash
dotnet run
```

3. รัน ngrok (terminal ใหม่):
```bash
ngrok http 5000
```

4. คัดลอก ngrok URL (เช่น `https://abc123.ngrok.io`)

5. ตั้งค่าใน LINE Console:
   - Webhook URL: `https://abc123.ngrok.io/api/linewebhook`
   - เปิด **Use webhook**: ON
   - **Verify** webhook URL

#### ถ้า Deploy บน Server:

- Webhook URL: `https://your-domain.com/api/linewebhook`

### Step 5: ปิด Auto-reply Messages

ใน LINE Console → Messaging API tab:
- Auto-reply messages: **Disabled**
- Greeting messages: **Optional** (ตั้งเองได้ในโค้ด)

---

## 🗄️ PostgreSQL Setup

### Option 1: ติดตั้ง PostgreSQL บน Local

#### Windows:

1. ดาวน์โหลด [PostgreSQL](https://www.postgresql.org/download/windows/)
2. ติดตั้ง (เลือก password สำหรับ postgres user)
3. สร้าง database:

```sql
-- เปิด pgAdmin หรือ psql
CREATE DATABASE nongtimeai;
```

#### macOS:

```bash
# ติดตั้งด้วย Homebrew
brew install postgresql@16
brew services start postgresql@16

# สร้าง database
createdb nongtimeai
```

#### Linux (Ubuntu/Debian):

```bash
# ติดตั้ง
sudo apt update
sudo apt install postgresql postgresql-contrib

# สร้าง database
sudo -u postgres createdb nongtimeai

# ตั้ง password สำหรับ postgres user
sudo -u postgres psql
ALTER USER postgres PASSWORD 'your_password';
\q
```

### Option 2: ใช้ Docker

```bash
# รัน PostgreSQL container
docker run --name nongtimeai-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=nongtimeai \
  -p 5432:5432 \
  -d postgres:16-alpine

# ตรวจสอบ
docker ps
docker logs nongtimeai-postgres
```

### การตั้งค่า Connection String

แก้ไข `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nongtimeai;Username=postgres;Password=your_password"
  }
}
```

**Components:**
- **Host**: `localhost` (หรือ IP ของ PostgreSQL server)
- **Port**: `5432` (default)
- **Database**: `nongtimeai`
- **Username**: `postgres` (หรือ user ที่สร้าง)
- **Password**: รหัสผ่านของ user

### รัน Migrations

```bash
# สร้าง migration (ทำแล้ว)
dotnet ef migrations add InitialCreate

# Apply migration ไปยัง database
dotnet ef database update
```

**หรือ** จะ auto-migrate เมื่อ start app (โค้ดมีอยู่แล้วใน Program.cs):
```bash
dotnet run
# Database จะถูกสร้างอัตโนมัติ
```

### ตรวจสอบ Database Schema

```sql
-- เชื่อมต่อ database
psql -U postgres -d nongtimeai

-- ดูตาราง
\dt

-- ตรวจสอบโครงสร้าง
\d timesheets
\d projects
\d users
```

ผลลัพธ์ที่คาดหวัง:
- ✅ Table: `timesheets`
- ✅ Table: `projects`
- ✅ Table: `users`
- ✅ Table: `__EFMigrationsHistory`

---

## 🚀 Deployment

### Option 1: Docker Compose (แนะนำ)

อัพเดท `docker-compose.yml`:

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:16-alpine
    container_name: nongtimeai-postgres
    environment:
      POSTGRES_DB: nongtimeai
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    networks:
      - nongtime-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Ollama Service
  ollama:
    image: ollama/ollama:latest
    container_name: nongtimeai-ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    networks:
      - nongtime-network

  # NongTimeAI API
  nongtimeai-api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: nongtimeai-api
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=nongtimeai;Username=postgres;Password=postgres
      - Ollama__BaseUrl=http://ollama:11434
      - Ollama__Model=llama3.2
      - Line__ChannelAccessToken=${LINE_CHANNEL_ACCESS_TOKEN}
      - Line__ChannelSecret=${LINE_CHANNEL_SECRET}
    depends_on:
      postgres:
        condition: service_healthy
      ollama:
        condition: service_started
    networks:
      - nongtime-network

volumes:
  postgres_data:
  ollama_data:

networks:
  nongtime-network:
    driver: bridge
```

สร้างไฟล์ `.env`:

```bash
LINE_CHANNEL_ACCESS_TOKEN=your_token_here
LINE_CHANNEL_SECRET=your_secret_here
```

รัน:

```bash
# Start all services
docker-compose up -d

# Pull Ollama model
docker exec -it nongtimeai-ollama ollama pull llama3.2

# ดู logs
docker-compose logs -f nongtimeai-api

# ตรวจสอบ database
docker exec -it nongtimeai-postgres psql -U postgres -d nongtimeai -c "\dt"
```

### Option 2: Deploy แยกส่วน

1. **PostgreSQL**: ใช้ cloud service เช่น:
   - Amazon RDS
   - Azure Database for PostgreSQL
   - Google Cloud SQL
   - Supabase (ฟรี)
   - ElephantSQL (ฟรี 20MB)

2. **API**: Deploy บน:
   - Azure App Service
   - AWS Elastic Beanstalk
   - Google Cloud Run
   - DigitalOcean App Platform
   - Render.com

3. **Ollama**: รันบน VM หรือ dedicated server

---

## 🧪 Testing

### 1. ทดสอบ Database Connection

```bash
# Health check endpoint
curl http://localhost:5000/api/health

# ตรวจสอบ database ใน logs
docker logs nongtimeai-api | grep "Database"
```

### 2. ทดสอบ LINE Webhook

#### ใช้ LINE Developers Console:

1. ไปที่ Messaging API tab
2. กด **Verify** ที่ Webhook URL
3. ควรได้ Success

#### ทดสอบด้วย curl:

```bash
# ส่ง test webhook (จำลอง LINE server)
curl -X POST http://localhost:5000/api/linewebhook \
  -H "Content-Type: application/json" \
  -H "X-Line-Signature: test" \
  -d '{
    "events": []
  }'
```

### 3. ทดสอบผ่าน LINE App

1. Scan QR code ของ bot ใน LINE Console
2. Add bot as friend
3. ส่งข้อความ: "แก้บั๊กหน้าล็อกอิน 2 ชม."
4. Bot ควรตอบกลับ

### 4. ตรวจสอบ Database

```sql
-- เชื่อมต่อ database
psql -U postgres -d nongtimeai

-- ดูข้อมูล timesheets
SELECT * FROM timesheets ORDER BY created_at DESC LIMIT 5;

-- ดูข้อมูล users
SELECT * FROM users;

-- ดูสถิติ
SELECT 
    user_id,
    DATE(date) as work_date,
    SUM(hours) as total_hours,
    COUNT(*) as task_count
FROM timesheets
GROUP BY user_id, DATE(date)
ORDER BY work_date DESC;
```

---

## 🔧 Troubleshooting

### ปัญหา: ไม่สามารถเชื่อมต่อ PostgreSQL

**วิธีแก้:**

```bash
# ตรวจสอบ PostgreSQL รันอยู่หรือไม่
# Windows
Get-Service postgresql*

# Linux/Mac
sudo systemctl status postgresql

# Docker
docker ps | grep postgres

# ทดสอบ connection
psql -U postgres -d nongtimeai
```

### ปัญหา: Migration ล้มเหลว

**วิธีแก้:**

```bash
# ลบ migration และสร้างใหม่
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update

# หรือ ลบ database และสร้างใหม่
DROP DATABASE nongtimeai;
CREATE DATABASE nongtimeai;
dotnet ef database update
```

### ปัญหา: LINE Webhook ไม่ทำงาน

**วิธีแก้:**

1. ตรวจสอบ Webhook URL ถูกต้อง
2. ตรวจสอบ SSL certificate (HTTPS จำเป็น)
3. ดู logs:
```bash
docker logs nongtimeai-api -f
```
4. ตรวจสอบ Channel Secret ถูกต้อง

### ปัญหา: Bot ไม่ตอบกลับ

**วิธีแก้:**

1. ตรวจสอบ Channel Access Token
2. ตรวจสอบ Auto-reply ปิดหรือไม่
3. ดู logs เพื่อดู error
4. ทดสอบ API โดยตรง:

```bash
curl -X POST http://localhost:5000/api/timesheet/process \
  -H "Content-Type: application/json" \
  -d '{"message": "แก้บั๊ก 2 ชม."}'
```

---

## 📊 Database Schema

```
┌─────────────┐      ┌──────────────┐      ┌─────────┐
│  users      │      │  timesheets  │      │ projects│
├─────────────┤      ├──────────────┤      ├─────────┤
│ id          │      │ id           │      │ id      │
│ line_user_id│◄─────│ user_id      │      │ name    │
│ display_name│      │ detail       │      │ is_active│
│ is_active   │      │ hours        │      └─────────┘
│ created_at  │      │ issue_type   │           △
└─────────────┘      │ date         │           │
                     │ project_id   │───────────┘
                     │ created_at   │
                     └──────────────┘
```

---

## 🎯 Quick Commands

```bash
# รัน API + PostgreSQL + Ollama
docker-compose up -d

# Pull Llama model
docker exec -it nongtimeai-ollama ollama pull llama3.2

# ดู logs
docker-compose logs -f

# ตรวจสอบ database
docker exec -it nongtimeai-postgres psql -U postgres -d nongtimeai

# Restart API only
docker-compose restart nongtimeai-api

# Stop all
docker-compose down

# Stop และลบ volumes (ระวัง: ข้อมูลจะหาย!)
docker-compose down -v
```

---

## 📚 เอกสารเพิ่มเติม

- [LINE Messaging API Reference](https://developers.line.biz/en/reference/messaging-api/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Npgsql Documentation](https://www.npgsql.org/doc/)

---

**🎉 ตอนนี้ระบบพร้อมใช้งานแล้ว!**

สรุป:
- ✅ LINE Bot webhook รับข้อความได้
- ✅ AI ประมวลผลและสกัดข้อมูล
- ✅ บันทึกลง PostgreSQL
- ✅ ตอบกลับผู้ใช้ผ่าน LINE
