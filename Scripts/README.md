# 🔧 Database Export/Import Scripts

สคริปต์สำหรับ Export ข้อมูลจาก SQL Server และ Import ไปยัง PostgreSQL

---

## 📂 ไฟล์ในโฟลเดอร์นี้

| ไฟล์ | คำอธิบาย |
|------|----------|
| `Export-SqlServerData.ps1` | PowerShell script สำหรับ Export ข้อมูลจาก SQL Server |
| `import_to_postgresql.sql` | SQL script สำหรับ Import ข้อมูลไปยัง PostgreSQL (สร้างอัตโนมัติ) |

---

## 🚀 วิธีการใช้งาน

### ขั้นตอนที่ 1: Export จาก SQL Server

```powershell
# เปิด PowerShell as Administrator
cd D:\Program\Line\NongTimeAI\Scripts

# Run export script (ใช้ Windows Authentication)
.\Export-SqlServerData.ps1 -ServerName "localhost" -DatabaseName "Timesheet" -OutputPath "C:\temp\export"

# หรือใช้ SQL Authentication
.\Export-SqlServerData.ps1 -ServerName "localhost" -DatabaseName "Timesheet" -OutputPath "C:\temp\export" -UseTrustedConnection:$false -Username "sa" -Password "YourPassword"
```

**ผลลัพธ์:**
- สร้างไฟล์ CSV ใน `C:\temp\export\`
- สร้างไฟล์ `import_to_postgresql.sql` สำหรับ Import

### ขั้นตอนที่ 2: Copy ไฟล์ไปยัง PostgreSQL Server

```powershell
# Windows → Linux/Mac
scp C:\temp\export\*.csv user@postgres-server:/tmp/import/

# หรือ copy แบบปกติถ้าอยู่เครื่องเดียวกัน
```

### ขั้นตอนที่ 3: Import ไปยัง PostgreSQL

```bash
# Linux/Mac Terminal
cd /tmp/import

# Import using psql
psql -U postgres -d nongtimeai -f import_to_postgresql.sql

# หรือ Import ทีละตาราง
psql -U postgres -d nongtimeai -c "\COPY tmt.t_tmt_customer FROM '/tmp/import/t_tmt_customer.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8')"
```

### ขั้นตอนที่ 4: ตรวจสอบข้อมูล

```sql
-- ใน PostgreSQL
SELECT 'Customers' AS table_name, COUNT(*) FROM tmt.t_tmt_customer
UNION ALL SELECT 'Users', COUNT(*) FROM sec.t_com_user
UNION ALL SELECT 'Project Headers', COUNT(*) FROM tmt.t_tmt_project_header
UNION ALL SELECT 'Project Tasks', COUNT(*) FROM tmt.t_tmt_project_task;
```

---

## ⚙️ Parameters สำหรับ Export Script

| Parameter | Type | Default | คำอธิบาย |
|-----------|------|---------|----------|
| `-ServerName` | String | `localhost` | ชื่อ SQL Server instance |
| `-DatabaseName` | String | `Timesheet` | ชื่อ Database |
| `-OutputPath` | String | `C:\temp\export` | Path สำหรับบันทึก CSV files |
| `-UseTrustedConnection` | Switch | `$true` | ใช้ Windows Authentication |
| `-Username` | String | - | SQL Server username (ถ้าไม่ใช้ Windows Auth) |
| `-Password` | String | - | SQL Server password (ถ้าไม่ใช้ Windows Auth) |

---

## 📊 ตารางที่จะถูก Export

1. `tmt.t_tmt_customer` - ข้อมูลลูกค้า
2. `tmt.t_tmt_project_header` - ข้อมูลโปรเจค
3. `tmt.t_tmt_project_task` - ข้อมูล Task
4. `tmt.t_tmt_project_task_member` - สมาชิกใน Task
5. `tmt.t_tmt_project_task_tracking` - การติดตาม Task
6. `sec.t_com_user` - ข้อมูลผู้ใช้
7. `sec.t_com_combobox_item` - ข้อมูล Combobox

---

## ⚠️ สิ่งที่สคริปต์ทำให้อัตโนมัติ

### ✅ Data Type Conversion
- `INT` → `BIGINT` สำหรับ Primary Keys และ Foreign Keys
- `NVARCHAR` → `VARCHAR` (รองรับ UTF-8)
- `DATETIME` → `TIMESTAMP` (ISO 8601 format)
- `DATE` → `DATE` (YYYY-MM-DD format)

### ✅ Encoding
- ใช้ **UTF-8** encoding สำหรับทุก CSV file
- รองรับภาษาไทยและตัวอักษรพิเศษ

### ✅ NULL Handling
- แปลง NULL values เป็น `NULL` string ใน CSV
- Import กลับเป็น NULL ใน PostgreSQL

### ❌ ไม่ Export
- Column `rowversion` (SQL Server specific)
- System tables

---

## 🔍 ตรวจสอบปัญหา

### ปัญหา: ภาษาไทยเป็น ???

**วิธีแก้:**
```powershell
# ตรวจสอบ encoding ของ CSV file
Get-Content C:\temp\export\t_tmt_customer.csv -Encoding UTF8

# แปลงเป็น UTF-8 ใหม่
Get-Content C:\temp\export\t_tmt_customer.csv | Out-File -FilePath C:\temp\export\t_tmt_customer_utf8.csv -Encoding UTF8
```

### ปัญหา: Primary Key Conflict

**วิธีแก้:**
```sql
-- ใน PostgreSQL: Update sequence
SELECT setval('tmt.project_header_id_seq', (SELECT MAX(project_header_id) FROM tmt.t_tmt_project_header));
```

### ปัญหา: Foreign Key Violation

**วิธีแก้:**
```sql
-- Disable triggers ชั่วคราว
ALTER TABLE tmt.t_tmt_project_task DISABLE TRIGGER ALL;

-- Import data...

-- Enable triggers กลับ
ALTER TABLE tmt.t_tmt_project_task ENABLE TRIGGER ALL;
```

---

## 📝 หมายเหตุ

- ✅ สคริปต์จะสร้างไฟล์ `import_to_postgresql.sql` อัตโนมัติ
- ✅ รองรับ Windows Authentication และ SQL Authentication
- ✅ มี Error Handling และ Transaction Rollback
- ⚠️ ควร Backup database ก่อนรัน Import
- ⚠️ ตรวจสอบ Disk space ก่อน Export (CSV files อาจใหญ่)

---

## 🔗 เอกสารเพิ่มเติม

- [EXPORT_IMPORT_DATA_GUIDE.md](../EXPORT_IMPORT_DATA_GUIDE.md) - คู่มือละเอียด
- [DATA_EXPORT_QUICK_REFERENCE.md](../DATA_EXPORT_QUICK_REFERENCE.md) - Quick Reference
- [DATABASE_DATATYPE_COMPATIBILITY.md](../DATABASE_DATATYPE_COMPATIBILITY.md) - Data Type Analysis

---

**Last Updated:** 2024-04-15  
**Version:** 1.0  
**Author:** NongTimeAI Team
