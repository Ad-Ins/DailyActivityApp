using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialogs
{
    public partial class EditActivityDialog : Form
    {
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public string ActivityType { get; private set; } = "";
        public string ActivityText { get; private set; } = "";

        private DateTimePicker dtpStartTime = null!;
        private DateTimePicker dtpEndTime = null!;
        private TextBox txtType = null!;
        private TextBox txtActivity = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        public EditActivityDialog(DateTime startTime, DateTime endTime, string type, string activity)
        {
            StartTime = startTime;
            EndTime = endTime;
            ActivityType = type;
            ActivityText = activity;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(450, 280);
            this.Text = "Edit Activity";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.TopMost = true;
            this.ShowInTaskbar = true;

            // Start Time
            var lblStart = new Label
            {
                Text = "Start Time:",
                Location = new Point(20, 20),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };

            dtpStartTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(110, 18),
                Size = new Size(80, 25),
                Value = StartTime,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            // End Time
            var lblEnd = new Label
            {
                Text = "End Time:",
                Location = new Point(20, 55),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };

            dtpEndTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(110, 53),
                Size = new Size(80, 25),
                Value = EndTime,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };

            // Type
            var lblType = new Label
            {
                Text = "Type:",
                Location = new Point(20, 90),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };

            txtType = new TextBox
            {
                Text = ActivityType,
                Location = new Point(110, 88),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Activity
            var lblActivity = new Label
            {
                Text = "Activity:",
                Location = new Point(20, 125),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };

            txtActivity = new TextBox
            {
                Text = ActivityText,
                Location = new Point(110, 123),
                Size = new Size(300, 60),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true
            };

            // Buttons
            btnSave = new Button
            {
                Text = "Save",
                Size = new Size(80, 30),
                Location = new Point(250, 200),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Location = new Point(340, 200),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[] {
                lblStart, dtpStartTime, lblEnd, dtpEndTime,
                lblType, txtType, lblActivity, txtActivity,
                btnSave, btnCancel
            });
            
            this.Shown += EditActivityDialog_Shown;
        }

        private void EditActivityDialog_Shown(object? sender, EventArgs e)
        {
            this.BringToFront();
            this.Activate();
            this.Focus();
            txtActivity.Focus();
        }
        
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Combine original date with new time
            DateTime newStartTime = new DateTime(StartTime.Year, StartTime.Month, StartTime.Day, 
                dtpStartTime.Value.Hour, dtpStartTime.Value.Minute, 0);
            DateTime newEndTime = new DateTime(EndTime.Year, EndTime.Month, EndTime.Day, 
                dtpEndTime.Value.Hour, dtpEndTime.Value.Minute, 0);

            if (newEndTime <= newStartTime)
            {
                MessageBox.Show("End time must be after start time!", "Invalid Time", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtActivity.Text))
            {
                MessageBox.Show("Activity cannot be empty!", "Invalid Activity", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            StartTime = newStartTime;
            EndTime = newEndTime;
            ActivityType = txtType.Text.Trim();
            ActivityText = txtActivity.Text.Trim();
        }
    }
}