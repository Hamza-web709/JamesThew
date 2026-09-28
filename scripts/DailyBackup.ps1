<#
.SYNOPSIS
    Repeatable Daily Database Backup and Retention Automation for JamesThew.
.DESCRIPTION
    Automates SQL Server full database backups with CHECKSUM and INIT,
    performs RESTORE VERIFYONLY integrity verification, prunes backups exceeding
    retention threshold, and optionally executes a non-destructive dry-run test restore
    to an ephemeral database.
.PARAMETER DatabaseName
    Target database to back up (e.g. 'JamesThew_Demo' or 'JamesThew_Development'). Default: 'JamesThew_Demo'.
.PARAMETER ServerInstance
    SQL Server instance name. Default: '(localdb)\MSSQLLocalDB'.
.PARAMETER BackupDirectory
    Destination directory for timestamped .bak files. Default: '<repo>/backups'.
.PARAMETER RetentionDays
    Number of days to retain backup files before automated cleanup. Default: 7.
.PARAMETER TestRestore
    Switch parameter to execute a dry-run test restore and row count verification.
#>
[CmdletBinding()]
param(
    [string]$DatabaseName = "JamesThew_Demo",
    [string]$ServerInstance = "(localdb)\MSSQLLocalDB",
    [string]$BackupDirectory = "",
    [int]$RetentionDays = 7,
    [switch]$TestRestore = $false
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $BackupDirectory = Join-Path $PSScriptRoot "..\backups"
}

$BackupDirectory = [System.IO.Path]::GetFullPath($BackupDirectory)
if (-not (Test-Path $BackupDirectory)) {
    New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFileName = "${DatabaseName}_${timestamp}.bak"
$backupFilePath = Join-Path $BackupDirectory $backupFileName

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " JamesThew Database Backup Automation" -ForegroundColor Cyan
Write-Host " Target Database : $DatabaseName"
Write-Host " Server Instance : $ServerInstance"
Write-Host " Backup File     : $backupFilePath"
Write-Host " Retention Days  : $RetentionDays"
Write-Host " Test Restore    : $TestRestore"
Write-Host "==========================================================" -ForegroundColor Cyan

# Connection string to master database
$connStr = "Server=$ServerInstance;Database=master;Integrated Security=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

function Invoke-SqlNonQuery([string]$sql, [int]$timeout = 120) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = $timeout
    $cmd.ExecuteNonQuery() | Out-Null
}

function Invoke-SqlScalar([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    return $cmd.ExecuteScalar()
}

try {
    # 1. Verify target database exists
    $escapedDb = $DatabaseName.Replace("'", "''")
    $dbExists = Invoke-SqlScalar "SELECT DB_ID(N'$escapedDb')"
    if ($null -eq $dbExists -or [System.DBNull]::Value.Equals($dbExists)) {
        throw "Target database '$DatabaseName' does not exist on instance '$ServerInstance'."
    }

    # 2. Execute full backup with CHECKSUM and INIT
    Write-Host "[1/4] Executing full database backup with CHECKSUM..." -ForegroundColor Yellow
    $escapedPath = $backupFilePath.Replace("'", "''")
    $backupSql = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$escapedPath' WITH INIT, CHECKSUM, STATS = 20"
    Invoke-SqlNonQuery $backupSql
    Write-Host "      Database backup completed successfully." -ForegroundColor Green

    # 3. Integrity verification: RESTORE VERIFYONLY
    Write-Host "[2/4] Verifying backup integrity (RESTORE VERIFYONLY)..." -ForegroundColor Yellow
    $verifySql = "RESTORE VERIFYONLY FROM DISK = N'$escapedPath' WITH CHECKSUM"
    Invoke-SqlNonQuery $verifySql
    Write-Host "      Backup integrity verified with CHECKSUM." -ForegroundColor Green

    # File size info
    $fileInfo = Get-Item $backupFilePath
    $fileSizeMb = [math]::Round($fileInfo.Length / 1MB, 2)
    Write-Host "      Backup File Size: $fileSizeMb MB ($($fileInfo.Length) bytes)"

    # 4. Optional dry-run test restore
    if ($TestRestore) {
        Write-Host "[3/4] Performing dry-run test restore..." -ForegroundColor Yellow
        $restoreDbName = "${DatabaseName}_DryRunRestore_${timestamp}"
        $escapedRestoreDb = $restoreDbName.Replace("'", "''")

        # Discover logical file names
        $fileListCmd = $conn.CreateCommand()
        $fileListCmd.CommandText = "RESTORE FILELISTONLY FROM DISK = N'$escapedPath'"
        $reader = $fileListCmd.ExecuteReader()
        $logicalFiles = @()
        while ($reader.Read()) {
            $logicalFiles += [PSCustomObject]@{
                LogicalName = $reader.GetString(0)
                Type = $reader.GetString(2)
            }
        }
        $reader.Close()

        $tempFolder = Join-Path ([System.IO.Path]::GetTempPath()) "JamesThew_RestoreTest_${timestamp}"
        New-Item -ItemType Directory -Path $tempFolder -Force | Out-Null

        $moveClauses = @()
        $idx = 0
        foreach ($lf in $logicalFiles) {
            $ext = if ($lf.Type -eq "L") { "ldf" } else { "mdf" }
            $targetPath = Join-Path $tempFolder "restore_${idx}.${ext}"
            $escapedTarget = $targetPath.Replace("'", "''")
            $escapedLog = $lf.LogicalName.Replace("'", "''")
            $moveClauses += "MOVE N'$escapedLog' TO N'$escapedTarget'"
            $idx++
        }
        $moveSql = $moveClauses -join ", "

        try {
            $restoreSql = "RESTORE DATABASE [$restoreDbName] FROM DISK = N'$escapedPath' WITH $moveSql, CHECKSUM"
            Invoke-SqlNonQuery $restoreSql 180

            # Verify table count in restored database
            $tableCount = Invoke-SqlScalar "SELECT COUNT(*) FROM [$restoreDbName].sys.tables"
            Write-Host "      Test restore verified! Restored table count: $tableCount tables." -ForegroundColor Green
        }
        finally {
            [System.Data.SqlClient.SqlConnection]::ClearAllPools()
            Invoke-SqlNonQuery "IF DB_ID(N'$escapedRestoreDb') IS NOT NULL BEGIN ALTER DATABASE [$restoreDbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$restoreDbName]; END"
            if (Test-Path $tempFolder) {
                Remove-Item -Path $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    } else {
        Write-Host "[3/4] Skipping dry-run restore (-TestRestore not specified)." -ForegroundColor Gray
    }

    # 5. Retention policy: prune backups older than $RetentionDays
    Write-Host "[4/4] Applying retention policy (older than $RetentionDays days)..." -ForegroundColor Yellow
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    $pattern = "${DatabaseName}_*.bak"
    $oldBackups = Get-ChildItem -Path $BackupDirectory -Filter $pattern | Where-Object { $_.CreationTime -lt $cutoff }
    $prunedCount = 0
    foreach ($old in $oldBackups) {
        Write-Host "      Pruning expired backup: $($old.Name) (Created: $($old.CreationTime))" -ForegroundColor DarkGray
        Remove-Item -Path $old.FullName -Force
        $prunedCount++
    }
    Write-Host "      Retention check complete. Expired files pruned: $prunedCount." -ForegroundColor Green

    # Log to backup audit manifest
    $auditFile = Join-Path $BackupDirectory "backup_audit_log.csv"
    $auditHeader = "TimestampUtc,DatabaseName,BackupFile,SizeBytes,VerifiedChecksum,TestRestored"
    if (-not (Test-Path $auditFile)) {
        Set-Content -Path $auditFile -Value $auditHeader
    }
    $utcNow = (Get-Date).ToUniversalTime().ToString("o")
    $logLine = "$utcNow,$DatabaseName,$backupFileName,$($fileInfo.Length),True,$TestRestore"
    Add-Content -Path $auditFile -Value $logLine

    Write-Host ""
    Write-Host "Backup operation completed successfully!" -ForegroundColor Green
    Write-Host "Artifact: $backupFilePath" -ForegroundColor Green
}
finally {
    $conn.Close()
}
