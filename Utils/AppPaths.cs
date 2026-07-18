using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Jido.Utils
{
    /// <summary>
    /// Single source of truth for where Jido keeps user data.
    /// </summary>
    public static class AppPaths
    {
        public static string InstallFolder { get; } = AppDomain.CurrentDomain.BaseDirectory;

        public static string DataFolder { get; } = ResolveDataFolder();

        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

        public static string BuildsFolder => Path.Combine(DataFolder, "autopress-builds");

        public static string EmptyInventoryReferenceFile =>
            Path.Combine(DataFolder, "empty_inventory_reference.png");

        private static string ResolveDataFolder()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrWhiteSpace(appData))
                    return AppDomain.CurrentDomain.BaseDirectory;

                var folder = Path.Combine(appData, "Jido");
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch (Exception)
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }
    }
}
