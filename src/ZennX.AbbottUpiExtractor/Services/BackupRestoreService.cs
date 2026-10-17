using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class BackupRestoreService
    {
        public static (bool Success, string BackupFilePath, string ErrorMessage) CreateBackup(string? customDestinationFolder = null, string? customSourceDb = null)
        {
            try
            {
                string sourceDb = customSourceDb ?? AppPaths.GetDatabasePath();
                if (!File.Exists(sourceDb))
                {
                    return (false, string.Empty, "Database file does not exist yet.");
                }

                string backupDir = customDestinationFolder ?? AppPaths.GetBackupsFolder();
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFilename = $"zennx_cashback_backup_{timestamp}.db";
                string destFile = Path.Combine(backupDir, backupFilename);

                // Use SQLite Online Backup API for safe transaction-consistent backup
                using (var srcConn = new SqliteConnection($"Data Source={sourceDb}"))
                using (var destConn = new SqliteConnection($"Data Source={destFile}"))
                {
                    srcConn.Open();
                    destConn.Open();
                    srcConn.BackupDatabase(destConn);
                }

                return (true, destFile, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Backup failed: {ex.Message}");
            }
        }

        public static (bool Valid, string Message) ValidateBackupFile(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
            {
                return (false, "File does not exist.");
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={backupFilePath};Mode=ReadOnly");
                conn.Open();

                // Check integrity and expected schema tables
                using var cmdIntegrity = new SqliteCommand("PRAGMA integrity_check;", conn);
                var integrityResult = cmdIntegrity.ExecuteScalar()?.ToString();
                if (integrityResult != "ok")
                {
                    return (false, $"Database integrity check failed: {integrityResult}");
                }

                using var cmdTables = new SqliteCommand(
                    "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name IN ('qr_records', 'batches');", conn);
                long tableCount = (long)(cmdTables.ExecuteScalar() ?? 0L);

                if (tableCount < 2)
                {
                    return (false, "The selected file is not a valid ZennX Cashback database backup (missing required tables).");
                }

                using var countCmd = new SqliteCommand("SELECT COUNT(1) FROM qr_records", conn);
                long recordsCount = (long)(countCmd.ExecuteScalar() ?? 0L);

                return (true, $"Valid backup file. Contains {recordsCount} QR records.");
            }
            catch (Exception ex)
            {
                return (false, $"File is not a valid SQLite database: {ex.Message}");
            }
        }

        public static (bool Success, string ErrorMessage) RestoreBackup(string backupFilePath)
        {
            var validation = ValidateBackupFile(backupFilePath);
            if (!validation.Valid)
            {
                return (false, validation.Message);
            }

            try
            {
                string targetDb = AppPaths.GetDatabasePath();

                // Make a safety snapshot of current DB before restoring
                if (File.Exists(targetDb))
                {
                    CreateBackup();
                }

                // Clear connection pools so the file lock is released
                SqliteConnection.ClearAllPools();

                // Safely copy backup to current db location
                using (var srcConn = new SqliteConnection($"Data Source={backupFilePath};Mode=ReadOnly"))
                using (var destConn = new SqliteConnection($"Data Source={targetDb}"))
                {
                    srcConn.Open();
                    destConn.Open();
                    srcConn.BackupDatabase(destConn);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Restore failed: {ex.Message}");
            }
        }
    }
}
