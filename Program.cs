using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdinersDailyActivityApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ======= Logger Setup =======
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AdinersDailyActivity"
            );
            Directory.CreateDirectory(logDirectory);
            string logFilePath = Path.Combine(logDirectory, "startup.log");

            void Log(string message)
            {
                try
                {
                    File.AppendAllText(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
                }
                catch { /* jangan crash kalau logging gagal */ }
            }

            Log("Application starting...");

            // ======= Main Program =======
            string csvFileName = $"activity_log_{DateTime.Now:yyyy-MM}.csv";
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, csvFileName);

            DateTime appStartTime = DateTime.Now;
            DateTime popupTime;
            DateTime today = DateTime.Today;
            DateTime? lastEndTime = null;

            try
            {
                // Ambil end time terakhir hari ini
                if (File.Exists(csvPath))
                {
                    var lines = File.ReadAllLines(csvPath)
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .ToList();

                    var lastToday = lines
                        .Where(line => line.StartsWith(today.ToString("yyyy-MM-dd")))
                        .LastOrDefault();

                    if (lastToday != null)
                    {
                        var parts = lastToday.Split(',');
                        if (parts.Length >= 3)
                        {
                            if (DateTime.TryParseExact(
                                $"{parts[0]} {parts[2]}",
                                "yyyy-MM-dd HH:mm",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out DateTime lastPopup))
                            {
                                lastEndTime = lastPopup;
                                Log($"Found last end time today: {lastEndTime}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error reading activity log: {ex.Message}");
            }

            if (lastEndTime != null)
            {
                appStartTime = lastEndTime.Value;
            }

            // Ambil interval popup dari konfigurasi
            int intervalHours = 1;
            try
            {
                var config = AppConfig.Load();
                intervalHours = config.IntervalHours;
                Log($"Interval hours loaded from config: {intervalHours}");
            }
            catch (Exception ex)
            {
                Log($"Error loading config, using default interval {intervalHours}: {ex.Message}");
            }

            popupTime = appStartTime.AddHours(intervalHours);
            Log($"AppStartTime={appStartTime}, NextPopupTime={popupTime}");

            try
            {
                Application.Run(new DailyActivityForm(appStartTime, popupTime));
                Log("Application exited normally.");
            }
            catch (Exception ex)
            {
                Log($"Application crashed: {ex.Message}");
            }
        }
    }
}
