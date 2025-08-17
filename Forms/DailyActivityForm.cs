using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;
using ClosedXML.Excel;
using AdinersDailyActivityApp.Dialog;

namespace AdinersDailyActivityApp
{
    public class DailyActivityForm : Form
    {
        #region Fields
        private Label lblTitle = null!;
        private TextBox txtActivity = null!;
        private ListBox lstActivityHistory = null!;
        private PictureBox logoPictureBox = null!;
        private System.Windows.Forms.Timer popupTimer = null!;
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip trayMenu = null!;
        private ContextMenuStrip historyContextMenu = null!;
        private DateTime appStartTime;
        private DateTime popupTime;
        private int popupIntervalInMinutes;
        private AppConfig config = null!;
        private bool isLunchPopupShown = false;
        private bool isLunchHandled = false;
        private DateTime lastActivityInputTime = DateTime.MinValue;
        #endregion
        #region Constructor
        public DailyActivityForm(DateTime appStartTime, DateTime popupTime)
        {
            this.appStartTime = appStartTime;
            this.popupTime = popupTime;
            InitializeComponent();
            SetupForm();
            LoadConfig();
            StartTimer();
            LoadLogHistory();
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }
        #endregion
        #region Initialization
        private void LoadConfig()
        {
            config = AppConfig.Load();
            popupIntervalInMinutes = config.IntervalHours * 60;
        }
        private void InitializeComponent()
        {
            lblTitle = new Label();
            txtActivity = new TextBox();
            lstActivityHistory = new ListBox();
            logoPictureBox = new PictureBox();
            popupTimer = new System.Windows.Forms.Timer();
            // Tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(30, 30, 30);
            trayMenu.ForeColor = Color.White;
            trayMenu.Items.Add("Input Activity Now", null, OnInputNowClicked);
            trayMenu.Items.Add("Export Log to Excel", null, OnExportLogClicked);
            trayMenu.Items.Add("Set Interval...", null, OnSetIntervalClicked);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, OnExitClicked);
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            Icon trayAppIcon = File.Exists(iconPath) ? MakeIconWhite(new Icon(iconPath)) : SystemIcons.Application;
            trayIcon = new NotifyIcon
            {
                Text = "Adiners - Daily Activity",
                Icon = trayAppIcon,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowFullScreenInput();
            // History context menu
            historyContextMenu = new ContextMenuStrip();
            historyContextMenu.BackColor = Color.FromArgb(30, 30, 30);
            historyContextMenu.ForeColor = Color.White;
            historyContextMenu.Items.Add("Edit", null, OnEditHistoryClicked);
            lstActivityHistory.ContextMenuStrip = historyContextMenu;
        }
        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.Opacity = 0.95;
            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.KeyDown += Form1_KeyDown;
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(20),
                BackColor = Color.FromArgb(20, 20, 20)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoPictureBox.Size = new Size(200, 60);
            logoPictureBox.Dock = DockStyle.Fill;
            LoadLogoImage();
            mainLayout.Controls.Add(logoPictureBox, 0, 0);
            lblTitle.Text = "What are you working on?";
            lblTitle.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(lblTitle, 0, 1);
            txtActivity.Font = new Font("Segoe UI", 16);
            txtActivity.BackColor = Color.FromArgb(40, 40, 40);
            txtActivity.ForeColor = Color.White;
            txtActivity.BorderStyle = BorderStyle.FixedSingle;
            txtActivity.Dock = DockStyle.Fill;
            txtActivity.Margin = new Padding(5);
            mainLayout.Controls.Add(txtActivity, 0, 2);
            lstActivityHistory.Font = new Font("Segoe UI", 12);
            lstActivityHistory.BackColor = Color.FromArgb(35, 35, 35);
            lstActivityHistory.ForeColor = Color.White;
            lstActivityHistory.BorderStyle = BorderStyle.FixedSingle;
            lstActivityHistory.Dock = DockStyle.Fill;
            lstActivityHistory.Margin = new Padding(5);
            lstActivityHistory.MouseDoubleClick += LstActivityHistory_MouseDoubleClick;
            mainLayout.Controls.Add(lstActivityHistory, 0, 3);
            this.Controls.Add(mainLayout);
            this.Hide();
        }
        #endregion
        #region Event Handlers
        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
            }
            if (e.KeyCode == Keys.Enter || (e.Control && e.KeyCode == Keys.Enter))
            {
                SaveActivity();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        private void LstActivityHistory_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (lstActivityHistory.SelectedItem != null)
            {
                string selectedItemText = lstActivityHistory.SelectedItem!.ToString();
                int closingBracketIndex = selectedItemText.IndexOf(']');
                if (closingBracketIndex != -1 && closingBracketIndex + 1 < selectedItemText.Length)
                {
                    int startIndex = closingBracketIndex + 1;
                    while (startIndex < selectedItemText.Length && char.IsWhiteSpace(selectedItemText[startIndex]))
                        startIndex++;
                    txtActivity.Text = (startIndex < selectedItemText.Length)
                        ? selectedItemText.Substring(startIndex).Trim()
                        : string.Empty;
                }
                else
                {
                    txtActivity.Text = selectedItemText.Trim();
                }
            }
        }
        private void OnExitClicked(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void OnInputNowClicked(object? sender, EventArgs e)
        {
            if ((DateTime.Now - lastActivityInputTime).TotalSeconds < 60 && lastActivityInputTime != DateTime.MinValue)
            {
                trayIcon.ShowBalloonTip(2000, "Cooldown", "Harus menunggu minimal 1 menit sebelum input activity baru.", ToolTipIcon.Info);
                return;
            }
            ShowFullScreenInput();
        }
        private void OnExportLogClicked(object? sender, EventArgs e)
        {
            using (var exportDialog = new ExportDateRangeDialog())
            {
                if (exportDialog.ShowDialog() == DialogResult.OK)
                {
                    DateTime fromDate = exportDialog.FromDate;
                    DateTime toDate = exportDialog.ToDate;

                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                        saveFileDialog.Title = "Export Activity Log";
                        saveFileDialog.FileName = "activity_log.xlsx";

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            ExportLogToExcel(fromDate, toDate, saveFileDialog.FileName);
                        }
                    }
                }
            }
        }
        private void OnSetIntervalClicked(object? sender, EventArgs e)
        {
            using (var setIntervalForm = new SetIntervalDialog(config.IntervalHours))
            {
                if (setIntervalForm.ShowDialog() == DialogResult.OK)
                {
                    config.IntervalHours = setIntervalForm.IntervalMinutes;
                    config.Save();
                    LoadConfig();
                    StartTimer(); // refresh interval
                }
            }
        }
        private void OnEditHistoryClicked(object? sender, EventArgs e)
        {
            if (lstActivityHistory.SelectedItem != null)
            {
                string selectedItem = lstActivityHistory.SelectedItem!.ToString();
                int closingBracketIndex = selectedItem.IndexOf(']');
                if (closingBracketIndex != -1)
                {
                    string inside = selectedItem.Substring(1, closingBracketIndex - 1);
                    string[] parts = inside.Split('|').Select(p => p.Trim()).ToArray();
                    if (parts.Length == 3)
                    {
                        string dateStr = parts[0];
                        string timesStr = parts[1];
                        string[] timeParts = timesStr.Split('-').Select(t => t.Trim()).ToArray();
                        if (timeParts.Length == 2)
                        {
                            string endStr = timeParts[1];
                            if (DateTime.TryParseExact($"{dateStr} {endStr}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                            {
                                string activity = selectedItem.Substring(closingBracketIndex + 1).Trim();
                                string logEntry = $"[{timestamp.ToString(CultureInfo.InvariantCulture)}] {activity}";
                                txtActivity.Text = activity;
                                lstActivityHistory.Items.Remove(selectedItem);
                                RemoveActivityFromLogFile(logEntry);
                            }
                        }
                    }
                }
            }
        }
        private void popupTimer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (!isLunchPopupShown && now.Hour == 12 && now.Minute >= 0 && now.Minute <= 15)
            {
                ShowLunchPopup();
                isLunchPopupShown = true;
                isLunchHandled = false;
            }
            if (now.Hour > 13)
                isLunchPopupShown = false;
            TimeSpan timeSinceLastPopup = now - popupTime;
            if (timeSinceLastPopup.TotalMinutes >= popupIntervalInMinutes && now.Hour >= 9 && now.Hour <= 17)
            {
                ShowFullScreenInput();
                popupTime = DateTime.Now;
            }
        }
        private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e) => SaveActivity();
        #endregion
        #region Methods
        private void ShowFullScreenInput()
        {
            if ((DateTime.Now - lastActivityInputTime).TotalSeconds < 60 && lastActivityInputTime != DateTime.MinValue)
            {
                // Masih cooldown -> jangan tampilkan form
                this.Hide();
                return;
            }
            txtActivity.Text = "";
            txtActivity.Focus();
            this.WindowState = FormWindowState.Maximized;
            this.Show();
            this.BringToFront();
            this.Activate();
        }
        private void SaveActivity()
        {
            string activity = txtActivity.Text.Trim();
            if (!string.IsNullOrEmpty(activity))
            {
                string logEntry = $"[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}] {activity}";
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
                LoadLogHistory();
                lastActivityInputTime = DateTime.Now;
            }
            this.Hide();
        }
        private string FormatLogEntry(DateTime startTime, DateTime endTime, string activity)
        {
            string dateStr = endTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            string startStr = startTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            string endStr = endTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            int minutes = (int)(endTime - startTime).TotalMinutes;
            if (minutes < 0) minutes = 0;
            return $"[{dateStr} | {startStr} - {endStr} | {minutes} minutes] {activity}";
        }
        private void LoadLogHistory()
        {
            lstActivityHistory.Items.Clear();
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var entries = new List<(DateTime time, string activity)>();
                foreach (string line in lines)
                {
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0)
                    {
                        string timestampStr = line.Substring(1, timestampEndIndex - 1);
                        if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                        {
                            string activity = line.Substring(timestampEndIndex + 2);
                            entries.Add((time, activity));
                        }
                    }
                }
                entries.Sort((a, b) => a.time.CompareTo(b.time)); // Sort ascending (oldest to newest)
                var displays = new List<string>();
                DateTime? currentDate = null;
                DateTime? prevTime = null;
                foreach (var entry in entries)
                {
                    DateTime time = entry.time;
                    if (!currentDate.HasValue || time.Date != currentDate.Value.Date)
                    {
                        // New day
                        currentDate = time.Date;
                        prevTime = new DateTime(time.Year, time.Month, time.Day, 8, 0, 0);
                        if (time < prevTime.Value)
                        {
                            prevTime = time;
                        }
                    }
                    DateTime start = prevTime.Value;
                    if (start > time) start = time;
                    string display = FormatLogEntry(start, time, entry.activity);
                    displays.Add(display);
                    prevTime = time;
                }
                displays.Reverse(); // Reverse to show newest first
                foreach (string display in displays)
                {
                    lstActivityHistory.Items.Add(display);
                }
            }
        }
        private void RemoveActivityFromLogFile(string logEntryToRemove)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var filteredLines = lines.Where(line => !line.Trim().Equals(logEntryToRemove.Trim(), StringComparison.OrdinalIgnoreCase));
                File.WriteAllLines(logFilePath, filteredLines);
            }
        }
        private void ExportLogToExcel(DateTime fromDate, DateTime toDate, string filePath)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (!File.Exists(logFilePath))
            {
                MessageBox.Show("No activity log found.", "Error");
                return;
            }
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Activity Log");
                worksheet.Cell(1, 1).Value = "Timestamp";
                worksheet.Cell(1, 2).Value = "Activity";
                string[] lines = File.ReadAllLines(logFilePath);
                int rowIndex = 2;
                foreach (string line in lines)
                {
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                    {
                        string timestampStr = line.Substring(1, timestampEndIndex - 1);
                        if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                        {
                            if (timestamp.Date >= fromDate.Date && timestamp.Date <= toDate.Date)
                            {
                                string activity = line.Substring(timestampEndIndex + 2);
                                worksheet.Cell(rowIndex, 1).Value = timestampStr;
                                worksheet.Cell(rowIndex, 2).Value = activity;
                                rowIndex++;
                            }
                        }
                    }
                }
                try
                {
                    workbook.SaveAs(filePath);
                    MessageBox.Show("Activity log exported to Excel successfully!\n" + filePath, "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error exporting to Excel: " + ex.Message, "Error");
                }
            }
        }
        private void ShowLunchPopup()
        {
            if (!isLunchHandled)
            {
                Form lunchForm = new Form
                {
                    Width = 400,
                    Height = 200,
                    Text = "Lunch Time!",
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    BackColor = Color.FromArgb(25, 25, 25),
                    ForeColor = Color.White
                };
                Label lunchLabel = new Label
                {
                    Text = "It's lunch time! Please record your lunch activity.",
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Height = 100,
                    ForeColor = Color.White
                };
                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Dock = DockStyle.Bottom,
                    Height = 35,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                okButton.FlatAppearance.BorderSize = 0;
                lunchForm.Controls.Add(lunchLabel);
                lunchForm.Controls.Add(okButton);
                if (lunchForm.ShowDialog() == DialogResult.OK)
                {
                    isLunchHandled = true;
                    ShowFullScreenInput();
                }
            }
        }
        #endregion
        #region Private Methods
        private void StartTimer()
        {
            popupTimer.Stop();
            popupTimer.Interval = popupIntervalInMinutes * 60 * 1000;
            popupTimer.Tick -= popupTimer_Tick;
            popupTimer.Tick += popupTimer_Tick;
            popupTimer.Start();
        }
        private void LoadLogoImage()
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logoPath))
            {
                using (var original = Image.FromFile(logoPath))
                {
                    logoPictureBox.Image = MakeImageWhite(new Bitmap(original));
                }
            }
            else
            {
                logoPictureBox.Image = null;
            }
        }
        private Bitmap MakeImageWhite(Bitmap original)
        {
            Bitmap bmp = new Bitmap(original.Width, original.Height);
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    if (c.A > 0)
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(c.A, 255, 255, 255));
                    }
                }
            }
            return bmp;
        }
        private Icon MakeIconWhite(Icon original)
        {
            Bitmap bmp = original.ToBitmap();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 0)
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(c.A, 255, 255, 255));
                    }
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }
        #endregion
    }
}

