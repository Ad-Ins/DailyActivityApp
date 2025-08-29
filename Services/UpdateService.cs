using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ComponentModel;

namespace AdinersDailyActivityApp.Services
{
    public class UpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/Ad-Ins/DailyActivityApp/releases/latest";
        
        public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        
        public static async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "DailyActivityApp-UpdateChecker");
                
                var response = await client.GetStringAsync(GITHUB_API_URL);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);
                
                if (release?.tag_name != null)
                {
                    var latestVersion = release.tag_name.TrimStart('v');
                    var currentVersion = CurrentVersion;
                    
                    return IsNewerVersion(latestVersion, currentVersion);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update check failed: {ex.Message}");
            }
            
            return false;
        }
        
        public static async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "DailyActivityApp-UpdateChecker");
                
                var response = await client.GetStringAsync(GITHUB_API_URL);
                return JsonSerializer.Deserialize<GitHubRelease>(response);
            }
            catch
            {
                return null;
            }
        }
        
        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = new Version(latestVersion);
                var current = new Version(currentVersion);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }
        
        public static async Task<string?> DownloadUpdateAsync(GitHubRelease release, IProgress<int>? progress = null)
        {
            try
            {
                var setupAsset = release.assets?.FirstOrDefault(a => a.name?.Contains("Setup") == true);
                if (setupAsset?.browser_download_url == null) return null;
                
                string tempPath = Path.Combine(Path.GetTempPath(), setupAsset.name!);
                
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "DailyActivityApp-UpdateChecker");
                
                using var response = await client.GetAsync(setupAsset.browser_download_url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((totalRead * 100) / totalBytes);
                        progress?.Report(progressPercentage);
                    }
                }
                
                return tempPath;
            }
            catch
            {
                return null;
            }
        }
        
        public static void OpenDownloadPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Ad-Ins/DailyActivityApp/releases/latest",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open download page: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    
    public class GitHubRelease
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? body { get; set; }
        public bool prerelease { get; set; }
        public DateTime published_at { get; set; }
        public GitHubAsset[]? assets { get; set; }
    }
    
    public class GitHubAsset
    {
        public string? name { get; set; }
        public string? browser_download_url { get; set; }
        public long size { get; set; }
    }
}