using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        #region Custom Controls
        public class RoundedPanel : Panel
        {
            public int CornerRadius { get; set; } = 8;
            
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                
                using (var brush = new SolidBrush(this.BackColor))
                using (var path = GetRoundedRectanglePath(this.ClientRectangle, CornerRadius))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
            
            private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                path.CloseAllFigures();
                return path;
            }
        }
        #endregion
        
        #region Fields
        private Label lblTitle = null!;
        private ComboBox cmbType = null!;
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
        private bool dontShowPopupToday = false;
        private const string TypeHint = "Enter type...";
        private const string ActivityHint = "Enter activity...";
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
            // Default to 2 hours if not set
            if (config.IntervalHours <= 0) 
            {
                config.IntervalHours = 2;
                config.Save();
            }
            popupIntervalInMinutes = config.IntervalHours * 60;
            
            // Check if don't show setting should reset (new day)
            if (config.LastDontShowDate.Date != DateTime.Now.Date)
            {
                config.DontShowPopupToday = false;
                config.LastDontShowDate = DateTime.Now.Date;
                config.Save();
            }
            dontShowPopupToday = config.DontShowPopupToday;
        }

        private void InitializeComponent()
        {
            lblTitle = new Label();
            cmbType = new ComboBox();
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
            trayMenu.Items.Add("Test Timer", null, OnTestTimerClicked);
            trayMenu.Items.Add("-");
            var dontShowMenuItem = new ToolStripMenuItem("Don't show popup today");
            dontShowMenuItem.CheckOnClick = true;
            dontShowMenuItem.Checked = config?.DontShowPopupToday ?? false;
            dontShowMenuItem.Click += OnDontShowTodayClicked;
            trayMenu.Items.Add(dontShowMenuItem);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Clear History", null, OnClearHistoryClicked);
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Logo
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Title
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Input row, lebih tinggi untuk modern look
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // History

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

            // Input row: TableLayout for type + activity full width
            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(20, 20, 20),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Fixed for cmbType
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Full for txtActivity
            inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Fixed height 50px

            // Modern ComboBox styling
            var typePanel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Margin = new Padding(10, 5, 5, 5),
                Padding = new Padding(15, 12, 15, 12),
                CornerRadius = 8
            };
            
            cmbType.Font = new Font("Segoe UI", 14, FontStyle.Regular);
            cmbType.BackColor = Color.FromArgb(45, 45, 45);
            cmbType.ForeColor = Color.FromArgb(180, 180, 180);
            cmbType.FlatStyle = FlatStyle.Standard;
            cmbType.DropDownStyle = ComboBoxStyle.DropDown;
            cmbType.AutoCompleteMode = AutoCompleteMode.None;
            cmbType.AutoCompleteSource = AutoCompleteSource.None;
            cmbType.Dock = DockStyle.Fill;
            cmbType.Text = TypeHint;
            cmbType.DropDownHeight = 200;
            
            // Modern focus and hover effects
            cmbType.GotFocus += (s, e) => {
                if (cmbType.Text == TypeHint) { 
                    cmbType.Text = ""; 
                    cmbType.ForeColor = Color.White; 
                }
                typePanel.BackColor = Color.FromArgb(60, 60, 60);
            };
            cmbType.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(cmbType.Text)) { 
                    cmbType.Text = TypeHint; 
                    cmbType.ForeColor = Color.FromArgb(180, 180, 180); 
                }
                typePanel.BackColor = Color.FromArgb(45, 45, 45);
            };
            cmbType.MouseEnter += (s, e) => {
                if (!cmbType.Focused) typePanel.BackColor = Color.FromArgb(55, 55, 55);
            };
            cmbType.MouseLeave += (s, e) => {
                if (!cmbType.Focused) typePanel.BackColor = Color.FromArgb(45, 45, 45);
            };
            cmbType.Click += (s, e) => {
                if (cmbType.Items.Count > 0 && !cmbType.DroppedDown)
                {
                    cmbType.DroppedDown = true;
                }
            };
            cmbType.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Down && cmbType.Items.Count > 0)
                {
                    cmbType.DroppedDown = true;
                }
            };
            cmbType.DropDown += (s, e) => {
                // Refresh items when dropdown opens
                var currentText = cmbType.Text;
                var uniqueTypes = GetUniqueTypesFromLog();
                cmbType.Items.Clear();
                cmbType.Items.AddRange(uniqueTypes.ToArray());
                cmbType.Text = currentText;
            };
            
            typePanel.Controls.Add(cmbType);
            inputPanel.Controls.Add(typePanel, 0, 0);

            // Modern TextBox styling
            var activityPanel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Margin = new Padding(5, 5, 10, 5),
                Padding = new Padding(15, 12, 15, 12),
                CornerRadius = 8
            };
            
            txtActivity.Font = new Font("Segoe UI", 14, FontStyle.Regular);
            txtActivity.BackColor = Color.FromArgb(45, 45, 45);
            txtActivity.ForeColor = Color.FromArgb(180, 180, 180);
            txtActivity.BorderStyle = BorderStyle.None;
            txtActivity.Dock = DockStyle.Fill;
            txtActivity.Text = ActivityHint;
            
            // Modern focus and hover effects
            txtActivity.GotFocus += (s, e) => {
                if (txtActivity.Text == ActivityHint) { 
                    txtActivity.Text = ""; 
                    txtActivity.ForeColor = Color.White; 
                }
                activityPanel.BackColor = Color.FromArgb(60, 60, 60);
            };
            txtActivity.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtActivity.Text)) { 
                    txtActivity.Text = ActivityHint; 
                    txtActivity.ForeColor = Color.FromArgb(180, 180, 180); 
                }
                activityPanel.BackColor = Color.FromArgb(45, 45, 45);
            };
            txtActivity.MouseEnter += (s, e) => {
                if (!txtActivity.Focused) activityPanel.BackColor = Color.FromArgb(55, 55, 55);
            };
            txtActivity.MouseLeave += (s, e) => {
                if (!txtActivity.Focused) activityPanel.BackColor = Color.FromArgb(45, 45, 45);
            };
            
            activityPanel.Controls.Add(txtActivity);
            inputPanel.Controls.Add(activityPanel, 1, 0);

            mainLayout.Controls.Add(inputPanel, 0, 2);

            lstActivityHistory.Font = new Font("Segoe UI", 12);
            lstActivityHistory.BackColor = Color.FromArgb(35, 35, 35);
            lstActivityHistory.ForeColor = Color.White;
            lstActivityHistory.BorderStyle = BorderStyle.None;
            lstActivityHistory.Dock = DockStyle.Fill;
            lstActivityHistory.Margin = new Padding(5);
            lstActivityHistory.DrawMode = DrawMode.OwnerDrawFixed;
            lstActivityHistory.ItemHeight = 25;
            lstActivityHistory.DrawItem += LstActivityHistory_DrawItem;
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

        private void LstActivityHistory_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            
            string item = lstActivityHistory.Items[e.Index].ToString();
            bool isHeader = !item.StartsWith("     ");
            
            e.DrawBackground();
            
            Color textColor = isHeader ? Color.FromArgb(100, 200, 255) : Color.White;
            Font font = isHeader ? new Font("Segoe UI", 12, FontStyle.Bold) : new Font("Segoe UI", 11);
            
            using (SolidBrush brush = new SolidBrush(textColor))
            {
                e.Graphics.DrawString(item, font, brush, e.Bounds);
            }
            
            e.DrawFocusRectangle();
        }
        
        private void LstActivityHistory_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (lstActivityHistory.SelectedItem != null)
            {
                string selectedItemText = lstActivityHistory.SelectedItem!.ToString();
                
                // Check if it's a header (type group)
                if (!selectedItemText.StartsWith("     "))
                {
                    // Extract type from header: [date | duration] TYPE
                    int closingBracketIndex = selectedItemText.IndexOf(']');
                    if (closingBracketIndex != -1)
                    {
                        string type = selectedItemText.Substring(closingBracketIndex + 1).Trim();
                        
                        cmbType.Text = type;
                        cmbType.ForeColor = Color.White;
                        // Clear activity when clicking header
                        txtActivity.Text = ActivityHint;
                        txtActivity.ForeColor = Color.FromArgb(180, 180, 180);
                    }
                }
                else
                {
                    // Sub-item: extract activity only
                    int closingBracketIndex = selectedItemText.IndexOf(']');
                    if (closingBracketIndex != -1)
                    {
                        string activity = selectedItemText.Substring(closingBracketIndex + 1).Trim();
                        txtActivity.Text = activity;
                        txtActivity.ForeColor = Color.White;
                    }
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
            using (var setIntervalForm = new SetIntervalDialog(config.IntervalHours * 60)) // Convert hours to minutes
            {
                if (setIntervalForm.ShowDialog() == DialogResult.OK)
                {
                    config.IntervalHours = setIntervalForm.IntervalMinutes / 60; // Convert minutes to hours
                    if (config.IntervalHours < 1) config.IntervalHours = 1; // Minimum 1 hour
                    config.Save();
                    LoadConfig();
                    StartTimer(); // refresh interval
                }
            }
        }
        
        private void OnClearHistoryClicked(object? sender, EventArgs e)
        {
            var result = ShowDarkMessageBox("Are you sure you want to clear all activity history?\nThis action cannot be undone.", 
                "Clear History", MessageBoxButtons.YesNo);
            
            if (result == DialogResult.Yes)
            {
                string logFilePath = GetLogFilePath();
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
                LoadLogHistory();
                ShowDarkMessageBox("History cleared successfully!", "Success");
            }
        }
        
        private void OnTestTimerClicked(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastPopup = now - popupTime;
            
            string info = $"Timer Status:\n" +
                         $"Current Time: {now:HH:mm:ss}\n" +
                         $"Last Popup: {popupTime:HH:mm:ss}\n" +
                         $"Time Since Last: {(int)timeSinceLastPopup.TotalMinutes} minutes\n" +
                         $"Interval Setting: {config.IntervalHours} hours ({popupIntervalInMinutes} minutes)\n" +
                         $"Timer Running: {popupTimer.Enabled}\n" +
                         $"Don't Show Today: {dontShowPopupToday}\n" +
                         $"Next Popup In: {(dontShowPopupToday ? "Disabled" : Math.Max(0, popupIntervalInMinutes - (int)timeSinceLastPopup.TotalMinutes).ToString() + " minutes")}";
            
            ShowDarkMessageBox(info, "Timer Debug Info");
        }
        
        private void OnDontShowTodayClicked(object? sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                dontShowPopupToday = menuItem.Checked;
                config.DontShowPopupToday = dontShowPopupToday;
                config.LastDontShowDate = DateTime.Now.Date;
                config.Save();
                
                string status = dontShowPopupToday ? "disabled" : "enabled";
                trayIcon.ShowBalloonTip(2000, "Popup Status", $"Activity popups {status} for today", ToolTipIcon.Info);
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
                    if (parts.Length == 2) // Untuk sub-item [time range | dur]
                    {
                        string timesStr = parts[0];
                        string[] timeParts = timesStr.Split('-').Select(t => t.Trim()).ToArray();
                        if (timeParts.Length == 2)
                        {
                            string dateStr = lstActivityHistory.Items.Cast<string>().FirstOrDefault(i => i.StartsWith("[") && !i.StartsWith("     "))?.Substring(1, 10); // Ambil date dari header
                            if (dateStr != null)
                            {
                                string endStr = timeParts[1];
                                if (DateTime.TryParseExact($"{dateStr} {endStr}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                                {
                                    string fullText = selectedItem.Substring(closingBracketIndex + 1).Trim();
                                    int colonIndex = fullText.IndexOf(':');
                                    string type = colonIndex > 0 ? fullText.Substring(0, colonIndex).Trim() : "";
                                    string activity = colonIndex > 0 ? fullText.Substring(colonIndex + 1).Trim() : fullText;
                                    string logEntry = $"[{timestamp.ToString(CultureInfo.InvariantCulture)}] {type} | {activity}";
                                    cmbType.Text = type;
                                    txtActivity.Text = activity;
                                    lstActivityHistory.Items.Remove(selectedItem);
                                    RemoveActivityFromLogFile(logEntry);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void popupTimer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            
            // Check if don't show setting should reset (new day)
            if (config.LastDontShowDate.Date != now.Date)
            {
                config.DontShowPopupToday = false;
                config.LastDontShowDate = now.Date;
                config.Save();
                dontShowPopupToday = false;
                
                // Update tray menu checkbox
                var dontShowMenuItem = trayMenu.Items.Cast<ToolStripItem>().FirstOrDefault(x => x.Text == "Don't show popup today") as ToolStripMenuItem;
                if (dontShowMenuItem != null)
                    dontShowMenuItem.Checked = false;
            }
            
            // Don't show popups if user disabled them for today
            if (dontShowPopupToday) return;
            
            // Show popups 24/7 now (removed working hours restriction)
            // Lunch popup logic
            if (!isLunchPopupShown && now.Hour == 12 && now.Minute >= 0 && now.Minute <= 15)
            {
                ShowLunchPopup();
                isLunchPopupShown = true;
                isLunchHandled = false;
            }
            
            // Activity reminder logic
            TimeSpan timeSinceLastPopup = now - popupTime;
            if (timeSinceLastPopup.TotalMinutes >= popupIntervalInMinutes)
            {
                ShowFullScreenInput();
                popupTime = DateTime.Now;
            }
            
            // Reset lunch popup for next day
            if (now.Hour > 13)
                isLunchPopupShown = false;
        }

        private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e) => SaveActivity();
        #endregion

        #region Methods
        private void ShowFullScreenInput()
        {
            if ((DateTime.Now - lastActivityInputTime).TotalSeconds < 60 && lastActivityInputTime != DateTime.MinValue)
            {
                this.Hide();
                return;
            }
            
            // Refresh dropdown items from log
            cmbType.Items.Clear();
            var uniqueTypes = GetUniqueTypesFromLog();
            cmbType.Items.AddRange(uniqueTypes.ToArray());
            
            // Reset form
            cmbType.Text = TypeHint;
            cmbType.ForeColor = Color.FromArgb(180, 180, 180);
            txtActivity.Text = ActivityHint;
            txtActivity.ForeColor = Color.FromArgb(180, 180, 180);
            
            txtActivity.Focus();
            this.WindowState = FormWindowState.Maximized;
            this.Show();
            this.BringToFront();
            this.Activate();
        }

        private List<string> GetUniqueTypesFromLog()
        {
            string logFilePath = GetLogFilePath();
            if (!File.Exists(logFilePath)) return new List<string>();

            try
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    // Format: [timestamp] type | activity
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                    {
                        string rest = line.Substring(timestampEndIndex + 2).Trim();
                        int pipeIndex = rest.IndexOf('|');
                        if (pipeIndex > 0)
                        {
                            string type = rest.Substring(0, pipeIndex).Trim();
                            if (!string.IsNullOrEmpty(type))
                            {
                                types.Add(type);
                            }
                        }
                    }
                }
                return types.OrderBy(t => t).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void SaveActivity()
        {
            string type = cmbType.Text.Trim();
            string activity = txtActivity.Text.Trim();
            
            if (type == TypeHint) type = "";
            if (activity == ActivityHint) activity = "";
            
            if (!string.IsNullOrEmpty(activity))
            {
                // Add new type to dropdown if not exists
                if (!string.IsNullOrEmpty(type) && !cmbType.Items.Contains(type))
                {
                    cmbType.Items.Add(type);
                }
                
                string typePart = string.IsNullOrEmpty(type) ? "" : $"{type} | ";
                string logEntry = $"[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}] {typePart}{activity}";
                string logFilePath = GetLogFilePath();
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
                LoadLogHistory();
                lastActivityInputTime = DateTime.Now;
            }
            this.Hide();
        }

        private void LoadLogHistory()
        {
            lstActivityHistory.Items.Clear();
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var entries = new List<(DateTime time, string type, string activity)>();
                foreach (string line in lines)
                {
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                    {
                        string timestampStr = line.Substring(1, timestampEndIndex - 1);
                        if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                        {
                            string rest = line.Substring(timestampEndIndex + 2).Trim();
                            string type = "";
                            string activity = rest;
                            int pipeIndex = rest.IndexOf('|');
                            if (pipeIndex > 0)
                            {
                                type = rest.Substring(0, pipeIndex).Trim();
                                activity = rest.Substring(pipeIndex + 1).Trim();
                            }
                            entries.Add((time, type, activity));
                        }
                    }
                }
                entries.Sort((a, b) => a.time.CompareTo(b.time)); // Sort ascending

                // Group by date
                var dateGroups = entries.GroupBy(e => e.time.Date).OrderByDescending(g => g.Key); // Newest first

                foreach (var dateGroup in dateGroups)
                {
                    DateTime date = dateGroup.Key;
                    string dateStr = date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

                    // Group by type
                    var typeGroups = dateGroup.GroupBy(e => e.type, StringComparer.OrdinalIgnoreCase);

                    foreach (var typeGroup in typeGroups)
                    {
                        string type = typeGroup.Key;
                        var typeEntries = typeGroup.ToList();

                        // Calculate segments for this type
                        var segments = new List<(DateTime start, DateTime end, int dur, string activity)>();
                        DateTime? prevTime = new DateTime(date.Year, date.Month, date.Day, 8, 0, 0);
                        if (typeEntries.First().time < prevTime.Value)
                        {
                            prevTime = typeEntries.First().time;
                        }

                        foreach (var entry in typeEntries)
                        {
                            DateTime start = prevTime.Value;
                            if (start > entry.time) start = entry.time;
                            DateTime end = entry.time;
                            int dur = (int)(end - start).TotalMinutes;
                            if (dur < 0) dur = 0;
                            segments.Add((start, end, dur, entry.activity));
                            prevTime = end;
                        }

                        int totalDur = segments.Sum(s => s.dur);

                        // Header - hanya type saja
                        string header = $"[{dateStr} | {totalDur} minutes] {type}";
                        lstActivityHistory.Items.Add(header);

                        // Sub-items
                        foreach (var seg in segments)
                        {
                            string startStr = seg.start.ToString("HH:mm", CultureInfo.InvariantCulture);
                            string endStr = seg.end.ToString("HH:mm", CultureInfo.InvariantCulture);
                            string sub = $"     [{startStr} - {endStr} | {seg.dur} minutes] {seg.activity}";
                            lstActivityHistory.Items.Add(sub);
                        }
                    }
                }
            }
        }

        private void RemoveActivityFromLogFile(string logEntryToRemove)
        {
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var filteredLines = lines.Where(line => !line.Trim().Equals(logEntryToRemove.Trim(), StringComparison.OrdinalIgnoreCase));
                File.WriteAllLines(logFilePath, filteredLines);
            }
        }

        private void ExportLogToExcel(DateTime fromDate, DateTime toDate, string filePath)
        {
            string logFilePath = GetLogFilePath();
            if (!File.Exists(logFilePath))
            {
                MessageBox.Show("No activity log found.", "Error");
                return;
            }
            
            using (var workbook = new XLWorkbook())
            {
                // Parse all entries
                var entries = new List<(DateTime timestamp, string type, string activity)>();
                string[] lines = File.ReadAllLines(logFilePath);
                
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
                                string rest = line.Substring(timestampEndIndex + 2).Trim();
                                string type = "General";
                                string activity = rest;
                                int pipeIndex = rest.IndexOf('|');
                                if (pipeIndex > 0)
                                {
                                    type = rest.Substring(0, pipeIndex).Trim();
                                    activity = rest.Substring(pipeIndex + 1).Trim();
                                }
                                entries.Add((timestamp, type, activity));
                            }
                        }
                    }
                }
                
                entries.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
                
                // Calculate segments like in LoadLogHistory
                var allSegments = new List<(DateTime date, DateTime start, DateTime end, int duration, string type, string activity, bool isOvertime)>();
                var summaryData = new Dictionary<string, (int totalMinutes, int activityCount)>();
                
                // Group by date
                var dateGroups = entries.GroupBy(e => e.timestamp.Date).OrderBy(g => g.Key);
                
                foreach (var dateGroup in dateGroups)
                {
                    DateTime date = dateGroup.Key;
                    
                    // Group by type
                    var typeGroups = dateGroup.GroupBy(e => e.type, StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var typeGroup in typeGroups)
                    {
                        string type = typeGroup.Key;
                        var typeEntries = typeGroup.ToList();
                        
                        // Calculate segments for this type (same logic as LoadLogHistory)
                        DateTime? prevTime = new DateTime(date.Year, date.Month, date.Day, 8, 0, 0);
                        if (typeEntries.First().timestamp < prevTime.Value)
                        {
                            prevTime = typeEntries.First().timestamp;
                        }
                        
                        foreach (var entry in typeEntries)
                        {
                            DateTime start = prevTime.Value;
                            if (start > entry.timestamp) start = entry.timestamp;
                            DateTime end = entry.timestamp;
                            int duration = (int)(end - start).TotalMinutes;
                            if (duration < 0) duration = 0;
                            
                            // Check if overtime: weekend or after 8 PM
                            bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                            bool isAfter8PM = end.Hour >= 20;
                            bool isOvertime = isWeekend || isAfter8PM;
                            
                            allSegments.Add((date, start, end, duration, type, entry.activity, isOvertime));
                            
                            // Update summary
                            if (!summaryData.ContainsKey(type))
                                summaryData[type] = (0, 0);
                            summaryData[type] = (summaryData[type].totalMinutes + duration, summaryData[type].activityCount + 1);
                            
                            prevTime = end;
                        }
                    }
                }
                
                // Sheet 1: Detailed Log
                var detailSheet = workbook.Worksheets.Add("Detailed Log");
                detailSheet.Cell(1, 1).Value = "Date";
                detailSheet.Cell(1, 2).Value = "Start";
                detailSheet.Cell(1, 3).Value = "End";
                detailSheet.Cell(1, 4).Value = "Duration (minutes)";
                detailSheet.Cell(1, 5).Value = "Type";
                detailSheet.Cell(1, 6).Value = "Activity";
                
                int detailRow = 2;
                foreach (var segment in allSegments.OrderBy(s => s.date).ThenBy(s => s.start))
                {
                    detailSheet.Cell(detailRow, 1).Value = segment.date.ToString("dd/MM/yyyy");
                    detailSheet.Cell(detailRow, 2).Value = segment.start.ToString("HH:mm");
                    detailSheet.Cell(detailRow, 3).Value = segment.end.ToString("HH:mm");
                    detailSheet.Cell(detailRow, 4).Value = segment.duration;
                    detailSheet.Cell(detailRow, 5).Value = segment.type;
                    detailSheet.Cell(detailRow, 6).Value = segment.activity;
                    detailRow++;
                }
                
                // Sheet 2: Summary by Type
                var summarySheet = workbook.Worksheets.Add("Summary by Type");
                summarySheet.Cell(1, 1).Value = "Type";
                summarySheet.Cell(1, 2).Value = "Duration (minutes)";
                summarySheet.Cell(1, 3).Value = "Activity Count";
                
                int summaryRow = 2;
                foreach (var summary in summaryData.OrderBy(s => s.Key))
                {
                    summarySheet.Cell(summaryRow, 1).Value = summary.Key;
                    summarySheet.Cell(summaryRow, 2).Value = summary.Value.totalMinutes;
                    summarySheet.Cell(summaryRow, 3).Value = summary.Value.activityCount;
                    summaryRow++;
                }
                
                // Sheet 3: Overtime
                var overtimeSheet = workbook.Worksheets.Add("Overtime");
                overtimeSheet.Cell(1, 1).Value = "Date";
                overtimeSheet.Cell(1, 2).Value = "Start";
                overtimeSheet.Cell(1, 3).Value = "End";
                overtimeSheet.Cell(1, 4).Value = "Duration (minutes)";
                overtimeSheet.Cell(1, 5).Value = "Type";
                overtimeSheet.Cell(1, 6).Value = "Activity";
                overtimeSheet.Cell(1, 7).Value = "Overtime Reason";
                
                int overtimeRow = 2;
                var overtimeSegments = allSegments.Where(s => s.isOvertime).OrderBy(s => s.date).ThenBy(s => s.start);
                
                foreach (var segment in overtimeSegments)
                {
                    bool isWeekend = segment.date.DayOfWeek == DayOfWeek.Saturday || segment.date.DayOfWeek == DayOfWeek.Sunday;
                    bool isAfter8PM = segment.end.Hour >= 20;
                    
                    string reason = "";
                    if (isWeekend && isAfter8PM) reason = "Weekend + After 8PM";
                    else if (isWeekend) reason = "Weekend";
                    else if (isAfter8PM) reason = "After 8PM";
                    
                    overtimeSheet.Cell(overtimeRow, 1).Value = segment.date.ToString("dd/MM/yyyy");
                    overtimeSheet.Cell(overtimeRow, 2).Value = segment.start.ToString("HH:mm");
                    overtimeSheet.Cell(overtimeRow, 3).Value = segment.end.ToString("HH:mm");
                    overtimeSheet.Cell(overtimeRow, 4).Value = segment.duration;
                    overtimeSheet.Cell(overtimeRow, 5).Value = segment.type;
                    overtimeSheet.Cell(overtimeRow, 6).Value = segment.activity;
                    overtimeSheet.Cell(overtimeRow, 7).Value = reason;
                    overtimeRow++;
                }
                
                // Auto-fit columns
                detailSheet.Columns().AdjustToContents();
                summarySheet.Columns().AdjustToContents();
                overtimeSheet.Columns().AdjustToContents();
                
                try
                {
                    workbook.SaveAs(filePath);
                    int overtimeCount = overtimeSegments.Count();
                    MessageBox.Show($"Excel exported successfully!\n\nDetailed: {allSegments.Count} activities\nSummary: {summaryData.Count} types\nOvertime: {overtimeCount} activities\n\n{filePath}", "Export Complete");
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
        private string GetLogFilePath()
        {
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdinersDailyActivity");
            Directory.CreateDirectory(appDataDir);
            return Path.Combine(appDataDir, "activity_log.txt");
        }

        private void StartTimer()
        {
            popupTimer.Stop();
            // Timer runs 24/7, checking every minute
            popupTimer.Interval = 60 * 1000; // 1 minute
            popupTimer.Tick -= popupTimer_Tick;
            popupTimer.Tick += popupTimer_Tick;
            popupTimer.Start();
            
            // Show current interval setting
            string status = dontShowPopupToday ? "(disabled today)" : "";
            trayIcon.ShowBalloonTip(3000, "Timer Started", 
                $"24/7 timer active - Reminders every {config.IntervalHours} hours {status}", ToolTipIcon.Info);
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

        private DialogResult ShowDarkMessageBox(string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            Form darkForm = new Form
            {
                Width = 400,
                Height = buttons == MessageBoxButtons.YesNo ? 180 : 150,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            
            Label messageLabel = new Label
            {
                Text = message,
                AutoSize = false,
                Size = new Size(360, 80),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            darkForm.Controls.Add(messageLabel);
            
            if (buttons == MessageBoxButtons.YesNo)
            {
                Button yesButton = new Button
                {
                    Text = "Yes",
                    DialogResult = DialogResult.Yes,
                    Size = new Size(80, 30),
                    Location = new Point(220, 110),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                yesButton.FlatAppearance.BorderSize = 0;
                
                Button noButton = new Button
                {
                    Text = "No",
                    DialogResult = DialogResult.No,
                    Size = new Size(80, 30),
                    Location = new Point(310, 110),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                noButton.FlatAppearance.BorderSize = 0;
                
                darkForm.Controls.Add(yesButton);
                darkForm.Controls.Add(noButton);
            }
            else
            {
                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(80, 30),
                    Location = new Point(310, 80),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                okButton.FlatAppearance.BorderSize = 0;
                darkForm.Controls.Add(okButton);
            }
            
            return darkForm.ShowDialog();
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