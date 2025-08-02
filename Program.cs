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

            string logDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = $"activity_log_{DateTime.Now:yyyy-MM}.csv";
            string logPath = Path.Combine(logDirectory, fileName);

            DateTime appStartTime = DateTime.Now;
            DateTime popupTime;
            DateTime today = DateTime.Today;
            DateTime? lastEndTime = null;

            // Ambil end time terakhir hari ini
            if (File.Exists(logPath))
            {
                var lines = File.ReadAllLines(logPath)
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
                        }
                    }
                }
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
            }
            catch { }

            popupTime = appStartTime.AddHours(intervalHours);

            Application.Run(new DailyActivityForm(appStartTime, popupTime));
        }
    }
}
