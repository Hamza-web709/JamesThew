# JamesThew Database Backup, Retention & Disaster Recovery (REQ-022)

> [!NOTE]
> **Status: PARTIAL**  
> - **One-time backup & restore**: Verified via unit tests (`FoundationTests.cs`) and demo database export (`JamesThew_Demo.bak`).
> - **Automated repeatable script & retention**: Implemented in [`scripts/DailyBackup.ps1`](../scripts/DailyBackup.ps1) with `CHECKSUM` validation, `RESTORE VERIFYONLY`, retention pruning, and test restore.
> - **Daily OS daemon scheduling**: Not configured as a persistent system daemon in this local demo environment. In accordance with academic honesty guidelines, this requirement is labeled **Partial** rather than Passed.

---

## 1. Overview & Architecture

The backup procedure protects the relational integrity of the JamesThew portal database (`JamesThew_Demo` and `JamesThew_Development`). The solution provides:
1. **Full Database Backups**: Executed using SQL Server `BACKUP DATABASE ... WITH INIT, CHECKSUM`.
2. **Integrity Verification**: Verified using `RESTORE VERIFYONLY ... WITH CHECKSUM`.
3. **Retention Management**: Automatic cleanup of backup files exceeding a configurable retention window (default: 7 days).
4. **Dry-Run Test Restore**: Optional non-destructive restoration to an ephemeral test database to verify table counts and relational integrity before dropping the scratch database.
5. **Audit Logging**: Timestamped records maintained in `backups/backup_audit_log.csv`.

---

## 2. Automated Script Usage (`DailyBackup.ps1`)

The backup script is located at `scripts/DailyBackup.ps1`.

### Basic Execution (Default: `JamesThew_Demo`, 7-day retention)
```powershell
powershell -ExecutionPolicy Bypass -File scripts\DailyBackup.ps1
```

### Execution with Verification Test Restore
```powershell
powershell -ExecutionPolicy Bypass -File scripts\DailyBackup.ps1 -DatabaseName "JamesThew_Demo" -TestRestore
```

### Script Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `-DatabaseName` | `string` | `"JamesThew_Demo"` | The target database to back up. |
| `-ServerInstance` | `string` | `"(localdb)\MSSQLLocalDB"` | SQL Server instance name. |
| `-BackupDirectory` | `string` | `<repo>\backups` | Target folder for timestamped `.bak` files. |
| `-RetentionDays` | `int` | `7` | Retention period in days; older backups are pruned. |
| `-TestRestore` | `switch` | `$false` | When specified, restores to an ephemeral database to verify table integrity. |

---

## 3. Manual Restoration Procedure

If a disaster recovery scenario requires restoring a backup file to SQL Server LocalDB:

### Step 1: Discover Logical File Names
```sql
RESTORE FILELISTONLY FROM DISK = N'C:\path\to\JamesThew_Demo_20260928_052327.bak';
```
Typical output shows logical data file `JamesThew_Demo` (Type `D`) and log file `JamesThew_Demo_log` (Type `L`).

### Step 2: Restore Database with Relocation (MOVE)
```sql
RESTORE DATABASE [JamesThew_Demo]
FROM DISK = N'C:\path\to\JamesThew_Demo_20260928_052327.bak'
WITH 
    MOVE N'JamesThew_Demo' TO N'C:\Users\<User>\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\JamesThew_Demo.mdf',
    MOVE N'JamesThew_Demo_log' TO N'C:\Users\<User>\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\JamesThew_Demo_log.ldf',
    REPLACE,
    CHECKSUM;
```

### Step 3: Verify Relational Integrity
```sql
USE [JamesThew_Demo];
SELECT COUNT(*) AS TotalContentItems FROM ContentItems;
SELECT COUNT(*) AS TotalContests FROM Contests;
SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;
```

---

## 4. Scheduling Automated Daily Backups (Host Configuration)

To elevate this requirement from **Partial** to **Passed** in a production or persistent server environment, schedule `DailyBackup.ps1` via Windows Task Scheduler:

### Windows Scheduled Task Command (Run as Administrator)
```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File C:\path\to\JamesThew\scripts\DailyBackup.ps1 -DatabaseName JamesThew_Demo -TestRestore"

$trigger = New-ScheduledTaskTrigger -Daily -At "02:00AM"

$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd

Register-ScheduledTask -TaskName "JamesThew_Daily_Database_Backup" `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Automated daily backup and retention for JamesThew database."
```
