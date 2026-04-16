# Export Data from SQL Server to PostgreSQL
# สคริปต์สำหรับ Export ข้อมูลจาก SQL Server และแปลง Data Type ให้พร้อม Import ไปยัง PostgreSQL

<#
.SYNOPSIS
    Export ข้อมูลจาก SQL Server ไปยัง CSV file พร้อมแปลง Data Type สำหรับ PostgreSQL

.DESCRIPTION
    สคริปต์นี้จะ:
    1. Export ข้อมูลจากตาราง SQL Server
    2. แปลง Data Type (INT → BIGINT, NVARCHAR → VARCHAR)
    3. Format Date/DateTime ให้ถูกต้อง
    4. บันทึกเป็น CSV file ด้วย UTF-8 encoding

.PARAMETER ServerName
    ชื่อ SQL Server instance (default: localhost)

.PARAMETER DatabaseName
    ชื่อ Database (default: Timesheet)

.PARAMETER OutputPath
    Path สำหรับบันทึก CSV files (default: C:\temp\export)

.EXAMPLE
    .\Export-SqlServerData.ps1 -ServerName "localhost" -DatabaseName "Timesheet" -OutputPath "C:\temp\export"

.NOTES
    Author: NongTimeAI Team
    Date: 2024-04-15
    Version: 1.0
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ServerName = "localhost",

    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "Timesheet",

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\temp\export",

    [Parameter(Mandatory=$false)]
    [switch]$UseTrustedConnection = $true,

    [Parameter(Mandatory=$false)]
    [string]$Username = "",

    [Parameter(Mandatory=$false)]
    [string]$Password = ""
)

# ตั้งค่า Error Action
$ErrorActionPreference = "Stop"

# สร้าง Output Directory ถ้ายังไม่มี
if (-not (Test-Path $OutputPath)) {
    Write-Host "📁 Creating output directory: $OutputPath" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# สร้าง Connection String
if ($UseTrustedConnection) {
    $connectionString = "Server=$ServerName;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;"
} else {
    $connectionString = "Server=$ServerName;Database=$DatabaseName;User Id=$Username;Password=$Password;TrustServerCertificate=True;"
}

Write-Host "🔌 Connecting to SQL Server: $ServerName" -ForegroundColor Cyan
Write-Host "📊 Database: $DatabaseName" -ForegroundColor Cyan
Write-Host ""

# ฟังก์ชันสำหรับ Export ตาราง
function Export-Table {
    param(
        [string]$Schema,
        [string]$TableName,
        [string]$SelectQuery,
        [string]$OutputFile
    )

    Write-Host "📤 Exporting $Schema.$TableName..." -ForegroundColor Yellow

    try {
        # สร้าง SQL Connection
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()

        # สร้าง Command
        $command = $connection.CreateCommand()
        $command.CommandText = $SelectQuery
        $command.CommandTimeout = 300  # 5 minutes

        # Execute และ Export to CSV
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $dataSet = New-Object System.Data.DataSet
        [void]$adapter.Fill($dataSet)

        $rowCount = $dataSet.Tables[0].Rows.Count

        # Export to CSV with UTF-8 encoding
        $dataSet.Tables[0] | Export-Csv -Path $OutputFile -NoTypeInformation -Encoding UTF8

        Write-Host "   ✅ Exported $rowCount rows to: $OutputFile" -ForegroundColor Green

        # ปิด Connection
        $connection.Close()

        return $rowCount
    }
    catch {
        Write-Host "   ❌ Error exporting $Schema.$TableName" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

# =============================================================================
# ตารางที่ต้อง Export
# =============================================================================

Write-Host "🚀 Starting export process..." -ForegroundColor Green
Write-Host ""

$totalRows = 0

# ----------------------------------------------------------------------------
# 1. Export t_tmt_customer
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    CAST(customer_id AS BIGINT) AS customer_id,
    customer_code,
    customer_name,
    customer_name_th,
    customer_address,
    telephone,
    fax,
    tax_id,
    is_active,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM tmt.t_tmt_customer
"@

$rows = Export-Table -Schema "tmt" -TableName "t_tmt_customer" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_tmt_customer.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 2. Export t_tmt_project_header
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    CAST(project_header_id AS BIGINT) AS project_header_id,
    master_project_id,
    project_no,
    project_name,
    project_status,
    application_type,
    project_type,
    iso_type_id,
    po_number,
    sale_id,
    CAST(customer_id AS BIGINT) AS customer_id,
    manday,
    management_cost,
    travel_cost,
    CONVERT(VARCHAR(10), plan_project_start, 23) AS plan_project_start,
    CONVERT(VARCHAR(10), plan_project_end, 23) AS plan_project_end,
    CONVERT(VARCHAR(10), revise_project_start, 23) AS revise_project_start,
    CONVERT(VARCHAR(10), revise_project_end, 23) AS revise_project_end,
    CONVERT(VARCHAR(10), actual_project_start, 23) AS actual_project_start,
    CONVERT(VARCHAR(10), actual_project_end, 23) AS actual_project_end,
    remark,
    record_type,
    year,
    is_active,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM tmt.t_tmt_project_header
"@

$rows = Export-Table -Schema "tmt" -TableName "t_tmt_project_header" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_tmt_project_header.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 3. Export t_tmt_project_task
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    CAST(project_task_id AS BIGINT) AS project_task_id,
    project_task_phase_id,
    CAST(project_header_id AS BIGINT) AS project_header_id,
    task_no,
    task_name,
    task_description,
    task_status,
    issue_type,
    priority,
    manday,
    CONVERT(VARCHAR(10), start_date, 23) AS start_date,
    CONVERT(VARCHAR(10), end_date, 23) AS end_date,
    CONVERT(VARCHAR(10), end_date_extend, 23) AS end_date_extend,
    sequence,
    remark,
    close_by,
    CONVERT(VARCHAR(23), close_date, 121) AS close_date,
    close_remark,
    is_incident,
    incident_no,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM tmt.t_tmt_project_task
"@

$rows = Export-Table -Schema "tmt" -TableName "t_tmt_project_task" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_tmt_project_task.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 4. Export t_tmt_project_task_member
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    CAST(project_task_member_id AS BIGINT) AS project_task_member_id,
    CAST(project_task_id AS BIGINT) AS project_task_id,
    user_id,
    is_leader,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM tmt.t_tmt_project_task_member
"@

$rows = Export-Table -Schema "tmt" -TableName "t_tmt_project_task_member" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_tmt_project_task_member.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 5. Export t_tmt_project_task_tracking
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    CAST(project_task_tracking_id AS BIGINT) AS project_task_tracking_id,
    CAST(project_task_id AS BIGINT) AS project_task_id,
    assignee,
    CONVERT(VARCHAR(10), actual_date, 23) AS actual_date,
    manday,
    detail,
    remark,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM tmt.t_tmt_project_task_tracking
"@

$rows = Export-Table -Schema "tmt" -TableName "t_tmt_project_task_tracking" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_tmt_project_task_tracking.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 6. Export t_com_user (SEC schema)
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    user_id,
    line_user_id,
    first_name,
    last_name,
    email_address,
    is_active,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM sec.t_com_user
"@

$rows = Export-Table -Schema "sec" -TableName "t_com_user" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_com_user.csv"
$totalRows += $rows

# ----------------------------------------------------------------------------
# 7. Export t_com_combobox_item (SEC schema)
# ----------------------------------------------------------------------------
$sql = @"
SELECT 
    combo_box_id,
    group_name,
    item_code,
    item_name,
    item_value,
    item_order,
    is_active,
    create_by,
    CONVERT(VARCHAR(23), create_date, 121) AS create_date,
    update_by,
    CONVERT(VARCHAR(23), update_date, 121) AS update_date
FROM sec.t_com_combobox_item
"@

$rows = Export-Table -Schema "sec" -TableName "t_com_combobox_item" `
    -SelectQuery $sql -OutputFile "$OutputPath\t_com_combobox_item.csv"
$totalRows += $rows

# =============================================================================
# สรุปผลการ Export
# =============================================================================

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "✅ Export completed successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "📊 Total rows exported: $totalRows" -ForegroundColor Cyan
Write-Host "📁 Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Copy CSV files ไปยัง PostgreSQL server" -ForegroundColor White
Write-Host "   2. Run: Import-PostgreSQLData.ps1" -ForegroundColor White
Write-Host "   3. หรือใช้: psql -c '\COPY ...'" -ForegroundColor White
Write-Host ""

# สร้างไฟล์ Import Script สำหรับ PostgreSQL
$importScript = @"
-- PostgreSQL Import Script
-- Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
-- Source: SQL Server ($ServerName\$DatabaseName)

-- Import Order: Parent tables → Child tables

-- 1. Import Customers
\COPY tmt.t_tmt_customer FROM '$OutputPath/t_tmt_customer.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 2. Import Users
\COPY sec.t_com_user FROM '$OutputPath/t_com_user.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 3. Import Combobox Items
\COPY sec.t_com_combobox_item FROM '$OutputPath/t_com_combobox_item.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 4. Import Project Headers
\COPY tmt.t_tmt_project_header FROM '$OutputPath/t_tmt_project_header.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 5. Import Project Tasks
\COPY tmt.t_tmt_project_task FROM '$OutputPath/t_tmt_project_task.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 6. Import Project Task Members
\COPY tmt.t_tmt_project_task_member FROM '$OutputPath/t_tmt_project_task_member.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- 7. Import Project Task Tracking
\COPY tmt.t_tmt_project_task_tracking FROM '$OutputPath/t_tmt_project_task_tracking.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8', NULL 'NULL');

-- Verify import
SELECT 'Customers' AS table_name, COUNT(*) FROM tmt.t_tmt_customer
UNION ALL SELECT 'Users', COUNT(*) FROM sec.t_com_user
UNION ALL SELECT 'Combobox Items', COUNT(*) FROM sec.t_com_combobox_item
UNION ALL SELECT 'Project Headers', COUNT(*) FROM tmt.t_tmt_project_header
UNION ALL SELECT 'Project Tasks', COUNT(*) FROM tmt.t_tmt_project_task
UNION ALL SELECT 'Task Members', COUNT(*) FROM tmt.t_tmt_project_task_member
UNION ALL SELECT 'Task Tracking', COUNT(*) FROM tmt.t_tmt_project_task_tracking;
"@

$importScriptPath = Join-Path $OutputPath "import_to_postgresql.sql"
$importScript | Out-File -FilePath $importScriptPath -Encoding UTF8

Write-Host "📄 Import script created: $importScriptPath" -ForegroundColor Cyan
Write-Host ""
