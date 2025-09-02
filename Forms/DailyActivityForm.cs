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
        
        // Clockify integration
        private ClockifyService clockifyService = null!;
        private string currentClockifyTimeEntryId = "";
        private Timer clockifyCheckTimer = null!;
        private string clockifyUserId = "";
        
        private const string TypeHint = "Enter type...";
        private const string ActivityHint = "Enter activity...";
        #endregion

        #region Constructor
        public DailyActivityForm(DateTime appStartTime, DateTime popupTime)
        {
            InitializeComponent();
            SetupForm();
            LoadConfig();
            InitializeClockify();
            
            // Initialize popup timer variables
            this.popupTime = popupTime;
            popupIntervalInMinutes = config.IntervalHours * 60;
            dontShowPopupToday = config.DontShowPopupToday && config.LastDontShowDate.Date == DateTime.Today;
            
            StartDisplayTimer();
            LoadLogHistory();
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            
            // Check for running Clockify timer on startup
            _ = Task.Run(CheckClockifyOnStartup);
            
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
            // Tray menu with organized structure
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(30, 30, 30);
            trayMenu.ForeColor = Color.White;
            
            // === TIMER CONTROLS ===
            var timerControlsMenu = new ToolStripMenuItem("⏱️ Timer");
            timerControlsMenu.ToolTipText = "Timer controls and activity input";
            
            var inputNowItem = new ToolStripMenuItem("▶️ Start Activity", null, OnInputNowClicked);
            inputNowItem.ToolTipText = "Start timer with new activity (Double-click tray icon)";
            timerControlsMenu.DropDownItems.Add(inputNowItem);
            
            var stopTimerItem = new ToolStripMenuItem("⏹️ Stop Timer", null, OnStopTimerClicked);
            stopTimerItem.ToolTipText = "Stop current running timer";
            timerControlsMenu.DropDownItems.Add(stopTimerItem);
            
            timerControlsMenu.DropDownItems.Add("-");
            
            var timerInfoItem = new ToolStripMenuItem("ℹ️ Timer Status", null, OnTestTimerClicked);
            timerInfoItem.ToolTipText = "View detailed timer information and debug status";
            timerControlsMenu.DropDownItems.Add(timerInfoItem);
            
            trayMenu.Items.Add(timerControlsMenu);
            
            // === DATA & REPORTS ===
            var dataMenu = new ToolStripMenuItem("📊 Data & Reports");
            dataMenu.ToolTipText = "View data, export reports, and dashboard";
            
            var dashboardItem = new ToolStripMenuItem("📈 Dashboard", null, OnDashboardClicked);
            dashboardItem.ToolTipText = "View activity dashboard and statistics";
            dataMenu.DropDownItems.Add(dashboardItem);
            
            var exportItem = new ToolStripMenuItem("📋 Export to Excel", null, OnExportLogClicked);
            exportItem.ToolTipText = "Export activity log to Excel with detailed reports";
            dataMenu.DropDownItems.Add(exportItem);
            
            trayMenu.Items.Add(dataMenu);
            
            // === SETTINGS ===
            var settingsMenu = new ToolStripMenuItem("⚙️ Settings");
            settingsMenu.ToolTipText = "Configure application settings";
            
            var intervalItem = new ToolStripMenuItem("⏰ Reminder Interval", null, OnSetIntervalClicked);
            intervalItem.ToolTipText = "Set popup reminder interval (legacy feature)";
            settingsMenu.DropDownItems.Add(intervalItem);
            
            var excludeTimesItem = new ToolStripMenuItem("☕ Break Times", null, OnExcludeTimesClicked);
            excludeTimesItem.ToolTipText = "Configure break periods (lunch, coffee) that auto-pause timer";
            settingsMenu.DropDownItems.Add(excludeTimesItem);
            
            var clockifyItem = new ToolStripMenuItem("🔗 Clockify Integration", null, OnClockifySettingsClicked);
            clockifyItem.ToolTipText = "Configure Clockify API connection and sync settings";
            settingsMenu.DropDownItems.Add(clockifyItem);
            
            settingsMenu.DropDownItems.Add("-");
            
            var dontShowMenuItem = new ToolStripMenuItem("🔕 Disable Popups Today");
            dontShowMenuItem.CheckOnClick = true;
            dontShowMenuItem.Checked = config?.DontShowPopupToday ?? false;
            dontShowMenuItem.Click += OnDontShowTodayClicked;
            dontShowMenuItem.ToolTipText = "Temporarily disable popup reminders for today only";
            settingsMenu.DropDownItems.Add(dontShowMenuItem);
            
            trayMenu.Items.Add(settingsMenu);
            
            trayMenu.Items.Add("-");
            
            // === HELP & UPDATES ===
            var helpMenu = new ToolStripMenuItem("❓ Help");
            helpMenu.ToolTipText = "Get help and information about the application";
            
            var shortcutsItem = new ToolStripMenuItem("⌨️ Keyboard Shortcuts", null, OnShowShortcutsClicked);
            shortcutsItem.ToolTipText = "View all available keyboard shortcuts and hotkeys";
            helpMenu.DropDownItems.Add(shortcutsItem);
            
            var featuresItem = new ToolStripMenuItem("✨ Features Guide", null, OnShowFeaturesClicked);
            featuresItem.ToolTipText = "Learn about all available features and how to use them";
            helpMenu.DropDownItems.Add(featuresItem);
            
            helpMenu.DropDownItems.Add("-");
            
            var updatesItem = new ToolStripMenuItem("🔄 Check for Updates", null, OnCheckUpdatesClicked);
            updatesItem.ToolTipText = "Check for application updates and download latest version";
            helpMenu.DropDownItems.Add(updatesItem);
            
            var aboutItem = new ToolStripMenuItem("ℹ️ About", null, OnAboutClicked);
            aboutItem.ToolTipText = "View application information, version, and credits";
            helpMenu.DropDownItems.Add(aboutItem);
            
            trayMenu.Items.Add(helpMenu);
            
            trayMenu.Items.Add("-");
            
            // === EXIT ===
            var exitItem = new ToolStripMenuItem("🚪 Exit", null, OnExitClicked);
            exitItem.ToolTipText = "Close the application completely";
            trayMenu.Items.Add(exitItem);

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
            historyContextMenu.Items.Add("Delete Activities (F3)", null, OnDeleteHistoryClicked);
            historyContextMenu.Items.Add("Sync to Clockify (F4)", null, OnSyncToClockifyClicked);
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
            cmbType.DropDown += async (s, e) => {
                // Refresh items when dropdown opens
                var currentText = cmbType.Text;
                var uniqueTypes = await GetUniqueTypesFromAllSourcesAsync();
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
            lstActivityHistory.SelectionMode = SelectionMode.MultiExtended;
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
            if (e.KeyCode == Keys.F4)
            {
                OnSyncToClockifyClicked(sender, e);
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
            else if (e.KeyCode == Keys.F4)
            {
                OnSyncToClockifyClicked(sender, e);
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
                    // Format: "     [HH:mm - HH:mm | duration] [✓/⚠] activity"
                    int firstClosingBracket = selectedItemText.IndexOf(']');
                    if (firstClosingBracket != -1)
                    {
                        // Find second bracket (sync status)
                        int secondOpenBracket = selectedItemText.IndexOf('[', firstClosingBracket + 1);
                        int secondClosingBracket = selectedItemText.IndexOf(']', secondOpenBracket + 1);
                        
                        string activity;
                        if (secondClosingBracket != -1)
                        {
                            // Extract activity after sync status bracket
                            activity = selectedItemText.Substring(secondClosingBracket + 1).Trim();
                        }
                        else
                        {
                            // Fallback: extract after first bracket (old format)
                            activity = selectedItemText.Substring(firstClosingBracket + 1).Trim();
                            // Remove sync icon if present
                            if (activity.StartsWith("✓ ") || activity.StartsWith("⚠ "))
                                activity = activity.Substring(2);
                        }
                        
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

        private async void OnExitClicked(object? sender, EventArgs e)
        {
            // Stop timer before exit
            if (isTimerRunning)
            {
                StopTimer();
                // Wait a moment for Clockify sync to complete
                await Task.Delay(1000);
            }
            
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void OnInputNowClicked(object? sender, EventArgs e)
        {
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
                    
                    // Update popup timer with new interval
                    popupIntervalInMinutes = config.IntervalHours * 60;
                    popupTimer.Stop();
                    popupTimer.Interval = popupIntervalInMinutes * 60 * 1000;
                    popupTimer.Start();
                    
                    trayIcon.ShowBalloonTip(2000, "Interval Updated", 
                        $"Reminder interval set to {config.IntervalHours} hour(s)", ToolTipIcon.Info);
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
            
            ShowTimerInfoDialog(info);
        }
        
        private void ShowTimerInfoDialog(string info)
        {
            Form timerForm = new Form
            {
                Width = 450,
                Height = 280,
                Text = "Timer Information",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                TopMost = true
            };
            
            Label infoLabel = new Label
            {
                Text = info,
                AutoSize = false,
                Size = new Size(410, 180),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            
            Button refreshButton = new Button
            {
                Text = "Refresh",
                Size = new Size(80, 30),
                Location = new Point(180, 210),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += (s, e) => {
                DateTime now = DateTime.Now;
                TimeSpan timeSinceLastPopup = now - popupTime;
                int minutesSinceLastPopup = (int)timeSinceLastPopup.TotalMinutes;
                int minutesUntilNext = Math.Max(0, popupIntervalInMinutes - minutesSinceLastPopup);
                DateTime nextPopupTime = popupTime.AddMinutes(popupIntervalInMinutes);
                
                string updatedInfo = $"Timer Status:\n" +
                                   $"Current Time: {now:HH:mm:ss}\n" +
                                   $"Next Popup Time: {(dontShowPopupToday ? "Disabled" : nextPopupTime.ToString("HH:mm:ss"))}\n" +
                                   $"Minutes Until Next: {(dontShowPopupToday ? "Disabled" : minutesUntilNext.ToString())}\n" +
                                   $"Interval Setting: {config.IntervalHours} hours ({popupIntervalInMinutes} minutes)\n" +
                                   $"Timer Running: {popupTimer.Enabled}\n" +
                                   $"Don't Show Today: {dontShowPopupToday}\n" +
                                   $"Last Popup: {popupTime:HH:mm:ss} ({minutesSinceLastPopup} min ago)";
                infoLabel.Text = updatedInfo;
            };
            
            Button closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 30),
                Location = new Point(270, 210),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            closeButton.FlatAppearance.BorderSize = 0;
            
            timerForm.Controls.Add(infoLabel);
            timerForm.Controls.Add(refreshButton);
            timerForm.Controls.Add(closeButton);
            
            timerForm.ShowDialog();
        }
        
        private void InitializeClockify()
        {
            clockifyService = new ClockifyService();
            if (!string.IsNullOrEmpty(config.ClockifyApiKey))
            {
                clockifyService.SetApiKey(config.ClockifyApiKey);
            }
        }
        
        private void OnClockifySettingsClicked(object? sender, EventArgs e)
        {
            using (var clockifyDialog = new ClockifySettingsDialog(
                config.ClockifyApiKey,
                config.ClockifyWorkspaceId,
                config.ClockifyProjectId,
                config.ClockifyAutoCreateTasks))
            {
                if (clockifyDialog.ShowDialog() == DialogResult.OK)
                {
                    config.ClockifyApiKey = clockifyDialog.ApiKey;
                    config.ClockifyWorkspaceId = clockifyDialog.WorkspaceId;
                    config.ClockifyProjectId = clockifyDialog.ProjectId;
                    config.ClockifyAutoCreateTasks = clockifyDialog.AutoCreateTasks;
                    config.Save();
                    
                    InitializeClockify();
                    trayIcon.ShowBalloonTip(2000, "Clockify Settings", "Settings saved successfully!", ToolTipIcon.Info);
                }
            }
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
        
        private async Task CheckClockifyOnStartup()
        {
            if (!IsClockifyConnected()) return;
            
            await Task.Delay(3000); // Wait for app to fully initialize
            
            try
            {
                // Get user ID first
                var user = await clockifyService.GetCurrentUserAsync();
                if (user == null) return;
                
                clockifyUserId = user.Id;
                
                // Check for running timer
                var currentEntry = await clockifyService.GetCurrentTimeEntryAsync(config.ClockifyWorkspaceId, clockifyUserId);
                if (currentEntry != null)
                {
                    this.Invoke(() => {
                        // Auto-resume timer from Clockify
                        currentClockifyTimeEntryId = currentEntry.Id;
                        currentActivityDescription = currentEntry.Description ?? "Resumed Activity";
                        currentActivityType = "";
                        timerStartTime = DateTime.Now; // Start from now
                        isTimerRunning = true;
                        
                        // Update UI
                        btnStartStop.Text = "STOP";
                        btnStartStop.BackColor = Color.FromArgb(220, 53, 69);
                        
                        UpdateTrayIcon();
                        trayIcon.ShowBalloonTip(3000, "Timer Resumed", 
                            $"Resumed running timer from Clockify: {currentActivityDescription}", 
                            ToolTipIcon.Info);
                    });
                }
            }
            catch { /* Ignore startup check errors */ }
        }
        
        private async Task CheckForUpdatesOnStartup()
        {
            if (!config.CheckForUpdates) return;
            
            // Check once per day
            if ((DateTime.Now - config.LastUpdateCheck).TotalHours < 24) return;
            
            // Wait a bit after startup to avoid conflicts with initial UI
            await Task.Delay(5000); // Wait 5 seconds after startup
            
            var hasUpdate = await UpdateService.CheckForUpdatesAsync();
            if (hasUpdate)
            {
                var release = await UpdateService.GetLatestReleaseAsync();
                if (release != null)
                {
                    this.Invoke(() => {
                        // Double-check if overlay is visible and wait if needed
                        if (this.Visible && this.WindowState == FormWindowState.Maximized)
                        {
                            // Schedule to show update dialog after a delay
                            var delayTimer = new System.Windows.Forms.Timer();
                            delayTimer.Interval = 3000; // Wait 3 seconds
                            delayTimer.Tick += (s, e) => {
                                delayTimer.Stop();
                                ShowUpdateDialog(release);
                            };
                            delayTimer.Start();
                        }
                        else
                        {
                            ShowUpdateDialog(release);
                        }
                    });
                }
            }
            
            config.LastUpdateCheck = DateTime.Now;
            config.Save();
        }
        
        private void ShowUpdateDialog(GitHubRelease release)
        {
            // If fullscreen overlay is visible, hide it first
            if (this.Visible && this.WindowState == FormWindowState.Maximized)
            {
                this.Hide();
            }
            
            var updateDialog = new UpdateDialog(release);
            updateDialog.TopMost = true; // Ensure it's always on top
            updateDialog.Show(); // Non-blocking
            updateDialog.BringToFront();
            updateDialog.Activate();
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
        
        private void OnShowShortcutsClicked(object? sender, EventArgs e)
        {
            Form shortcutsForm = new Form
            {
                Width = 540,
                Height = 500,
                Text = "Keyboard Shortcuts & Hotkeys",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            
            Label titleLabel = new Label
            {
                Text = "⌨️ Keyboard Shortcuts & Hotkeys",
                Location = new Point(20, 20),
                Size = new Size(480, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            
            // Create scrollable panel for content
            Panel scrollPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(480, 380),
                BackColor = Color.FromArgb(35, 35, 35),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            Label shortcutsLabel = new Label
            {
                Text = "🎯 ACTIVITY MANAGEMENT\n" +
                       "   Enter          - Start timer with current activity\n" +
                       "   Escape         - Hide fullscreen overlay\n" +
                       "   F2             - Edit selected activity in history\n" +
                       "   F3             - Delete selected activity (with confirmation)\n" +
                       "   F4             - Sync selected activity to Clockify\n\n" +
                       "🖱️ MOUSE ACTIONS\n" +
                       "   Double-click tray icon    - Open activity input overlay\n" +
                       "   Right-click tray icon     - Show organized context menu\n" +
                       "   Right-click history item  - Show edit/delete menu\n" +
                       "   Double-click history item - Auto-fill activity details\n" +
                       "   Double-click header       - Expand/collapse group\n\n" +
                       "⏱️ TIMER CONTROLS\n" +
                       "   START/STOP button         - Toggle timer state\n" +
                       "   Tray menu > Timer         - Access timer controls\n" +
                       "   Auto-pause during breaks  - Configured exclude times\n" +
                       "   Auto-split at midnight    - Accurate daily tracking\n\n" +
                       "🔗 CLOCKIFY INTEGRATION\n" +
                       "   Auto-sync on timer stop   - If Clockify configured\n" +
                       "   Manual sync via F4        - Sync individual activities\n" +
                       "   Bulk sync via menu        - Sync all unsynced activities\n\n" +
                       "📊 DATA & REPORTS\n" +
                       "   Tray menu > Export         - Excel export with date range\n" +
                       "   Tray menu > Dashboard      - View activity statistics\n\n" +
                       "⚙️ SETTINGS ACCESS\n" +
                       "   Tray menu > Settings       - All configuration options\n" +
                       "   Break Times               - Configure auto-pause periods\n" +
                       "   Clockify Integration      - API and sync settings\n\n" +
                       "💡 PRO TIPS\n" +
                       "   • Timer runs continuously in system tray\n" +
                       "   • Real-time elapsed time display in tray text\n" +
                       "   • Activities auto-save when timer stops\n" +
                       "   • Use activity type dropdown for consistency\n" +
                       "   • History groups by date and type for easy review\n" +
                       "   • Sync status indicators show Clockify integration\n" +
                       "   • Multiple selection supported for bulk delete",
                Location = new Point(10, 10),
                Size = new Size(440, 700), // Larger height for scrolling
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                AutoSize = false
            };
            
            scrollPanel.Controls.Add(shortcutsLabel);
            
            shortcutsForm.Controls.AddRange(new Control[] { titleLabel, scrollPanel });
            
            // Adjust scroll panel to fill remaining space
            scrollPanel.Size = new Size(480, 420);
            shortcutsForm.ShowDialog();
        }
        
        private void OnShowFeaturesClicked(object? sender, EventArgs e)
        {
            Form featuresForm = new Form
            {
                Width = 580,
                Height = 550,
                Text = "Features Guide",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            
            Label titleLabel = new Label
            {
                Text = "✨ Features Guide",
                Location = new Point(20, 20),
                Size = new Size(520, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            
            // Create scrollable panel for content
            Panel scrollPanel = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(520, 430),
                BackColor = Color.FromArgb(35, 35, 35),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            Label featuresLabel = new Label
            {
                Text = "⏱️ CLOCKIFY-STYLE TIMER\n" +
                       "   • Manual START/STOP button like Clockify\n" +
                       "   • Real-time elapsed time in tray icon\n" +
                       "   • Enter key to start timer quickly\n" +
                       "   • Timer continues across app restarts\n\n" +
                       "🌙 AUTOMATIC MIDNIGHT SPLITTING\n" +
                       "   • Activities crossing midnight auto-split\n" +
                       "   • Accurate daily tracking guaranteed\n" +
                       "   • No manual intervention required\n\n" +
                       "☕ EXCLUDE TIME PERIODS\n" +
                       "   • Configure lunch/coffee breaks\n" +
                       "   • Timer auto-pauses and resumes\n" +
                       "   • Default: 12:00-13:00 lunch break\n" +
                       "   • Multiple break periods supported\n\n" +
                       "✏️ ON-THE-FLY EDITING\n" +
                       "   • Edit activity times with F2 key\n" +
                       "   • Delete incorrect activities with F3\n" +
                       "   • Right-click context menu support\n" +
                       "   • Bulk operations for multiple activities\n\n" +
                       "📊 SMART REPORTING\n" +
                       "   • Excel export with 3 detailed sheets\n" +
                       "   • Overtime detection (weekends + after 8PM)\n" +
                       "   • Activity type management and history\n" +
                       "   • Date range filtering for exports\n\n" +
                       "🔗 CLOCKIFY INTEGRATION\n" +
                       "   • Sync activities to Clockify automatically\n" +
                       "   • Auto-create tasks and projects\n" +
                       "   • Bi-directional sync support\n" +
                       "   • Real-time sync status indicators\n\n" +
                       "🔄 AUTO-UPDATE SYSTEM\n" +
                       "   • Automatic update notifications\n" +
                       "   • One-click update downloads\n" +
                       "   • GitHub releases integration\n\n" +
                       "📱 SYSTEM INTEGRATION\n" +
                       "   • System tray operation\n" +
                       "   • Windows startup integration\n" +
                       "   • Session end detection\n" +
                       "   • Dark mode UI throughout",
                Location = new Point(10, 10),
                Size = new Size(480, 800), // Larger height for scrolling
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                AutoSize = false
            };
            
            scrollPanel.Controls.Add(featuresLabel);
            
            featuresForm.Controls.AddRange(new Control[] { titleLabel, scrollPanel });
            
            // Adjust scroll panel to fill remaining space
            scrollPanel.Size = new Size(520, 470);
            featuresForm.ShowDialog();
        }

        private void OnAboutClicked(object? sender, EventArgs e)
        {
            Form aboutForm = new Form
            {
                Width = 520,
                Height = 480,
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
                Size = new Size(460, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            
            Label versionLabel = new Label
            {
                Text = $"Version {UpdateService.CurrentVersion}",
                Location = new Point(20, 55),
                Size = new Size(460, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            // Create scrollable panel for content
            Panel scrollPanel = new Panel
            {
                Location = new Point(20, 85),
                Size = new Size(460, 340),
                BackColor = Color.FromArgb(35, 35, 35),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            Label contentLabel = new Label
            {
                Text = "📝 DESCRIPTION\n" +
                       "A comprehensive activity tracking application designed to help\n" +
                       "professionals monitor and manage their daily work activities.\n\n" +
                       "Never miss tracking your activities again! This tool provides\n" +
                       "automatic reminders, detailed logging, and comprehensive\n" +
                       "reporting to ensure all your work is properly documented.\n\n" +
                       "✨ KEY FEATURES\n" +
                       "• Clockify-style manual timer with START/STOP button\n" +
                       "• Automatic midnight activity splitting for accurate tracking\n" +
                       "• Exclude time periods (lunch, coffee breaks) with auto-pause\n" +
                       "• On-the-fly activity editing with F2 key\n" +
                       "• Delete activities with F3 key and confirmation\n" +
                       "• Smart activity type management with history\n" +
                       "• Excel export with 3 detailed sheets (Log, Summary, Overtime)\n" +
                       "• Overtime detection for weekends and after-hours work\n" +
                       "• Clockify integration with bi-directional sync\n" +
                       "• Auto-update system with GitHub releases\n" +
                       "• Dark mode UI with modern design\n" +
                       "• System tray operation with real-time timer display\n\n" +
                       "🛠️ TECHNICAL DETAILS\n" +
                       "• Built with .NET 6 Windows Forms\n" +
                       "• Self-contained executable (no .NET runtime required)\n" +
                       "• Professional Windows installer with Inno Setup\n" +
                       "• Automatic startup integration\n" +
                       "• Session end detection for timer safety\n" +
                       "• JSON configuration with backward compatibility\n\n" +
                       "🏢 COMPANY INFORMATION\n" +
                       "© 2024 AdIns (Advance Innovations)\n" +
                       "PT. Adicipta Inovasi Teknologi\n" +
                       "Developed by LJP\n\n" +
                       "📞 SUPPORT & UPDATES\n" +
                       "• Automatic update notifications\n" +
                       "• GitHub repository for issues and feedback\n" +
                       "• Comprehensive help system with shortcuts guide\n" +
                       "• Features guide with detailed explanations",
                Location = new Point(10, 10),
                Size = new Size(420, 800), // Larger height for scrolling
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                AutoSize = false
            };
            
            scrollPanel.Controls.Add(contentLabel);
            
            Button btnClose = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                Location = new Point(420, 435),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            btnClose.FlatAppearance.BorderSize = 0;
            
            aboutForm.Controls.AddRange(new Control[] { 
                titleLabel, versionLabel, scrollPanel, btnClose 
            });
            
            aboutForm.ShowDialog();
        }

        private async void OnEditHistoryClicked(object? sender, EventArgs e)
        {
            // Check if multiple items are selected
            if (lstActivityHistory.SelectedItems.Count > 1)
            {
                ShowDarkMessageBox("Multiple selection is only supported for delete operation.\nPlease select a single activity to edit.", "Multiple Selection Not Supported");
                return;
            }
            
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

            // Parse sub-item: "     [HH:mm - HH:mm | duration] [✓/⚠] activity"
            int firstClosingBracket = selectedItem.IndexOf(']');
            if (firstClosingBracket == -1) return;

            string inside = selectedItem.Substring(6, firstClosingBracket - 6); // Skip "     ["
            string[] parts = inside.Split('|');
            if (parts.Length != 2) return;

            string timesStr = parts[0].Trim();
            string[] timeParts = timesStr.Split('-');
            if (timeParts.Length != 2) return;

            string startStr = timeParts[0].Trim();
            string endStr = timeParts[1].Trim();
            
            // Extract activity after sync status bracket
            int secondOpenBracket = selectedItem.IndexOf('[', firstClosingBracket + 1);
            int secondClosingBracket = selectedItem.IndexOf(']', secondOpenBracket + 1);
            
            string activity;
            if (secondClosingBracket != -1)
            {
                activity = selectedItem.Substring(secondClosingBracket + 1).Trim();
            }
            else
            {
                // Fallback for old format
                activity = selectedItem.Substring(firstClosingBracket + 1).Trim();
                if (activity.StartsWith("✓ ") || activity.StartsWith("⚠ "))
                    activity = activity.Substring(2);
            }

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
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] [SYNCED] {activity}" :
                $"[{endTime.ToString(CultureInfo.InvariantCulture)}] [SYNCED] {type} | {activity}";
            
            // Try LOCAL flag if SYNCED not found
            if (!File.ReadAllText(GetLogFilePath()).Contains(originalLogEntry))
            {
                originalLogEntry = string.IsNullOrEmpty(type) ? 
                    $"[{endTime.ToString(CultureInfo.InvariantCulture)}] [LOCAL] {activity}" :
                    $"[{endTime.ToString(CultureInfo.InvariantCulture)}] [LOCAL] {type} | {activity}";
            }

            // Open edit dialog
            using (var editDialog = new EditActivityDialog(startTime, endTime, type, activity))
            {
                if (editDialog.ShowDialog() == DialogResult.OK)
                {
                    // Extract Clockify ID from original entry
                    string clockifyId = ExtractClockifyIdFromLogEntry(originalLogEntry);
                    
                    // Remove original entry from log file
                    string editLogFilePath = GetLogFilePath();
                    if (File.Exists(editLogFilePath))
                    {
                        var lines = File.ReadAllLines(editLogFilePath).ToList();
                        for (int i = lines.Count - 1; i >= 0; i--)
                        {
                            if (lines[i].Contains(activity))
                            {
                                lines.RemoveAt(i);
                                break;
                            }
                        }
                        File.WriteAllLines(editLogFilePath, lines);
                    }
                    
                    // Update Clockify if entry was synced
                    bool updatedInClockify = false;
                    if (!string.IsNullOrEmpty(clockifyId))
                    {
                        updatedInClockify = await UpdateClockifyTimeEntryAsync(clockifyId, editDialog.ActivityType, editDialog.ActivityText, editDialog.StartTime, editDialog.EndTime);
                    }
                    
                    // Add new entry with consistent format
                    string newTypePart = string.IsNullOrEmpty(editDialog.ActivityType) ? "" : $"{editDialog.ActivityType} | ";
                    string syncFlag = updatedInClockify ? "[SYNCED]" : "[LOCAL]";
                    string clockifyIdPart = updatedInClockify ? $" [CID:{clockifyId}]" : "";
                    string newLogEntry = $"[{editDialog.EndTime.ToString(CultureInfo.InvariantCulture)}] {syncFlag} {newTypePart}{editDialog.ActivityText}{clockifyIdPart}";
                    string logFilePath = GetLogFilePath();
                    File.AppendAllText(logFilePath, newLogEntry + Environment.NewLine);
                    
                    // Refresh display
                    LoadLogHistory();
                    
                    string updateMessage = updatedInClockify ? "Activity updated in both local and Clockify." : "Activity updated locally.";
                    trayIcon.ShowBalloonTip(2000, "Activity Updated", updateMessage, ToolTipIcon.Info);
                }
            }
        }
        
        private async void OnDeleteHistoryClicked(object? sender, EventArgs e)
        {
            // Get selected items
            var selectedItems = lstActivityHistory.SelectedItems.Cast<object>().ToList();
            
            // If no selection, select first activity
            if (selectedItems.Count == 0)
            {
                for (int i = 0; i < lstActivityHistory.Items.Count; i++)
                {
                    string item = lstActivityHistory.Items[i].ToString();
                    if (item.StartsWith("     "))
                    {
                        lstActivityHistory.SelectedIndex = i;
                        selectedItems = new List<object> { lstActivityHistory.Items[i] };
                        break;
                    }
                }
                
                if (selectedItems.Count == 0)
                {
                    ShowDarkMessageBox("No activities found to delete.", "No Activities");
                    return;
                }
            }

            // Filter activities only
            var activitiesToDelete = new List<string>();
            foreach (var item in selectedItems)
            {
                string itemText = item.ToString();
                if (itemText.StartsWith("     "))
                {
                    // Extract activity name for display
                    int bracketEnd = itemText.IndexOf(']');
                    if (bracketEnd > 0)
                    {
                        string activityName = itemText.Substring(bracketEnd + 1).Trim();
                        // Remove sync icon
                        if (activityName.StartsWith("✓ ") || activityName.StartsWith("⚠ "))
                            activityName = activityName.Substring(2);
                        activitiesToDelete.Add(activityName);
                    }
                }
            }
            
            if (activitiesToDelete.Count == 0)
            {
                ShowDarkMessageBox("Please select activities (not headers) to delete.", "Invalid Selection");
                return;
            }

            // Confirm deletion
            string confirmMessage = activitiesToDelete.Count == 1 ?
                $"Are you sure you want to delete this activity?\n\n{activitiesToDelete[0]}\n\nThis action cannot be undone." :
                $"Are you sure you want to delete these {activitiesToDelete.Count} activities?\n\n{string.Join("\n", activitiesToDelete.Take(3))}{(activitiesToDelete.Count > 3 ? "\n...and more" : "")}\n\nThis action cannot be undone.";
                
            var result = ShowDarkMessageBox(confirmMessage, "Delete Activities", MessageBoxButtons.YesNo);
            
            if (result == DialogResult.Yes)
            {
                // Simple approach: remove lines containing the activity names
                string logFilePath = GetLogFilePath();
                if (File.Exists(logFilePath))
                {
                    var lines = File.ReadAllLines(logFilePath).ToList();
                    var linesToRemove = new List<string>();
                    
                    foreach (string activityName in activitiesToDelete)
                    {
                        // Find and mark lines for removal
                        for (int i = lines.Count - 1; i >= 0; i--)
                        {
                            string line = lines[i];
                            if (line.Contains(activityName) && !linesToRemove.Contains(line))
                            {
                                linesToRemove.Add(line);
                                break; // Only remove first match to avoid duplicates
                            }
                        }
                    }
                    
                    // Remove the lines
                    foreach (string lineToRemove in linesToRemove)
                    {
                        lines.Remove(lineToRemove);
                    }
                    
                    // Write back to file
                    File.WriteAllLines(logFilePath, lines);
                }
                
                // Refresh display
                LoadLogHistory();
                
                string successMessage = activitiesToDelete.Count == 1 ?
                    "Activity has been successfully deleted." :
                    $"{activitiesToDelete.Count} activities have been successfully deleted.";
                    
                trayIcon.ShowBalloonTip(2000, "Activities Deleted", successMessage, ToolTipIcon.Info);
            }
        }
        
        private async void OnSyncToClockifyClicked(object? sender, EventArgs e)
        {
            if (!IsClockifyConnected())
            {
                ShowDarkMessageBox("Please configure Clockify settings first to sync activities.", "Clockify Not Connected");
                return;
            }
            
            // Find all unsynced activities
            var unsyncedActivities = GetUnsyncedActivities();
            
            if (unsyncedActivities.Count == 0)
            {
                ShowDarkMessageBox("No unsynced activities found. All activities are already synced to Clockify.", "No Activities to Sync");
                return;
            }
            
            // Show confirmation
            string confirmMessage = $"Found {unsyncedActivities.Count} unsynced activities.\n\nDo you want to sync them to Clockify?";
            var result = ShowDarkMessageBox(confirmMessage, "Sync to Clockify", MessageBoxButtons.YesNo);
            
            if (result == DialogResult.Yes)
            {
                int syncedCount = 0;
                int failedCount = 0;
                
                foreach (var activity in unsyncedActivities)
                {
                    try
                    {
                        // Create time entry in Clockify
                        string taskId = null;
                        if (config.ClockifyAutoCreateTasks && !string.IsNullOrEmpty(activity.Type))
                        {
                            var task = await clockifyService.CreateTaskAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId, activity.Type);
                            taskId = task?.Id;
                        }
                        
                        var timeEntry = await clockifyService.CreateTimeEntryAsync(
                            config.ClockifyWorkspaceId,
                            config.ClockifyProjectId,
                            taskId,
                            activity.Description,
                            activity.StartTime,
                            activity.EndTime);
                        
                        if (timeEntry != null)
                        {
                            // Update log entry to mark as synced
                            UpdateLogEntryToSynced(activity.OriginalLogEntry, timeEntry.Id);
                            syncedCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch
                    {
                        failedCount++;
                    }
                }
                
                // Refresh display
                LoadLogHistory();
                
                // Show result
                string resultMessage = $"Sync completed!\n\nSynced: {syncedCount} activities\nFailed: {failedCount} activities";
                trayIcon.ShowBalloonTip(3000, "Clockify Sync", resultMessage, ToolTipIcon.Info);
            }
        }
        
        private List<UnsyncedActivity> GetUnsyncedActivities()
        {
            var unsyncedActivities = new List<UnsyncedActivity>();
            string logFilePath = GetLogFilePath();
            
            if (!File.Exists(logFilePath)) return unsyncedActivities;
            
            var lines = File.ReadAllLines(logFilePath);
            var entries = new List<(DateTime timestamp, string type, string activity, string originalLine)>();
            
            // Parse all entries - include LOCAL and entries without sync flags
            foreach (string line in lines)
            {
                // Skip entries that are already synced
                if (line.Contains("[SYNCED]") || line.Contains("[CID:"))
                    continue;
                    
                var parsed = ParseLogEntry(line);
                if (parsed != null)
                {
                    entries.Add((parsed.Value.timestamp, parsed.Value.type, parsed.Value.activity, line));
                }
            }
            
            // Group by date and type to calculate time segments
            var dateGroups = entries.GroupBy(e => e.timestamp.Date).OrderBy(g => g.Key);
            
            foreach (var dateGroup in dateGroups)
            {
                var typeGroups = dateGroup.GroupBy(e => e.type, StringComparer.OrdinalIgnoreCase);
                
                foreach (var typeGroup in typeGroups)
                {
                    var typeEntries = typeGroup.ToList();
                    DateTime? prevTime = null;
                    
                    // Use actual first entry time as starting point
                    if (typeEntries.Count > 0)
                    {
                        prevTime = typeEntries.First().timestamp;
                    }
                    
                    foreach (var entry in typeEntries)
                    {
                        DateTime start = prevTime ?? entry.timestamp;
                        DateTime end = entry.timestamp;
                        
                        if ((end - start).TotalMinutes > 0)
                        {
                            // Clean activity description from any display symbols
                            string cleanDescription = entry.activity;
                            // Remove any sync icons that might have been added for display
                            while (cleanDescription.StartsWith("⚠ ") || cleanDescription.StartsWith("✓ ") || 
                                   cleanDescription.StartsWith("✓") || cleanDescription.StartsWith("⚠"))
                            {
                                if (cleanDescription.StartsWith("⚠ ") || cleanDescription.StartsWith("✓ "))
                                    cleanDescription = cleanDescription.Substring(2);
                                else if (cleanDescription.StartsWith("⚠") || cleanDescription.StartsWith("✓"))
                                    cleanDescription = cleanDescription.Substring(1);
                            }
                            
                            unsyncedActivities.Add(new UnsyncedActivity
                            {
                                StartTime = start,
                                EndTime = end,
                                Type = entry.type,
                                Description = cleanDescription.Trim(),
                                OriginalLogEntry = entry.originalLine
                            });
                        }
                        
                        prevTime = end;
                    }
                }
            }
            
            return unsyncedActivities;
        }
        
        private void UpdateLogEntryToSynced(string originalEntry, string clockifyId)
        {
            string logFilePath = GetLogFilePath();
            if (!File.Exists(logFilePath)) return;
            
            var lines = File.ReadAllLines(logFilePath).ToList();
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == originalEntry)
                {
                    string updatedLine;
                    if (lines[i].Contains("[LOCAL]"))
                    {
                        // Replace [LOCAL] with [SYNCED] and add Clockify ID
                        updatedLine = lines[i].Replace("[LOCAL]", "[SYNCED]") + $" [CID:{clockifyId}]";
                    }
                    else
                    {
                        // Add [SYNCED] flag and Clockify ID to entry without flag
                        int timestampEnd = lines[i].IndexOf(']');
                        if (timestampEnd > 0)
                        {
                            string timestamp = lines[i].Substring(0, timestampEnd + 1);
                            string rest = lines[i].Substring(timestampEnd + 1).Trim();
                            updatedLine = $"{timestamp} [SYNCED] {rest} [CID:{clockifyId}]";
                        }
                        else
                        {
                            updatedLine = lines[i] + $" [SYNCED] [CID:{clockifyId}]";
                        }
                    }
                    lines[i] = updatedLine;
                    break;
                }
            }
            
            File.WriteAllLines(logFilePath, lines);
        }
        
        private class UnsyncedActivity
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string Type { get; set; } = "";
            public string Description { get; set; } = "";
            public string OriginalLogEntry { get; set; } = "";
        }



        private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
        {
            if (isTimerRunning)
            {
                StopTimer();
            }
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Stop timer when application closes
            if (isTimerRunning)
            {
                StopTimer();
            }
            base.OnFormClosed(e);
        }
        #endregion

        #region Methods
        private void ShowIntervalPopup()
        {
            Form intervalForm = new Form
            {
                Width = 650,
                Height = isTimerRunning ? 380 : 320,
                Text = "Activity Reminder",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                TopMost = true
            };
            
            Label titleLabel = new Label
            {
                Text = "⏰ Activity Reminder",
                Location = new Point(20, 20),
                Size = new Size(590, 40),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            string statusMessage;
            if (isTimerRunning)
            {
                var elapsed = DateTime.Now - timerStartTime;
                string elapsedStr = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                statusMessage = $"⏱️ Timer is currently running\n\n" +
                               $"Activity: {currentActivityDescription}\n" +
                               $"Type: {(string.IsNullOrEmpty(currentActivityType) ? "General" : currentActivityType)}\n" +
                               $"Elapsed Time: {elapsedStr}\n" +
                               $"Started: {timerStartTime:HH:mm}\n\n" +
                               $"Would you like to continue or start a new activity?";
            }
            else if (isTimerPausedForExclude)
            {
                statusMessage = $"☕ Timer is paused for break time\n\n" +
                               $"Paused Activity: {pausedActivityDescription}\n" +
                               $"Type: {(string.IsNullOrEmpty(pausedActivityType) ? "General" : pausedActivityType)}\n\n" +
                               $"Timer will resume automatically after break.\n" +
                               $"Would you like to start a different activity?";
            }
            else
            {
                statusMessage = $"💤 No timer is currently running\n\n" +
                               $"It's time to track your activity!\n" +
                               $"What are you working on right now?";
            }
            
            Label messageLabel = new Label
            {
                Text = statusMessage,
                Location = new Point(20, 80),
                Size = new Size(590, isTimerRunning ? 180 : 140),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12),
                TextAlign = ContentAlignment.TopLeft
            };
            
            // Buttons
            int buttonY = isTimerRunning ? 280 : 240;
            
            Button inputButton = new Button
            {
                Text = isTimerRunning ? "New Activity" : "Start Timer",
                Size = new Size(140, 45),
                Location = new Point(180, buttonY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            inputButton.FlatAppearance.BorderSize = 0;
            inputButton.Click += (s, e) => {
                intervalForm.Close();
                ShowFullScreenInput();
            };
            
            Button continueButton = new Button
            {
                Text = "Continue",
                Size = new Size(120, 45),
                Location = new Point(330, buttonY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12)
            };
            continueButton.FlatAppearance.BorderSize = 0;
            continueButton.Click += (s, e) => intervalForm.Close();
            
            Button snoozeButton = new Button
            {
                Text = "Snooze 15m",
                Size = new Size(120, 45),
                Location = new Point(460, buttonY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11)
            };
            snoozeButton.FlatAppearance.BorderSize = 0;
            snoozeButton.Click += (s, e) => {
                // Snooze for 15 minutes
                popupTimer.Stop();
                popupTimer.Interval = 15 * 60 * 1000; // 15 minutes
                popupTimer.Start();
                intervalForm.Close();
                trayIcon.ShowBalloonTip(2000, "Reminder Snoozed", "Next reminder in 15 minutes", ToolTipIcon.Info);
            };
            
            intervalForm.Controls.AddRange(new Control[] { 
                titleLabel, messageLabel, inputButton, continueButton, snoozeButton 
            });
            
            intervalForm.ShowDialog();
            
            // Reset popup timer to normal interval after dialog closes
            popupTimer.Stop();
            popupTimer.Interval = popupIntervalInMinutes * 60 * 1000;
            popupTimer.Start();
        }
        
        private async void ShowFullScreenInput()
        {
            // Refresh dropdown items from all sources
            cmbType.Items.Clear();
            var uniqueTypes = await GetUniqueTypesFromAllSourcesAsync();
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

        private async Task<List<string>> GetUniqueTypesFromAllSourcesAsync()
        {
            var allTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // 1. Load from default file
            LoadDefaultActivityTypes(allTypes);
            
            // 2. Load from log history
            LoadTypesFromLog(allTypes);
            
            // 3. Load from Clockify if connected
            if (IsClockifyConnected())
            {
                await LoadTypesFromClockifyAsync(allTypes);
            }
            
            return allTypes.OrderBy(t => t).ToList();
        }
        
        private void LoadDefaultActivityTypes(HashSet<string> types)
        {
            try
            {
                string defaultTypesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_activity_types.txt");
                if (File.Exists(defaultTypesPath))
                {
                    var lines = File.ReadAllLines(defaultTypesPath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            types.Add(line.Trim());
                    }
                }
            }
            catch { }
        }
        
        private void LoadTypesFromLog(HashSet<string> types)
        {
            try
            {
                string logFilePath = GetLogFilePath();
                if (!File.Exists(logFilePath)) return;
                
                string[] lines = File.ReadAllLines(logFilePath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                    {
                        string rest = line.Substring(timestampEndIndex + 2).Trim();
                        
                        // Remove sync flags
                        if (rest.StartsWith("[SYNCED]") || rest.StartsWith("[LOCAL]"))
                        {
                            int flagEnd = rest.IndexOf(']', 1);
                            if (flagEnd != -1) rest = rest.Substring(flagEnd + 1).Trim();
                        }
                        
                        int pipeIndex = rest.IndexOf('|');
                        if (pipeIndex > 0)
                        {
                            string type = rest.Substring(0, pipeIndex).Trim();
                            if (!string.IsNullOrEmpty(type))
                                types.Add(type);
                        }
                    }
                }
            }
            catch { }
        }
        
        private async Task LoadTypesFromClockifyAsync(HashSet<string> types)
        {
            try
            {
                var tasks = await clockifyService.GetTasksAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId);
                foreach (var task in tasks)
                {
                    if (!string.IsNullOrEmpty(task.Name))
                        types.Add(task.Name);
                }
            }
            catch { }
        }
        
        private async Task<bool> CanCreateTaskInClockifyAsync()
        {
            if (!IsClockifyConnected()) return false;
            
            try
            {
                // Try to create a test task to check permission
                var testTask = await clockifyService.CreateTaskAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId, "__TEST_PERMISSION__");
                if (testTask != null)
                {
                    // Delete the test task immediately
                    await clockifyService.DeleteTimeEntryAsync(config.ClockifyWorkspaceId, testTask.Id);
                    return true;
                }
            }
            catch { }
            return false;
        }
        
        private bool IsClockifyConnected()
        {
            return !string.IsNullOrEmpty(config.ClockifyApiKey) && 
                   !string.IsNullOrEmpty(config.ClockifyWorkspaceId) && 
                   !string.IsNullOrEmpty(config.ClockifyProjectId);
        }
        
        private List<string> GetUniqueTypesFromLog()
        {
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LoadTypesFromLog(types);
            return types.OrderBy(t => t).ToList();
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
        
        private async void StartTimer()
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
            
            // Check if user entered a new task type and has permission to create
            if (!string.IsNullOrEmpty(type) && IsClockifyConnected())
            {
                var existingTasks = await clockifyService.GetTasksAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId);
                bool taskExists = existingTasks.Any(t => t.Name.Equals(type, StringComparison.OrdinalIgnoreCase));
                
                if (!taskExists && config.ClockifyAutoCreateTasks)
                {
                    // Task will be created automatically in StartClockifyTimerAsync
                    trayIcon.ShowBalloonTip(2000, "New Task", $"Creating new task: {type}", ToolTipIcon.Info);
                }
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
            
            // Start Clockify timer if configured
            _ = StartClockifyTimerAsync(type, activity);
            
            this.Hide();
        }
        
        private void StopTimer()
        {
            if (!isTimerRunning) return;
            
            DateTime endTime = DateTime.Now;
            elapsedTime = endTime - timerStartTime;
            
            // Save to log with sync flag and Clockify ID
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string syncFlag = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? "[SYNCED]" : "[LOCAL]";
            string clockifyIdPart = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? $" [CID:{currentClockifyTimeEntryId}]" : "";
            string logEntry = $"[{endTime.ToString(CultureInfo.InvariantCulture)}] {syncFlag} {typePart}{currentActivityDescription}{clockifyIdPart}";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            var duration = endTime - timerStartTime;
            string durationStr = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
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
                $"Activity logged: {durationStr}\n{activityDesc}", ToolTipIcon.Info);
                
            // Stop Clockify timer if running
            _ = StopClockifyTimerAsync();
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
                            
                            // Extract sync flag
                            bool isSynced = false;
                            if (rest.StartsWith("[SYNCED]"))
                            {
                                isSynced = true;
                                rest = rest.Substring(8).Trim(); // Remove "[SYNCED] "
                            }
                            else if (rest.StartsWith("[LOCAL]"))
                            {
                                rest = rest.Substring(7).Trim(); // Remove "[LOCAL] "
                            }
                            
                            // Remove Clockify ID if present for display
                            int cidIndex = rest.IndexOf(" [CID:");
                            if (cidIndex != -1)
                            {
                                int endIndex = rest.IndexOf("]", cidIndex);
                                if (endIndex != -1)
                                {
                                    rest = rest.Substring(0, cidIndex) + rest.Substring(endIndex + 1);
                                }
                            }
                            
                            string type = "";
                            string activity = rest;
                            int pipeIndex = rest.IndexOf('|');
                            if (pipeIndex > 0)
                            {
                                type = rest.Substring(0, pipeIndex).Trim();
                                activity = rest.Substring(pipeIndex + 1).Trim();
                            }
                            
                            // Store sync status separately for display only
                            // Don't modify the actual activity text
                            
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
                        DateTime? prevTime = null;
                        
                        // Use actual first entry time as starting point, not fixed 8:00 AM
                        if (typeEntries.Count > 0)
                        {
                            prevTime = typeEntries.First().time;
                        }

                        foreach (var entry in typeEntries)
                        {
                            DateTime start = prevTime ?? entry.time;
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
                                // Clean activity text from any existing sync icons for display
                                string cleanActivity = seg.activity;
                                if (cleanActivity.StartsWith("✓ ") || cleanActivity.StartsWith("⚠ "))
                                    cleanActivity = cleanActivity.Substring(2);
                                
                                // Determine sync status from the original log entry
                                bool isActivitySynced = false;
                                string activityLogPath = GetLogFilePath();
                                if (File.Exists(activityLogPath))
                                {
                                    var logLines = File.ReadAllLines(activityLogPath);
                                    foreach (var logLine in logLines)
                                    {
                                        if (logLine.Contains(cleanActivity) && (logLine.Contains("[SYNCED]") || logLine.Contains("[CID:")))
                                        {
                                            isActivitySynced = true;
                                            break;
                                        }
                                    }
                                }
                                
                                // Separate sync status from activity text
                                string syncIcon = isActivitySynced ? "✓" : "⚠";
                                string sub = $"     [{startStr} - {endStr} | {FormatDuration(seg.dur)}] [{syncIcon}] {cleanActivity}";
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
        

        

        
        private (DateTime timestamp, string type, string activity)? ParseLogEntry(string logEntry)
        {
            try
            {
                int timestampEndIndex = logEntry.IndexOf(']');
                if (timestampEndIndex <= 0) return null;
                
                string timestampStr = logEntry.Substring(1, timestampEndIndex - 1);
                if (!DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                    return null;
                
                string rest = logEntry.Substring(timestampEndIndex + 2).Trim();
                
                // Remove sync flag if present
                if (rest.StartsWith("[SYNCED]"))
                {
                    rest = rest.Substring(8).Trim();
                }
                else if (rest.StartsWith("[LOCAL]"))
                {
                    rest = rest.Substring(7).Trim();
                }
                
                // Remove Clockify ID if present
                int cidIndex = rest.IndexOf(" [CID:");
                if (cidIndex != -1)
                {
                    int endIndex = rest.IndexOf("]", cidIndex);
                    if (endIndex != -1)
                    {
                        rest = rest.Substring(0, cidIndex) + rest.Substring(endIndex + 1);
                    }
                }
                
                string type = "";
                string activity = rest.Trim();
                
                int pipeIndex = rest.IndexOf('|');
                if (pipeIndex > 0)
                {
                    type = rest.Substring(0, pipeIndex).Trim();
                    activity = rest.Substring(pipeIndex + 1).Trim();
                }
                
                return (timestamp, type, activity);
            }
            catch
            {
                return null;
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
                        DateTime? prevTime = null;
                        
                        // Use actual first entry time as starting point
                        if (typeEntries.Count > 0)
                        {
                            prevTime = typeEntries.First().timestamp;
                        }
                        
                        foreach (var entry in typeEntries)
                        {
                            DateTime start = prevTime ?? entry.timestamp;
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
            
            // Initialize popup interval timer
            popupTimer.Stop();
            popupIntervalInMinutes = config.IntervalHours * 60;
            popupTimer.Interval = popupIntervalInMinutes * 60 * 1000; // Convert to milliseconds
            popupTimer.Tick -= PopupTimer_Tick;
            popupTimer.Tick += PopupTimer_Tick;
            popupTimer.Start();
            
            // Initialize Clockify check timer
            clockifyCheckTimer = new Timer();
            clockifyCheckTimer.Interval = 10000; // Check every 10 seconds
            clockifyCheckTimer.Tick += ClockifyCheckTimer_Tick;
            clockifyCheckTimer.Start();
            
            // Get Clockify user ID
            _ = Task.Run(async () => {
                if (!string.IsNullOrEmpty(config.ClockifyApiKey))
                {
                    clockifyService.SetApiKey(config.ClockifyApiKey);
                    var user = await clockifyService.GetCurrentUserAsync();
                    if (user != null)
                        clockifyUserId = user.Id;
                }
            });
            
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
                
                // Check if timer has been running for 24+ hours (Clockify limit)
                var totalRunTime = now - timerStartTime;
                if (totalRunTime.TotalHours >= 24)
                {
                    AutoBreakAt24Hours();
                }
            }
            
            UpdateTrayIcon();
        }
        
        private void PopupTimer_Tick(object? sender, EventArgs e)
        {
            // Skip if popups are disabled for today
            if (dontShowPopupToday) return;
            
            // Show interval popup regardless of timer state
            ShowIntervalPopup();
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
            
            // Save current activity up to now with sync flag and Clockify ID
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string syncFlag = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? "[SYNCED]" : "[LOCAL]";
            string clockifyIdPart = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? $" [CID:{currentClockifyTimeEntryId}]" : "";
            string logEntry = $"[{now.ToString(CultureInfo.InvariantCulture)}] {syncFlag} {typePart}{currentActivityDescription}{clockifyIdPart}";
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
            
            var duration = now - timerStartTime;
            string durationStr = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            trayIcon.ShowBalloonTip(3000, $"Timer Paused - {periodName}", 
                $"Activity saved: {durationStr}\nTimer will resume after {periodName}", 
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
            
            // Save the activity from start time to midnight with sync flag and Clockify ID
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string syncFlag = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? "[SYNCED]" : "[LOCAL]";
            string clockifyIdPart = !string.IsNullOrEmpty(currentClockifyTimeEntryId) ? $" [CID:{currentClockifyTimeEntryId}]" : "";
            string logEntry = $"[{midnight.ToString(CultureInfo.InvariantCulture)}] {syncFlag} {typePart}{currentActivityDescription}{clockifyIdPart}";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            // Calculate duration for notification
            var duration = midnight - timerStartTime;
            string durationStr = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            
            // Restart timer from midnight with same activity
            timerStartTime = midnight;
            
            // Show notification about auto-split
            trayIcon.ShowBalloonTip(3000, "Activity Auto-Split", 
                $"Activity split at midnight: {durationStr} logged\nTimer continues for: {currentActivityDescription}", 
                ToolTipIcon.Info);
            
            // Refresh history display
            LoadLogHistory();
        }
        
        private async void AutoBreakAt24Hours()
        {
            if (!isTimerRunning) return;
            
            DateTime now = DateTime.Now;
            DateTime breakPoint = timerStartTime.AddHours(24); // Exactly 24 hours from start
            
            // Stop current Clockify timer first
            await StopClockifyTimerAsync();
            
            // Save current activity up to 24-hour mark
            string typePart = string.IsNullOrEmpty(currentActivityType) ? "" : $"{currentActivityType} | ";
            string logEntry = $"[{breakPoint.ToString(CultureInfo.InvariantCulture)}] [LOCAL] {typePart}{currentActivityDescription} (24h auto-break)";
            string logFilePath = GetLogFilePath();
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            
            // Store current activity details for restart
            string savedType = currentActivityType;
            string savedDescription = currentActivityDescription;
            
            // Calculate duration for notification
            var duration = breakPoint - timerStartTime;
            string durationStr = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            
            // Show notification about 24-hour break
            trayIcon.ShowBalloonTip(4000, "24-Hour Auto-Break", 
                $"Timer auto-stopped after 24 hours: {durationStr}\nRestarting new timer for: {savedDescription}", 
                ToolTipIcon.Info);
            
            // Restart timer with same activity from break point
            currentActivityType = savedType;
            currentActivityDescription = savedDescription;
            timerStartTime = breakPoint;
            currentClockifyTimeEntryId = "";
            
            // Start new Clockify timer
            _ = StartClockifyTimerAsync(savedType, savedDescription);
            
            // Refresh history display
            LoadLogHistory();
        }
        
        private void UpdateTrayIcon()
        {
            // Update Stop Timer menu visibility in Timer submenu
            var timerMenu = trayMenu.Items[0] as ToolStripMenuItem; // Timer menu is first
            if (timerMenu != null && timerMenu.DropDownItems.Count > 1)
            {
                var stopTimerItem = timerMenu.DropDownItems[1]; // Stop Timer is second in submenu
                stopTimerItem.Visible = isTimerRunning || isTimerPausedForExclude;
            }
            
            // Update Dashboard menu visibility based on Clockify connection
            UpdateDashboardMenuVisibility();
            
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
        
        private async Task StartClockifyTimerAsync(string type, string activity)
        {
            if (string.IsNullOrEmpty(config.ClockifyApiKey) || string.IsNullOrEmpty(config.ClockifyWorkspaceId) || string.IsNullOrEmpty(config.ClockifyProjectId))
                return;
                
            try
            {
                string taskId = null;
                
                // Auto-create task if enabled
                if (config.ClockifyAutoCreateTasks && !string.IsNullOrEmpty(type))
                {
                    var task = await clockifyService.CreateTaskAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId, type);
                    taskId = task?.Id;
                }
                
                var timeEntry = await clockifyService.StartTimeEntryAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId, taskId, activity);
                if (timeEntry != null)
                {
                    currentClockifyTimeEntryId = timeEntry.Id;
                }
            }
            catch { /* Ignore Clockify errors */ }
        }
        
        private async Task StopClockifyTimerAsync()
        {
            if (string.IsNullOrEmpty(currentClockifyTimeEntryId) || string.IsNullOrEmpty(config.ClockifyWorkspaceId))
                return;
                
            int retryCount = 0;
            const int maxRetries = 3;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    var result = await clockifyService.StopTimeEntryAsync(config.ClockifyWorkspaceId, currentClockifyTimeEntryId);
                    if (result != null)
                    {
                        // Successfully stopped
                        currentClockifyTimeEntryId = "";
                        return;
                    }
                    else
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await Task.Delay(1000 * retryCount); // Progressive delay
                            continue;
                        }
                        
                        // Final failure - keep the ID for manual sync later
                        trayIcon.ShowBalloonTip(3000, "Clockify Sync Issue", 
                            "Failed to stop Clockify timer after retries. Activity saved locally and will sync later.", 
                            ToolTipIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        await Task.Delay(1000 * retryCount);
                        continue;
                    }
                    
                    // Final failure - keep the ID for manual sync later
                    trayIcon.ShowBalloonTip(3000, "Clockify Connection Error", 
                        $"Network error stopping Clockify timer. Activity saved locally.\nError: {ex.Message}", 
                        ToolTipIcon.Warning);
                    return;
                }
            }
        }
        
        private async void ClockifyCheckTimer_Tick(object? sender, EventArgs e)
        {
            // DISABLED: Auto-stop feature removed to prevent unexpected timer stops
            // Users reported timer stopping unexpectedly due to network issues or Clockify API problems
            return;
        }
        
        private string ExtractClockifyIdFromLogEntry(string logEntry)
        {
            try
            {
                int cidIndex = logEntry.IndexOf("[CID:");
                if (cidIndex == -1) return "";
                
                int startIndex = cidIndex + 5;
                int endIndex = logEntry.IndexOf("]", startIndex);
                if (endIndex == -1) return "";
                
                return logEntry.Substring(startIndex, endIndex - startIndex);
            }
            catch
            {
                return "";
            }
        }
        
        private async Task<bool> UpdateClockifyTimeEntryAsync(string clockifyId, string type, string activity, DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrEmpty(config.ClockifyApiKey) || string.IsNullOrEmpty(config.ClockifyWorkspaceId) || string.IsNullOrEmpty(config.ClockifyProjectId))
                return false;
                
            try
            {
                string taskId = null;
                
                if (config.ClockifyAutoCreateTasks && !string.IsNullOrEmpty(type))
                {
                    var task = await clockifyService.CreateTaskAsync(config.ClockifyWorkspaceId, config.ClockifyProjectId, type);
                    taskId = task?.Id;
                }
                
                var updatedEntry = await clockifyService.UpdateTimeEntryAsync(
                    config.ClockifyWorkspaceId, 
                    clockifyId, 
                    config.ClockifyProjectId, 
                    taskId, 
                    activity, 
                    startTime, 
                    endTime);
                    
                return updatedEntry != null;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<bool> DeleteClockifyTimeEntryAsync(string clockifyId)
        {
            if (string.IsNullOrEmpty(config.ClockifyApiKey) || string.IsNullOrEmpty(config.ClockifyWorkspaceId))
                return false;
                
            try
            {
                return await clockifyService.DeleteTimeEntryAsync(config.ClockifyWorkspaceId, clockifyId);
            }
            catch
            {
                return false;
            }
        }
        
        private void OnDashboardClicked(object? sender, EventArgs e)
        {
            var dashboardForm = new Forms.DashboardForm();
            dashboardForm.Show();
        }
        
        private async Task ShowDashboard()
        {
            if (!IsClockifyConnected())
            {
                ShowDarkMessageBox("Please configure Clockify settings first to view dashboard.", "Clockify Not Connected");
                return;
            }
            
            Form dashboardForm = new Form
            {
                Width = 700,
                Height = 600,
                Text = "Dashboard - Activity Summary",
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            
            Label loadingLabel = new Label
            {
                Text = "Loading dashboard data...",
                Location = new Point(20, 20),
                Size = new Size(640, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 12)
            };
            dashboardForm.Controls.Add(loadingLabel);
            
            dashboardForm.Show();
            
            try
            {
                var dashboardData = await clockifyService.GetDashboardDataAsync(config.ClockifyWorkspaceId, clockifyUserId);
                var localData = GetLocalDashboardData();
                
                dashboardForm.Controls.Clear();
                
                // Today's Summary
                var todayLabel = new Label
                {
                    Text = "📅 Today's Summary",
                    Location = new Point(20, 20),
                    Size = new Size(640, 25),
                    ForeColor = Color.FromArgb(100, 200, 255),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                };
                
                var todayStats = new Label
                {
                    Text = $"Local: {localData.TodayHours}\nClockify: {CalculateHours(dashboardData.TodayEntries)}\nEntries: {localData.TodayEntries} local, {dashboardData.TodayEntries.Count} Clockify",
                    Location = new Point(20, 50),
                    Size = new Size(640, 60),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };
                
                // This Week Summary
                var weekLabel = new Label
                {
                    Text = "📊 This Week Summary",
                    Location = new Point(20, 130),
                    Size = new Size(640, 25),
                    ForeColor = Color.FromArgb(100, 200, 255),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                };
                
                var weekStats = new Label
                {
                    Text = $"Local: {localData.WeekHours}\nClockify: {CalculateHours(dashboardData.WeekEntries)}\nEntries: {localData.WeekEntries} local, {dashboardData.WeekEntries.Count} Clockify",
                    Location = new Point(20, 160),
                    Size = new Size(640, 60),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };
                
                // Top Activity
                var topLabel = new Label
                {
                    Text = "🏆 Top Activity Type",
                    Location = new Point(20, 240),
                    Size = new Size(640, 25),
                    ForeColor = Color.FromArgb(100, 200, 255),
                    Font = new Font("Segoe UI", 12, FontStyle.Bold)
                };
                
                var topStats = new Label
                {
                    Text = $"Most used: {localData.TopActivityType}\nTime spent: {localData.TopActivityHours}",
                    Location = new Point(20, 270),
                    Size = new Size(640, 40),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10)
                };
                
                // Chart Panel
                var chartPanel = new Panel
                {
                    Location = new Point(20, 320),
                    Size = new Size(640, 200),
                    BackColor = Color.FromArgb(40, 40, 40)
                };
                chartPanel.Paint += (s, pe) => {
                    var typeMinutes = new Dictionary<string, int>();
                    foreach (var line in File.Exists(GetLogFilePath()) ? File.ReadAllLines(GetLogFilePath()) : new string[0])
                    {
                        var parsed = ParseLogEntry(line);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.Value.type))
                        {
                            typeMinutes[parsed.Value.type] = typeMinutes.GetValueOrDefault(parsed.Value.type, 0) + 30;
                        }
                    }
                    DrawSimpleChart(pe.Graphics, chartPanel.ClientRectangle, typeMinutes, "Activity Types Distribution (This Week)");
                };
                
                Button closeButton = new Button
                {
                    Text = "Close",
                    Size = new Size(80, 30),
                    Location = new Point(580, 530),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                closeButton.FlatAppearance.BorderSize = 0;
                closeButton.Click += (s, e) => dashboardForm.Close();
                
                dashboardForm.Controls.AddRange(new Control[] {
                    todayLabel, todayStats, weekLabel, weekStats, topLabel, topStats, chartPanel, closeButton
                });
            }
            catch
            {
                dashboardForm.Controls.Clear();
                Label errorLabel = new Label
                {
                    Text = "Failed to load dashboard data. Please check your Clockify connection.",
                    Location = new Point(20, 20),
                    Size = new Size(640, 60),
                    ForeColor = Color.FromArgb(220, 53, 69),
                    Font = new Font("Segoe UI", 10)
                };
                
                Button errorCloseButton = new Button
                {
                    Text = "Close",
                    Size = new Size(80, 30),
                    Location = new Point(580, 530),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White
                };
                errorCloseButton.FlatAppearance.BorderSize = 0;
                errorCloseButton.Click += (s, e) => dashboardForm.Close();
                
                dashboardForm.Controls.Add(errorLabel);
                dashboardForm.Controls.Add(errorCloseButton);
            }
        }
        
        private (string TodayHours, string WeekHours, int TodayEntries, int WeekEntries, string TopActivityType, string TopActivityHours) GetLocalDashboardData()
        {
            try
            {
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                
                string logFilePath = GetLogFilePath();
                if (!File.Exists(logFilePath)) return ("00:00:00", "00:00:00", 0, 0, "None", "00:00:00");
                
                var lines = File.ReadAllLines(logFilePath);
                var todayMinutes = 0;
                var weekMinutes = 0;
                var todayCount = 0;
                var weekCount = 0;
                var typeMinutes = new Dictionary<string, int>();
                
                foreach (var line in lines)
                {
                    var parsed = ParseLogEntry(line);
                    if (parsed == null) continue;
                    
                    var (timestamp, type, activity) = parsed.Value;
                    
                    if (timestamp.Date == today)
                    {
                        todayMinutes += 30; // Assume 30 min average
                        todayCount++;
                    }
                    
                    if (timestamp.Date >= weekStart)
                    {
                        weekMinutes += 30;
                        weekCount++;
                        
                        if (!string.IsNullOrEmpty(type))
                        {
                            typeMinutes[type] = typeMinutes.GetValueOrDefault(type, 0) + 30;
                        }
                    }
                }
                
                var topType = typeMinutes.OrderByDescending(x => x.Value).FirstOrDefault();
                
                return (
                    FormatMinutes(todayMinutes),
                    FormatMinutes(weekMinutes),
                    todayCount,
                    weekCount,
                    topType.Key ?? "None",
                    FormatMinutes(topType.Value)
                );
            }
            catch
            {
                return ("00:00:00", "00:00:00", 0, 0, "None", "00:00:00");
            }
        }
        
        private string CalculateHours(List<ClockifyTimeEntry> entries)
        {
            try
            {
                var totalMinutes = 0;
                foreach (var entry in entries)
                {
                    if (entry.TimeInterval?.Start != null && entry.TimeInterval?.End != null)
                    {
                        if (DateTime.TryParse(entry.TimeInterval.Start, out var start) && DateTime.TryParse(entry.TimeInterval.End, out var end))
                        {
                            var duration = end - start;
                            totalMinutes += (int)duration.TotalMinutes;
                        }
                    }
                }
                return FormatMinutes(totalMinutes);
            }
            catch
            {
                return "00:00:00";
            }
        }
        
        private string FormatMinutes(int totalMinutes)
        {
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{hours:D2}:{minutes:D2}:00";
        }
        
        private void UpdateDashboardMenuVisibility()
        {
            // Find Data & Reports menu (index 1)
            var dataMenu = trayMenu.Items[1] as ToolStripMenuItem;
            if (dataMenu != null && dataMenu.DropDownItems.Count > 0)
            {
                // Dashboard is first item in Data & Reports submenu
                var dashboardItem = dataMenu.DropDownItems[0];
                bool shouldShowDashboard = IsClockifyConnected();
                dashboardItem.Visible = shouldShowDashboard;
                
                // Update tooltip based on connection status
                if (dashboardItem is ToolStripMenuItem menuItem)
                {
                    if (shouldShowDashboard)
                    {
                        menuItem.ToolTipText = "View activity dashboard and statistics";
                    }
                    else
                    {
                        menuItem.ToolTipText = "Dashboard requires Clockify connection (configure in Settings)";
                    }
                }
            }
        }
        
        private void DrawSimpleChart(Graphics g, Rectangle bounds, Dictionary<string, int> data, string title)
        {
            if (data == null || data.Count == 0) return;
            
            // Chart area
            var chartRect = new Rectangle(bounds.X + 10, bounds.Y + 30, bounds.Width - 20, bounds.Height - 40);
            
            // Title
            using (var titleFont = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(100, 200, 255)))
            {
                g.DrawString(title, titleFont, titleBrush, bounds.X + 10, bounds.Y + 5);
            }
            
            // Simple bar chart
            var maxValue = data.Values.Max();
            if (maxValue == 0) return;
            
            var barWidth = chartRect.Width / Math.Max(data.Count, 1);
            var x = chartRect.X;
            
            var colors = new Color[] {
                Color.FromArgb(54, 162, 235),
                Color.FromArgb(255, 99, 132),
                Color.FromArgb(255, 205, 86),
                Color.FromArgb(75, 192, 192),
                Color.FromArgb(153, 102, 255)
            };
            
            int colorIndex = 0;
            foreach (var item in data.Take(5)) // Show top 5
            {
                var barHeight = (int)((double)item.Value / maxValue * chartRect.Height * 0.8);
                var barRect = new Rectangle(x + 5, chartRect.Bottom - barHeight, barWidth - 10, barHeight);
                
                using (var brush = new SolidBrush(colors[colorIndex % colors.Length]))
                {
                    g.FillRectangle(brush, barRect);
                }
                
                // Label
                using (var labelFont = new Font("Segoe UI", 8))
                using (var labelBrush = new SolidBrush(Color.White))
                {
                    var labelText = item.Key.Length > 8 ? item.Key.Substring(0, 8) + "..." : item.Key;
                    var labelSize = g.MeasureString(labelText, labelFont);
                    g.DrawString(labelText, labelFont, labelBrush, 
                        x + (barWidth - labelSize.Width) / 2, chartRect.Bottom + 5);
                }
                
                x += barWidth;
                colorIndex++;
            }
        }
        #endregion
    }
}