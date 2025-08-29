using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialogs
{
    public partial class ExcludeTimeDialog : Form
    {
        public List<(TimeSpan start, TimeSpan end, string name)> ExcludeTimes { get; private set; } = new();

        private ListBox lstExcludeTimes = null!;
        private DateTimePicker dtpStartTime = null!;
        private DateTimePicker dtpEndTime = null!;
        private TextBox txtName = null!;
        private Button btnAdd = null!;
        private Button btnRemove = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        public ExcludeTimeDialog(List<(TimeSpan start, TimeSpan end, string name)> existingTimes)
        {
            ExcludeTimes = existingTimes.ToList();
            InitializeComponent();
            LoadExcludeTimes();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(450, 400);
            this.Text = "Exclude Time Periods";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.TopMost = true;

            // List of exclude times
            var lblList = new Label
            {
                Text = "Exclude Time Periods:",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                ForeColor = Color.White
            };

            lstExcludeTimes = new ListBox
            {
                Location = new Point(20, 45),
                Size = new Size(400, 120),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Add new exclude time section
            var lblAdd = new Label
            {
                Text = "Add New Exclude Period:",
                Location = new Point(20, 180),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            var lblName = new Label
            {
                Text = "Name:",
                Location = new Point(20, 210),
                Size = new Size(50, 20),
                ForeColor = Color.White
            };

            txtName = new TextBox
            {
                Location = new Point(80, 208),
                Size = new Size(120, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Break"
            };

            var lblStart = new Label
            {
                Text = "Start:",
                Location = new Point(220, 210),
                Size = new Size(40, 20),
                ForeColor = Color.White
            };

            dtpStartTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(265, 208),
                Size = new Size(70, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Value = DateTime.Today.AddHours(12) // Default 12:00
            };

            var lblEnd = new Label
            {
                Text = "End:",
                Location = new Point(345, 210),
                Size = new Size(30, 20),
                ForeColor = Color.White
            };

            dtpEndTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Location = new Point(380, 208),
                Size = new Size(70, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Value = DateTime.Today.AddHours(13) // Default 13:00
            };

            // Buttons
            btnAdd = new Button
            {
                Text = "Add",
                Size = new Size(60, 30),
                Location = new Point(20, 250),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += BtnAdd_Click;

            btnRemove = new Button
            {
                Text = "Remove",
                Size = new Size(70, 30),
                Location = new Point(90, 250),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Click += BtnRemove_Click;

            btnSave = new Button
            {
                Text = "Save",
                Size = new Size(80, 30),
                Location = new Point(270, 320),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            btnSave.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(80, 30),
                Location = new Point(360, 320),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[] {
                lblList, lstExcludeTimes, lblAdd, lblName, txtName,
                lblStart, dtpStartTime, lblEnd, dtpEndTime,
                btnAdd, btnRemove, btnSave, btnCancel
            });
        }

        private void LoadExcludeTimes()
        {
            lstExcludeTimes.Items.Clear();
            foreach (var time in ExcludeTimes)
            {
                lstExcludeTimes.Items.Add($"{time.name}: {time.start:hh\\:mm} - {time.end:hh\\:mm}");
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a name for the exclude period.", "Name Required");
                return;
            }

            var startTime = dtpStartTime.Value.TimeOfDay;
            var endTime = dtpEndTime.Value.TimeOfDay;

            if (endTime <= startTime)
            {
                MessageBox.Show("End time must be after start time.", "Invalid Time");
                return;
            }

            ExcludeTimes.Add((startTime, endTime, txtName.Text.Trim()));
            LoadExcludeTimes();

            // Reset form
            txtName.Text = "Break";
            dtpStartTime.Value = DateTime.Today.AddHours(12);
            dtpEndTime.Value = DateTime.Today.AddHours(13);
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (lstExcludeTimes.SelectedIndex >= 0)
            {
                ExcludeTimes.RemoveAt(lstExcludeTimes.SelectedIndex);
                LoadExcludeTimes();
            }
        }
    }
}