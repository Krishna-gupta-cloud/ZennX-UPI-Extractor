using System;
using System.IO;

namespace ZennX.AbbottUpiExtractor.Services
{
    public static class AppPaths
    {
        // Store database and data in the application's executing directory
        private static readonly string AppDataFolder = AppDomain.CurrentDomain.BaseDirectory;

        public static string GetAppDataFolder()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
            return AppDataFolder;
        }

        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppDataFolder(), "zennx_cashback.db");
        }

        public static string GetBackupsFolder()
        {
            string backupPath = Path.Combine(GetAppDataFolder(), "Backups");
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }
            return backupPath;
        }

        public static string GetTempFolder()
        {
            string tempPath = Path.Combine(GetAppDataFolder(), "Temp");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            return tempPath;
        }
    }
}
