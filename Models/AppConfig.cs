using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;

namespace AdinersDailyActivityApp
{
    /// <summary>
    /// Konfigurasi aplikasi (interval popup, dsb).
    /// </summary>
    public class AppConfig
    {
        public int IntervalHours { get; set; } = 1;

        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigFilePath))
            {
                var defaultConfig = new AppConfig();
                defaultConfig.Save(); // Save default config if it doesn't exist
                return defaultConfig;
            }

            string json = File.ReadAllText(ConfigFilePath);
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch (JsonException ex)
            {
                // Log the error (e.g., to console or a log file)
                Console.WriteLine($"Error deserializing config.json: {ex.Message}");
                // Return a new default config and save it to overwrite the corrupted one
                var defaultConfig = new AppConfig();
                defaultConfig.Save();
                return defaultConfig;
            }
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }

        public string JiraUrl { get; set; }
        public string JiraUsername { get; set; }
        public string JiraPasswordEncrypted { get; set; }

        // Tambahkan fungsi enkripsi/dekripsi sederhana (gunakan DPAPI)
        public static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        public static string Decrypt(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            var bytes = Convert.FromBase64String(encrypted);
            var dec = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(dec);
        }
    }
}
