
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;
using ClosedXML.Excel;

namespace AdinersDailyActivityApp
{
    public class DailyActivityForm : Form
    {
        #region Fields

        private Label lblTitle;
        private TextBox txtActivity;
        private ListBox lstActivityHistory;
        private PictureBox logoPictureBox;
        private System.Windows.Forms.Timer popupTimer;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        //private ComboBox cmbActivityType;
        private ContextMenuStrip historyContextMenu;

        private DateTime appStartTime;
        private DateTime popupTime;
        private int popupIntervalInMinutes;
        private AppConfig config;

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
            // Inisialisasi komponen UI
            lblTitle = new Label();
            txtActivity = new TextBox();
            lstActivityHistory = new ListBox();
            logoPictureBox = new PictureBox();
            popupTimer = new System.Windows.Forms.Timer();

            // ComboBox tipe activity
//             //cmbActivityType = new ComboBox
           // {
            //    Font = new Font("Segoe UI", 16),
            //    DropDownStyle = ComboBoxStyle.DropDownList,
            //    Width = 180,
            //    Dock = DockStyle.Left,
            //    Margin = new Padding(0, 20, 0, 20)
            //};
//             //cmbActivityType.Items.AddRange(new object[] { "NON-JIRA", "Others", "JIRA-001" });
//             //cmbActivityType.SelectedIndex = 0;

            // Tray menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Input Activity Now", null, OnInputNowClicked);
            trayMenu.Items.Add("Export Log to Excel", null, OnExportLogClicked); 
            trayMenu.Items.Add("Set Interval...", null, OnSetIntervalClicked);
            // trayMenu.Items.Add("SIT/UAT Jira Connection...", null, OnJiraConnectionClicked);
            trayMenu.Items.Add("-"); // Separator
            trayMenu.Items.Add("Test Tray", null, OnTestTrayClicked);
            trayMenu.Items.Add("Exit", null, OnExitClicked);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            Icon trayAppIcon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

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
            historyContextMenu.Items.Add("Edit", null, OnEditHistoryClicked);

            // List activity history
            lstActivityHistory.ContextMenuStrip = historyContextMenu;
        }

        private void SetupForm()
        {
            // Pengaturan tampilan utama
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Opacity = 0.9;
            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.KeyDown += Form1_KeyDown;

            logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoPictureBox.Size = new Size(200, 60);
            logoPictureBox.Dock = DockStyle.Fill;
            LoadLogoImage();

            lblTitle.Text = "What are you working on?";
            lblTitle.Font = new Font("Arial", 24, FontStyle.Italic);
        }

        #endregion

        #region Event Handlers

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.WindowState = FormWindowState.Minimized;
            }

            if (e.Control && e.KeyCode == Keys.Enter)
            {
                SaveActivity();
            }
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            // Bersihkan resources dan keluar dari aplikasi
            trayIcon.Visible = false;
            Application.Exit();
        }
        
        private void OnTestTrayClicked(object sender, EventArgs e)
        {
            trayIcon.ShowBalloonTip(2000, "Tray Test", "Tray functionality is working properly", ToolTipIcon.Info);
        }

        private void OnInputNowClicked(object sender, EventArgs e)
        {
            ShowFullScreenInput();
        }

        private void OnExportLogClicked(object sender, EventArgs e)
        {
            ExportLogToExcel();
        }

        private void OnSetIntervalClicked(object sender, EventArgs e)
        {
            using (var setIntervalForm = new SetIntervalForm(config.IntervalHours))
            {
                if (setIntervalForm.ShowDialog() == DialogResult.OK)
                {
                    config.IntervalHours = setIntervalForm.IntervalHours;
                    config.Save();
                    LoadConfig();
                }
            }
        }

        private void OnJiraConnectionClicked(object sender, EventArgs e)
        {
            // TODO: Implement Jira connection settings form
            MessageBox.Show("Jira connection settings will be implemented in a future version.", "Info");
        }

        private void OnEditHistoryClicked(object sender, EventArgs e)
        {
            if (lstActivityHistory.SelectedItem != null)
            {
                string selectedActivity = lstActivityHistory.SelectedItem.ToString();
                txtActivity.Text = selectedActivity;
                // Remove the selected item from the history list
                lstActivityHistory.Items.Remove(selectedActivity);
                // Remove the selected item from the log file
                RemoveActivityFromLogFile(selectedActivity);
            }
        }

        private void popupTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;

            // Check for lunch popup
            if (!isLunchPopupShown && now.Hour == 12 && now.Minute >= 0 && now.Minute <= 15)
            {
                ShowLunchPopup();
                isLunchPopupShown = true;
                isLunchHandled = false;
            }

            // Reset lunch popup flag after 1 PM
            if (now.Hour > 13)
            {
                isLunchPopupShown = false;
            }

            // Regular activity popup
            TimeSpan timeSinceStart = now - appStartTime;
            TimeSpan timeSinceLastPopup = now - popupTime;

            if (timeSinceLastPopup.TotalMinutes >= popupIntervalInMinutes && now.Hour >= 9 && now.Hour <= 17)
            {
                ShowFullScreenInput();
                popupTime = DateTime.Now;
            }
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            // Save the activity before the session ends
            SaveActivity();
        }

        #endregion

        #region Methods

        private void ShowFullScreenInput()
        {
            // Reset the activity input
            txtActivity.Text = "";
            txtActivity.Focus();

            // Show the form in full screen
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

                // Save to log file
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);

                // Update activity history
                LoadLogHistory();

                // Reset last activity input time
                lastActivityInputTime = DateTime.Now;
            }

            // Minimize the form after saving
            this.WindowState = FormWindowState.Minimized;
        }

        private void LoadLogHistory()
        {
            lstActivityHistory.Items.Clear();

            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                // Display the log in reverse chronological order
                foreach (string line in lines.Reverse())
                {
                    lstActivityHistory.Items.Add(line);
                }
            }
        }

        private void RemoveActivityFromLogFile(string activityToRemove)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (File.Exists(logFilePath))
            {
                string[] lines = File.ReadAllLines(logFilePath);
                // Filter out the line containing the activity to remove
                var filteredLines = lines.Where(line => line != activityToRemove);
                // Write the filtered lines back to the log file
                File.WriteAllLines(logFilePath, filteredLines);
            }
        }

        private void ExportLogToExcel()
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.txt");
            if (!File.Exists(logFilePath))
            {
                MessageBox.Show("No activity log found.", "Error");
                return;
            }

            // Create a new Excel workbook
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Activity Log");

                // Add headers
                worksheet.Cell(1, 1).Value = "Timestamp";
                worksheet.Cell(1, 2).Value = "Activity";

                // Read activity log
                string[] lines = File.ReadAllLines(logFilePath);

                // Populate data
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // Split the log entry into timestamp and activity
                    int timestampEndIndex = line.IndexOf(']');
                    if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                    {
                        string timestamp = line.Substring(1, timestampEndIndex - 1);
                        string activity = line.Substring(timestampEndIndex + 2);

                        worksheet.Cell(i + 2, 1).Value = timestamp;
                        worksheet.Cell(i + 2, 2).Value = activity;
                    }
                    else
                    {
                        // If the log entry format is incorrect, put the whole line in the activity column
                        worksheet.Cell(i + 2, 2).Value = line;
                    }
                }

                // Save the Excel file
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_log.xlsx");
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
                    MinimizeBox = false
                };

                Label lunchLabel = new Label
                {
                    Text = "It's lunch time! Please record your lunch activity.",
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Height = 100
                };

                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Dock = DockStyle.Bottom,
                    Height = 30
                };

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
    }
}
