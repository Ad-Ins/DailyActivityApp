using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ClosedXML.Excel;
using AdinersDailyActivityApp.Dialog;
using AdinersDailyActivityApp.Dialogs;
using AdinersDailyActivityApp.Services;

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
        private System.Windows.Forms.Timer displayTimer = null!;
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip trayMenu = null!;
        private ContextMenuStrip historyContextMenu = null!;
        private AppConfig config = null!;
        
        // Clockify-style timer fields
        private Button btnStartStop = null!;
        private bool isTimerRunning = false;
        private DateTime timerStartTime;
        private string currentActivityType = "";
        private string currentActivityDescription = "";
        private TimeSpan elapsedTime = TimeSpan.Zero;
        
        // Legacy variables for compatibility
        private DateTime lastActivityInputTime = DateTime.MinValue;
        private DateTime popupTime = DateTime.Now;
        private int popupIntervalInMinutes = 60;
        private bool dontShowPopupToday = false;
        private System.Windows.Forms.Timer popupTimer = null!;
        private bool isLunchHandled = false;
        
        // Exclude times functionality
        private List<(TimeSpan start, TimeSpan end, string name)> excludeTimes = new();
        private bool isTimerPausedForExclude = false;
        private string pausedActivityType = "";
        private string pausedActivityDescription = "";
        private DateTime pausedTimerStartTime;
        
        // History expand/collapse functionality
        private HashSet<string> expandedHeaders = new();
        
        private const string TypeHint = "Enter type...";
        private const string ActivityHint = "Enter activity...";
        #endregion

        #region Constructor
        public DailyActivityForm(DateTime appStartTime, DateTime popupTime)
        {
            InitializeComponent();
            SetupForm();
            LoadConfig();
            StartDisplayTimer();
            LoadLogHistory();
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            
            // Check for updates on startup (async)
            _ = Task.Run(CheckForUpdatesOnStartup);
        }
        #endregion

        #region Initialization
        private void LoadConfig()
        {
            config = AppConfig.Load();
            LoadExcludeTimes();
        }

        private void InitializeComponent()
        {
            lblTitle = new Label();
            cmbType = new ComboBox();
            txtActivity = new TextBox();
            lstActivityHistory = new ListBox();
            logoPictureBox = new PictureBox();
            displayTimer = new System.Windows.Forms.Timer();
            popupTimer = new System.Windows.Forms.Timer();
            btnStartStop = new Button();
            // Tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(30, 30, 30);
            trayMenu.ForeColor = Color.White;
            trayMenu.Items.Add("Input Activity Now", null, OnInputNowClicked);
            trayMenu.Items.Add("Stop Timer", null, OnStopTimerClicked);
            trayMenu.Items.Add("Export Log to Excel", null, OnExportLogClicked);
            trayMenu.Items.Add("Set Interval...", null, OnSetIntervalClicked);
            trayMenu.Items.Add("Exclude Times...", null, OnExcludeTimesClicked);
            trayMenu.Items.Add("Timer Information", null, OnTestTimerClicked);
            trayMenu.Items.Add("-");
            var dontShowMenuItem = new ToolStripMenuItem("Don't show popup today");
            dontShowMenuItem.CheckOnClick = true;
            dontShowMenuItem.Checked = config?.DontShowPopupToday ?? false;
            dontShowMenuItem.Click += OnDontShowTodayClicked;
            trayMenu.Items.Add(dontShowMenuItem);
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Check for Updates", null, OnCheckUpdatesClicked);
            // trayMenu.Items.Add("Clear History", null, OnClearHistoryClicked); // Temporarily hidden
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("About", null, OnAboutClicked);
            trayMenu.Items.Add("Exit", null, OnExitClicked);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            Icon trayAppIcon;
            
            try
            {
                if (File.Exists(iconPath))
                {
                    trayAppIcon = new Icon(iconPath);
                }
                else
                {
                    trayAppIcon = SystemIcons.Application;
                }
            }
            catch
            {
                // Fallback to system icon if there's any error
                trayAppIcon = SystemIcons.Application;
            }

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
            historyContextMenu.Items.Add("Edit Activity (F2)", null, OnEditHistoryClicked);
            historyContextMenu.Items.Add("Delete Activity (F3)", null, OnDeleteHistoryClicked);
            lstActivityHistory.ContextMenuStrip = historyContextMenu;
            lstActivityHistory.KeyDown += LstActivityHistory_KeyDown;
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

            // Input row: TableLayout for type + activity + button
            var inputPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(20, 20, 20),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Fixed for cmbType
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Full for txtActivity
            inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Fixed for button
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
            
            // START/STOP Button
            btnStartStop.Text = "START";
            btnStartStop.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnStartStop.BackColor = Color.FromArgb(0, 120, 215); // Blue for START
            btnStartStop.ForeColor = Color.White;
            btnStartStop.FlatStyle = FlatStyle.Flat;
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Dock = DockStyle.Fill;
            btnStartStop.Margin = new Padding(5, 5, 10, 5);
            btnStartStop.Click += OnStartStopClicked;
            inputPanel.Controls.Add(btnStartStop, 2, 0);

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
            if (e.KeyCode == Keys.F2)
            {
                OnEditHistoryClicked(sender, e);
                e.Handled = true;
            }
            if (e.KeyCode == Keys.F3)
            {
                OnDeleteHistoryClicked(sender, e);
                e.Handled = true;
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
        
        private void LstActivityHistory_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                OnEditHistoryClicked(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                OnDeleteHistoryClicked(sender, e);
                e.Handled = true;
            }
        }
        
        private void LstActivityHistory_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (lstActivityHistory.SelectedItem != null)
            {
                string selectedItemText = lstActivityHistory.SelectedItem.ToString() ?? "";
                
                // Check if it's a header (type group)
                if (!selectedItemText.StartsWith("     "))
                {
                    // Toggle expand/collapse for header
                    string headerKey = selectedItemText.Substring(2); // Remove icon (▶ or ▼)
                    if (expandedHeaders.Contains(headerKey))
                    {
                        expandedHeaders.Remove(headerKey);
                    }
                    else
                    {
                        expandedHeaders.Add(headerKey);
                    }
                    LoadLogHistory(); // Refresh display
                }
                else
                {
                    // Sub-item: extract activity and find type from header
                    int closingBracketIndex = selectedItemText.IndexOf(']');
                    if (closingBracketIndex != -1)
                    {
                        string activity = selectedItemText.Substring(closingBracketIndex + 1).Trim();
                        
                        // Find the header (type) for this sub-item
                        int selectedIndex = lstActivityHistory.SelectedIndex;
                        string type = "";
                        
                        // Look backwards to find the header
                        for (int i = selectedIndex - 1; i >= 0; i--)
                        {
                            string item = lstActivityHistory.Items[i].ToString() ?? "";
                            if (!item.StartsWith("     ")) // Found header
                            {
                                int headerBracketIndex = item.IndexOf(']');
                                if (headerBracketIndex != -1)
                                {
                                    type = item.Substring(headerBracketIndex + 1).Trim();
                                }
                                break;
                            }
                        }
                        
                        cmbType.Text = type;
                        cmbType.ForeColor = Color.White;
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
        
        private void OnStopTimerClicked(object? sender, EventArgs e)
        {
            if (isTimerRunning || isTimerPausedForExclude)
            {
                StopTimer();
            }
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
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        saveFileDialog.FileName = $"activity_log_{timestamp}.xlsx";
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
                    StartDisplayTimer(); // refresh interval
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
        
        private void OnRefreshIconClicked(object? sender, EventArgs e)
        {
            RefreshTrayIcon();
            trayIcon.ShowBalloonTip(2000, "Icon Refreshed", "Tray icon has been refreshed from logo.ico", ToolTipIcon.Info);
        }
        
        private void OnTestTimerClicked(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastPopup = now - popupTime;
            int minutesSinceLastPopup = (int)timeSinceLastPopup.TotalMinutes;
            int minutesUntilNext = Math.Max(0, popupIntervalInMinutes - minutesSinceLastPopup);
            DateTime nextPopupTime = popupTime.AddMinutes(popupIntervalInMinutes);
            
            string info = $"Timer Status:\n" +
                         $"Current Time: {now:HH:mm:ss}\n" +
                         $"Next Popup Time: {(dontShowPopupToday ? "Disabled" : nextPopupTime.ToString("HH:mm:ss"))}\n" +
                         $"Minutes Until Next: {(dontShowPopupToday ? "Disabled" : minutesUntilNext.ToString())}\n" +
                         $"Interval Setting: {config.IntervalHours} hours ({popupIntervalInMinutes} minutes)\n" +
                         $"Timer Running: {popupTimer.Enabled}\n" +
                         $"Don't Show Today: {dontShowPopupToday}\n" +
                         $"Last Popup: {popupTime:HH:mm:ss} ({minutesSinceLastPopup} min ago)";
            
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
        
        private void OnCheckUpdatesClicked(object? sender, EventArgs e)
        {
            trayIcon.ShowBalloonTip(2000, "Checking Updates", "Checking for updates...", ToolTipIcon.Info);
            
            // Run async without blocking
            _ = Task.Run(async () =>
            {
                var hasUpdate = await UpdateService.CheckForUpdatesAsync();
                if (hasUpdate)
                {
                    var release = await UpdateService.GetLatestReleaseAsync();
                    if (release != null)
                    {
                        this.Invoke(() => ShowUpdateDialog(release));
                    }
                }
                else
                {
                    this.Invoke(() => ShowNoUpdateDialog());
                }
            });
        }
        
        private async Task CheckForUpdatesOnStartup()
        {
            if (!config.CheckForUpdates) return;
            
            // Check once per day
            if ((DateTime.Now - config.LastUpdateCheck).TotalHours < 24) return;
            
            var hasUpdate = await UpdateService.CheckForUpdatesAsync();
            if (hasUpdate)
            {
                var release = await UpdateService.GetLatestReleaseAsync();
                if (release != null)
                {
                    this.Invoke(() => ShowUpdateDialog(release));
                }
            }
            
            config.LastUpdateCheck = DateTime.Now;
            config.Save();
        }
        
        private void ShowUpdateDialog(GitHubRelease release)
        {
            var updateDialog = new UpdateDialog(release);
            updateDialog.Show(); // Non-blocking
        }
        
        private void ShowNoUpdateDialog()
        {
            Form noUpdateForm = new Form
            {
                Width = 400,
                Height = 120,
                Text = "No Updates Available",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                TopMost = false
            };
            
            Label messageLabel = new Label
            {
                Text = $"You are using the latest version ({UpdateService.CurrentVersion})",
                Location = new Point(20, 20),
                Size = new Size(340, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            noUpdateForm.Controls.Add(messageLabel);
            
            // Auto-close after 3 seconds
            var autoCloseTimer = new System.Windows.Forms.Timer();
            autoCloseTimer.Interval = 3000;
            autoCloseTimer.Tick += (s, e) => {
                autoCloseTimer.Stop();
                noUpdateForm.Close();
            };
            autoCloseTimer.Start();
            
            noUpdateForm.Show();
        }
        
        private void OnAboutClicked(object? sender, EventArgs e)
        {
            Form aboutForm = new Form
            {
                Width = 480,
                Height = 420,
                Text = "About Daily Activity App",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            
            Label titleLabel = new Label
            {
                Text = "Daily Activity Tracker",
                Location = new Point(20, 20),
                Size = new Size(420, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            
            Label versionLabel = new Label
            {
                Text = $"Version {UpdateService.CurrentVersion}",
                Location = new Point(20, 55),
                Size = new Size(420, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            Label descriptionLabel = new Label
            {
                Text = "A comprehensive activity tracking application designed to help\n" +
                       "professionals monitor and manage their daily work activities.\n\n" +
                       "Never miss tracking your activities again! This tool provides\n" +
                       "automatic reminders, detailed logging, and comprehensive\n" +
                       "reporting to ensure all your work is properly documented.",
                Location = new Point(20, 85),
                Size = new Size(420, 120),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            Label featuresLabel = new Label
            {
                Text = "Key Features:\n" +
                       "• Manual timer with START/STOP button\n" +
                       "• Automatic midnight activity splitting\n" +
                       "• Exclude time periods (lunch, breaks)\n" +
                       "• On-the-fly activity editing (F2)\n" +
                       "• Delete activities with F3 key\n" +
                       "• Smart activity type management\n" +
                       "• Excel export with detailed reports\n" +
                       "• Overtime tracking and analysis\n" +
                       "• Auto-update system",
                Location = new Point(20, 210),
                Size = new Size(200, 140),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 9)
            };
            
            Label copyrightLabel = new Label
            {
                Text = "© 2024 AdIns (Advance Innovations) - PT. Adicipta Inovasi Teknologi\nDeveloped by LJP",
                Location = new Point(240, 330),
                Size = new Size(200, 40),
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.TopRight
            };
            
            Button btnClose = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                Location = new Point(380, 370),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            btnClose.FlatAppearance.BorderSize = 0;
            
            aboutForm.Controls.AddRange(new Control[] { 
                titleLabel, versionLabel, descriptionLabel, featuresLabel, 
                copyrightLabel, btnClose 
            });
            
            aboutForm.ShowDialog();
        }

        private void OnEditHistoryClicked(object? sender, EventArgs e)
        {
            // If no selection, try to use the first sub-item (activity) in the list
            if (lstActivityHistory.SelectedItem == null)
            {
                // Find first sub-item (activity) in the list
                for (int i = 0; i < lstActivityHistory.Items.Count; i++)
                {
                    string item = lstActivityHistory.Items[i].ToString();
                    if (item.StartsWith("     ")) // This is a sub-item (activity)
                    {
                        lstActivityHistory.SelectedIndex = i;
                        break;
                    }
                }
                
                if (lstActivityHistory.SelectedItem == null)
                {
                    ShowDarkMessageBox("No activities found to edit.", "No Activities");
                    return;
                }
            }

            string selectedItem = lstActivityHistory.SelectedItem.ToString();
            int selectedIndex = lstActivityHistory.SelectedIndex;
            
            // Only allow editing sub-items (activities), not headers
            if (!selectedItem.StartsWith("     "))
            {
                ShowDarkMessageBox("Please select an activity (not a header) to edit.", "Invalid Selection");
                return;
            }

            // Parse sub-item: "     [HH:mm - HH:mm | duration] activity"
            int closingBracketIndex = selectedItem.IndexOf(']');
            if (closingBracketIndex == -1) return;

            string inside = selectedItem.Substring(6, closingBracketIndex - 6); // Skip "     ["
            string[] parts = inside.Split('|');
            if (parts.Length != 2) return;

            string timesStr = parts[0].Trim();
            string[] timeParts = timesStr.Split('-');
            if (timeParts.Length != 2) return;

            string startStr = timeParts[0].Trim();
            string endStr = timeParts[1].Trim();
            string activity = selectedItem.Substring(closingBracketIndex + 1).Trim();

            // Find date and type from header
            string dateStr = "";
            string type = "";
            for (int i = selectedIndex - 1; i >= 0; i--)
            {
                string item = lstActivityHistory.Items[i].ToString();
                if (!item.StartsWith("     ")) // Found header
                {
                    // Remove expand icon and parse header: ▶ [date | start-end | duration] type
                    string headerWithoutIcon = item.Substring(2); // Remove icon
                    int headerBracketIndex = headerWithoutIcon.IndexOf(']');
                    if (headerBracketIndex != -1)
                    {
                        string headerInside = headerWithoutIcon.Substring(1, headerBracketIndex - 1);
                        string[] headerParts = headerInside.Split('|');
                        if (headerParts.Length == 3) // date | start-end | duration
                        {
                            dateStr = headerParts[0].Trim();
                            type = headerWithoutIcon.Substring(headerBracketIndex + 1).Trim();
                        }
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(dateStr)) return;

            // Parse times
            if (!DateTime.TryParseExact($"{dateStr} {startStr}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startTime) ||
                !DateTime.TryParseExact($"{dateStr} {endStr}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endTime))
                return;

            // Create original log entry for removal - ensure exact format match
            string originalLogEntry = string.IsNullOrEmpty(type) ? 
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {activity}" :
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {type} | {activity}";

            // Open edit dialog
            using (var editDialog = new EditActivityDialog(startTime, endTime, type, activity))
            {
                if (editDialog.ShowDialog() == DialogResult.OK)
                {
                    // Remove original entry
                    RemoveActivityFromLogFile(originalLogEntry);
                    
                    // Add new entry with consistent format
                    string newTypePart = string.IsNullOrEmpty(editDialog.ActivityType) ? "" : $"{editDialog.ActivityType} | ";
                    string newLogEntry = $"[{editDialog.EndTime.ToString(CultureInfo.InvariantCulture)}] {newTypePart}{editDialog.ActivityText}";
                    string logFilePath = GetLogFilePath();
                    File.AppendAllText(logFilePath, newLogEntry + Environment.NewLine);
                    
                    // Refresh display
                    LoadLogHistory();
                    
                    trayIcon.ShowBalloonTip(2000, "Activity Updated", "Activity has been successfully updated.", ToolTipIcon.Info);
                }
            }
        }
        
        private void OnDeleteHistoryClicked(object? sender, EventArgs e)
        {
            // If no selection, try to use the first sub-item (activity) in the list
            if (lstActivityHistory.SelectedItem == null)
            {
                // Find first sub-item (activity) in the list
                for (int i = 0; i < lstActivityHistory.Items.Count; i++)
                {
                    string item = lstActivityHistory.Items[i].ToString();
                    if (item.StartsWith("     ")) // This is a sub-item (activity)
                    {
                        lstActivityHistory.SelectedIndex = i;
                        break;
                    }
                }
                
                if (lstActivityHistory.SelectedItem == null)
                {
                    ShowDarkMessageBox("No activities found to delete.", "No Activities");
                    return;
                }
            }

            string selectedItem = lstActivityHistory.SelectedItem.ToString();
            int selectedIndex = lstActivityHistory.SelectedIndex;
            
            // Only allow deleting sub-items (activities), not headers
            if (!selectedItem.StartsWith("     "))
            {
                ShowDarkMessageBox("Please select an activity (not a header) to delete.", "Invalid Selection");
                return;
            }

            // Parse sub-item to get activity details
            int closingBracketIndex = selectedItem.IndexOf(']');
            if (closingBracketIndex == -1) return;

            string inside = selectedItem.Substring(6, closingBracketIndex - 6); // Skip "     ["
            string[] parts = inside.Split('|');
            if (parts.Length != 2) return;

            string timesStr = parts[0].Trim();
            string[] timeParts = timesStr.Split('-');
            if (timeParts.Length != 2) return;

            string endStr = timeParts[1].Trim();
            string activity = selectedItem.Substring(closingBracketIndex + 1).Trim();

            // Find date and type from header
            string dateStr = "";
            string type = "";
            for (int i = selectedIndex - 1; i >= 0; i--)
            {
                string item = lstActivityHistory.Items[i].ToString();
                if (!item.StartsWith("     ")) // Found header
                {
                    string headerWithoutIcon = item.Substring(2); // Remove icon
                    int headerBracketIndex = headerWithoutIcon.IndexOf(']');
                    if (headerBracketIndex != -1)
                    {
                        string headerInside = headerWithoutIcon.Substring(1, headerBracketIndex - 1);
                        string[] headerParts = headerInside.Split('|');
                        if (headerParts.Length == 3) // date | start-end | duration
                        {
                            dateStr = headerParts[0].Trim();
                            type = headerWithoutIcon.Substring(headerBracketIndex + 1).Trim();
                        }
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(dateStr)) return;

            // Parse end time to create log entry
            if (!DateTime.TryParseExact($"{dateStr} {endStr}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endTime))
                return;

            // Create log entry for removal - ensure exact format match
            string logEntryToRemove = string.IsNullOrEmpty(type) ? 
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {activity}" :
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {type} | {activity}";

            // Confirm deletion
            var result = ShowDarkMessageBox($"Are you sure you want to delete this activity?\n\n{activity}\n\nThis action cannot be undone.", 
                "Delete Activity", MessageBoxButtons.YesNo);
            
            if (result == DialogResult.Yes)
            {
                // Remove from log file
                RemoveActivityFromLogFile(logEntryToRemove);
                
                // Refresh display
                LoadLogHistory();
                
                trayIcon.ShowBalloonTip(2000, "Activity Deleted", "Activity has been successfully deleted.", ToolTipIcon.Info);
            }
        }



        private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
        {
            if (isTimerRunning)
            {
                StopTimer();
            }
        }
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
            // Enter key starts timer like Clockify
            if (!isTimerRunning)
            {
                StartTimer();
            }
        }
        
        private void OnStartStopClicked(object? sender, EventArgs e)
        {
            if (isTimerRunning)
            {
                StopTimer();
            }
            else
            {
                StartTimer();
            }
        }
        
        private void StartTimer()
        {
            string type = cmbType.Text.Trim();
            string activity = txtActivity.Text.Trim();
            
            if (type == TypeHint) type = "";
            if (activity == ActivityHint) activity = "";
            
            if (string.IsNullOrEmpty(activity))
            {
                ShowDarkMessageBox("Please enter an activity description to start the timer.", "Activity Required");
                return;
            }
            
            currentActivityType = type;
            currentActivityDescription = activity;
            timerStartTime = DateTime.Now;
            isTimerRunning = true;
            elapsedTime = TimeSpan.Zero;
            
            // Update button appearance
            btnStartStop.Text = "STOP";
            btnStartStop.BackColor = Color.FromArgb(220, 53, 69); // Red for STOP
            
            UpdateTrayIcon();
            trayIcon.ShowBalloonTip(2000, "Timer Started", $"Timer started for: {activity}", ToolTipIcon.Info);
            this.Hide();
        }
        
        private void StopTimer()
        {
            if (!isTimerRunning) return;
            
            DateTime endTime = DateTime.Now;
            elapsedTime = endTime - timerStartTime;
            
            // Save to log
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string logEntry = $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {typePart}{currentActivityDescription}";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            int totalMinutes = (int)(endTime - timerStartTime).TotalMinutes;
            string activityDesc = currentActivityDescription;
            
            // Reset timer state
            isTimerRunning = false;
            currentActivityType = "";
            currentActivityDescription = "";
            elapsedTime = TimeSpan.Zero;
            
            // Update button appearance
            btnStartStop.Text = "START";
            btnStartStop.BackColor = Color.FromArgb(0, 120, 215); // Blue for START
            
            // Reset form
            cmbType.Text = TypeHint;
            cmbType.ForeColor = Color.FromArgb(180, 180, 180);
            txtActivity.Text = ActivityHint;
            txtActivity.ForeColor = Color.FromArgb(180, 180, 180);
            
            LoadLogHistory();
            UpdateTrayIcon();
            
            trayIcon.ShowBalloonTip(3000, "Timer Stopped", 
                $"Activity logged: {totalMinutes} minutes\n{activityDesc}", ToolTipIcon.Info);
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
                        
                        // Calculate overall start and end times
                        DateTime overallStart = segments.Min(s => s.start);
                        DateTime overallEnd = segments.Max(s => s.end);
                        string overallStartStr = overallStart.ToString("HH:mm", CultureInfo.InvariantCulture);
                        string overallEndStr = overallEnd.ToString("HH:mm", CultureInfo.InvariantCulture);

                        // Create header key without icon for comparison
                        string headerKey = $"[{dateStr} | {overallStartStr}-{overallEndStr} | {FormatDuration(totalDur)}] {type}";
                        bool isExpanded = expandedHeaders.Contains(headerKey);
                        
                        // Header with expand/collapse indicator
                        string expandIcon = isExpanded ? "▼" : "▶";
                        string header = $"{expandIcon} {headerKey}";
                        
                        lstActivityHistory.Items.Add(header);

                        // Sub-items (only show if expanded)
                        if (isExpanded)
                        {
                            foreach (var seg in segments)
                            {
                                string startStr = seg.start.ToString("HH:mm", CultureInfo.InvariantCulture);
                                string endStr = seg.end.ToString("HH:mm", CultureInfo.InvariantCulture);
                                string sub = $"     [{startStr} - {endStr} | {FormatDuration(seg.dur)}] {seg.activity}";
                                lstActivityHistory.Items.Add(sub);
                            }
                        }
                    }
                }
            }
        }

        private string FormatDuration(int totalMinutes)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return $"{hours:D2}:{minutes:D2}:00";
        }
        
        private void RemoveActivityFromLogFile(string logEntryToRemove)
        {
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                var filteredLines = lines.Where(line => !line.Trim().Equals(logEntryToRemove.Trim(), StringComparison.Ordinal));
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

        private void StartDisplayTimer()
        {
            displayTimer.Stop();
            displayTimer.Interval = 1000; // 1 second for real-time updates
            displayTimer.Tick -= DisplayTimer_Tick;
            displayTimer.Tick += DisplayTimer_Tick;
            displayTimer.Start();
            
            UpdateTrayIcon();
        }
        
        private void DisplayTimer_Tick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            
            // Check exclude times
            CheckExcludeTimes(now);
            
            // Check for midnight crossing if timer is running
            if (isTimerRunning)
            {
                DateTime timerStartDate = timerStartTime.Date;
                DateTime currentDate = now.Date;
                
                // If we've crossed midnight, auto-split the activity
                if (currentDate > timerStartDate)
                {
                    AutoSplitAtMidnight();
                }
            }
            
            UpdateTrayIcon();
        }
        
        private void OnExcludeTimesClicked(object? sender, EventArgs e)
        {
            using (var excludeDialog = new ExcludeTimeDialog(excludeTimes))
            {
                if (excludeDialog.ShowDialog() == DialogResult.OK)
                {
                    excludeTimes = excludeDialog.ExcludeTimes;
                    SaveExcludeTimes();
                }
            }
        }
        
        private void CheckExcludeTimes(DateTime now)
        {
            TimeSpan currentTime = now.TimeOfDay;
            
            // Check if we're in an exclude period
            bool inExcludePeriod = excludeTimes.Any(et => currentTime >= et.start && currentTime < et.end);
            
            if (inExcludePeriod && isTimerRunning && !isTimerPausedForExclude)
            {
                // Pause timer for exclude period
                PauseTimerForExclude();
            }
            else if (!inExcludePeriod && isTimerPausedForExclude)
            {
                // Resume timer after exclude period
                ResumeTimerAfterExclude();
            }
        }
        
        private void PauseTimerForExclude()
        {
            if (!isTimerRunning) return;
            
            DateTime now = DateTime.Now;
            
            // Save current activity up to now
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string logEntry = $"[{now.ToString(CultureInfo.InvariantCulture)}] {typePart}{currentActivityDescription}";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            // Store current timer state
            pausedActivityType = currentActivityType;
            pausedActivityDescription = currentActivityDescription;
            pausedTimerStartTime = timerStartTime;
            
            // Mark as paused for exclude
            isTimerPausedForExclude = true;
            
            // Find exclude period name
            TimeSpan currentTime = now.TimeOfDay;
            var excludePeriod = excludeTimes.FirstOrDefault(et => currentTime >= et.start && currentTime < et.end);
            string periodName = excludePeriod.name ?? "Break";
            
            int totalMinutes = (int)(now - timerStartTime).TotalMinutes;
            trayIcon.ShowBalloonTip(3000, $"Timer Paused - {periodName}", 
                $"Activity saved: {totalMinutes} minutes\nTimer will resume after {periodName}", 
                ToolTipIcon.Info);
            
            LoadLogHistory();
        }
        
        private void ResumeTimerAfterExclude()
        {
            if (!isTimerPausedForExclude) return;
            
            // Restore timer state
            currentActivityType = pausedActivityType;
            currentActivityDescription = pausedActivityDescription;
            timerStartTime = DateTime.Now; // Start fresh from now
            isTimerRunning = true;
            isTimerPausedForExclude = false;
            
            // Update button appearance
            btnStartStop.Text = "STOP";
            btnStartStop.BackColor = Color.FromArgb(220, 53, 69);
            
            trayIcon.ShowBalloonTip(2000, "Timer Resumed", 
                $"Timer resumed for: {currentActivityDescription}", 
                ToolTipIcon.Info);
        }
        
        private void SaveExcludeTimes()
        {
            try
            {
                string configDir = Path.GetDirectoryName(GetLogFilePath())!;
                string excludeTimesPath = Path.Combine(configDir, "exclude_times.json");
                
                // Convert to serializable format
                var serializableData = excludeTimes.Select(et => new {
                    StartHours = et.start.Hours,
                    StartMinutes = et.start.Minutes,
                    EndHours = et.end.Hours,
                    EndMinutes = et.end.Minutes,
                    Name = et.name
                }).ToList();
                
                var json = System.Text.Json.JsonSerializer.Serialize(serializableData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(excludeTimesPath, json);
            }
            catch { /* Ignore save errors */ }
        }
        
        private void LoadExcludeTimes()
        {
            try
            {
                string configDir = Path.GetDirectoryName(GetLogFilePath())!;
                string excludeTimesPath = Path.Combine(configDir, "exclude_times.json");
                
                if (File.Exists(excludeTimesPath))
                {
                    string json = File.ReadAllText(excludeTimesPath);
                    var deserializedData = System.Text.Json.JsonSerializer.Deserialize<List<dynamic>>(json);
                    
                    if (deserializedData != null)
                    {
                        excludeTimes = new List<(TimeSpan start, TimeSpan end, string name)>();
                        
                        foreach (var item in deserializedData)
                        {
                            var jsonElement = (JsonElement)item;
                            int startHours = jsonElement.GetProperty("StartHours").GetInt32();
                            int startMinutes = jsonElement.GetProperty("StartMinutes").GetInt32();
                            int endHours = jsonElement.GetProperty("EndHours").GetInt32();
                            int endMinutes = jsonElement.GetProperty("EndMinutes").GetInt32();
                            string name = jsonElement.GetProperty("Name").GetString() ?? "Break";
                            
                            var startTime = new TimeSpan(startHours, startMinutes, 0);
                            var endTime = new TimeSpan(endHours, endMinutes, 0);
                            
                            excludeTimes.Add((startTime, endTime, name));
                        }
                    }
                    else
                    {
                        SetDefaultExcludeTimes();
                    }
                }
                else
                {
                    SetDefaultExcludeTimes();
                }
            }
            catch 
            { 
                SetDefaultExcludeTimes();
            }
        }
        
        private void SetDefaultExcludeTimes()
        {
            excludeTimes = new List<(TimeSpan start, TimeSpan end, string name)>
            {
                (new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), "Lunch Break")
            };
            SaveExcludeTimes();
        }
        
        private void AutoSplitAtMidnight()
        {
            if (!isTimerRunning) return;
            
            // Calculate midnight of the start date
            DateTime midnight = timerStartTime.Date.AddDays(1); // 00:00 of next day
            
            // Save the activity from start time to midnight
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string logEntry = $"[{midnight.ToString(CultureInfo.InvariantCulture)}] {typePart}{currentActivityDescription}";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            // Calculate duration for notification
            int totalMinutes = (int)(midnight - timerStartTime).TotalMinutes;
            
            // Restart timer from midnight with same activity
            timerStartTime = midnight;
            
            // Show notification about auto-split
            trayIcon.ShowBalloonTip(3000, "Activity Auto-Split", 
                $"Activity split at midnight: {totalMinutes} minutes logged\nTimer continues for: {currentActivityDescription}", 
                ToolTipIcon.Info);
            
            // Refresh history display
            LoadLogHistory();
        }
        
        private void UpdateTrayIcon()
        {
            // Update Stop Timer menu visibility
            var stopTimerItem = trayMenu.Items[1]; // Stop Timer is at index 1
            stopTimerItem.Visible = isTimerRunning || isTimerPausedForExclude;
            
            if (isTimerPausedForExclude)
            {
                trayIcon.Text = $"Timer Paused - {pausedActivityDescription}";
                
                if (lblTitle != null)
                {
                    lblTitle.Text = $"Timer Paused (Break Time) - {pausedActivityDescription}";
                }
            }
            else if (isTimerRunning)
            {
                elapsedTime = DateTime.Now - timerStartTime;
                string timeStr = $"{(int)elapsedTime.TotalHours:D2}:{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}";
                trayIcon.Text = $"Timer: {timeStr} - {currentActivityDescription}";
                
                // Update title if form is visible
                if (lblTitle != null)
                {
                    lblTitle.Text = $"Timer Running: {timeStr} - {currentActivityDescription}";
                }
            }
            else
            {
                trayIcon.Text = "Adiners - Daily Activity";
                
                // Update title if form is visible
                if (lblTitle != null)
                {
                    lblTitle.Text = "What are you working on?";
                }
            }
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

        private void RefreshTrayIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            
            try
            {
                if (File.Exists(iconPath))
                {
                    var newIcon = new Icon(iconPath);
                    
                    // Update tray icon
                    var oldIcon = trayIcon.Icon;
                    trayIcon.Icon = newIcon;
                    
                    // Dispose old icon to free resources
                    if (oldIcon != null && oldIcon != SystemIcons.Application)
                    {
                        oldIcon.Dispose();
                    }
                }
            }
            catch
            {
                // Keep current icon if there's an error
            }
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
                ForeColor = Color.White,
                TopMost = true
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