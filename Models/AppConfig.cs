using System;
using System.IO;
using System.Text.Json;

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
                return new AppConfig();

            string json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
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
            var enc = System.Security.Cryptography.ProtectedData.Protect(bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        public static string Decrypt(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            var bytes = Convert.FromBase64String(encrypted);
            var dec = System.Security.Cryptography.ProtectedData.Unprotect(bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(dec);
        }
    }
}
