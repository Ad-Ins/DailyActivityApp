using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialog
{
    public class EditActivityDialog : Form
    {
        public string StartTime => txtStart.Text.Trim();
        public string EndTime => txtEnd.Text.Trim();
        public string ActivityType => cmbType.SelectedItem?.ToString() ?? "Others";
        public string Activity => txtActivity.Text.Trim();

        private TextBox txtStart, txtEnd, txtActivity;
        private ComboBox cmbType;
        private Label lblError;

        public EditActivityDialog(string start, string end, string type, string activity, bool fullscreen = false)
        {
            // --- FORM SETUP ---
            this.Text = "Edit Activity";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = true;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.Width = 700;
            this.Height = 420;

            // --- LAYOUT SETUP ---
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 6,
                ColumnCount = 2,
                Padding = new Padding(40, 30, 40, 30),
                BackColor = this.BackColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            // --- LABEL & INPUT STYLE ---
            Font labelFont = new Font("Segoe UI", 15F, FontStyle.Bold);
            Font inputFont = new Font("Segoe UI", 15F, FontStyle.Regular);
            Color labelColor = Color.WhiteSmoke;
            Color inputBack = Color.FromArgb(36, 36, 48);
            Color inputFore = Color.White;

            // --- START TIME ---
            var lblStart = new Label
            {
                Text = "Start Time (HH:mm)",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtStart = new TextBox
            {
                Text = start,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            // --- END TIME ---
            var lblEnd = new Label
            {
                Text = "End Time (HH:mm)",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtEnd = new TextBox
            {
                Text = end,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            // --- TYPE ---
            var lblType = new Label
            {
                Text = "Type",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            cmbType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill
            };
            cmbType.Items.AddRange(new object[] { "NON-JIRA", "Others", "JIRA-001" });
            cmbType.SelectedItem = type;
            if (cmbType.SelectedIndex < 0) cmbType.SelectedIndex = 0;

            // --- ACTIVITY ---
            var lblActivity = new Label
            {
                Text = "Activity",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtActivity = new TextBox
            {
                Text = activity,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            // --- ERROR LABEL ---
            lblError = new Label
            {
                ForeColor = Color.OrangeRed,
                Text = "",
                Font = new Font("Segoe UI", 12F, FontStyle.Italic),
                BackColor = this.BackColor,
                Dock = DockStyle.Fill,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // --- BUTTON PANEL ---
            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = this.BackColor,
                Height = 60
            };
            var btnOK = new Button
            {
                Text = "OK",
                Width = 140,
                Height = 44,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 140,
                Height = 44,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnPanel.Controls.Add(btnOK);
            btnPanel.Controls.Add(btnCancel);

            // --- ADD TO LAYOUT ---
            layout.Controls.Add(lblStart, 0, 0);
            layout.Controls.Add(txtStart, 1, 0);
            layout.Controls.Add(lblEnd, 0, 1);
            layout.Controls.Add(txtEnd, 1, 1);
            layout.Controls.Add(lblType, 0, 2);
            layout.Controls.Add(cmbType, 1, 2);
            layout.Controls.Add(lblActivity, 0, 3);
            layout.Controls.Add(txtActivity, 1, 3);
            layout.Controls.Add(lblError, 0, 4);
            layout.SetColumnSpan(lblError, 2);
            layout.Controls.Add(btnPanel, 0, 5);
            layout.SetColumnSpan(btnPanel, 2);

            this.Controls.Add(layout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // --- VALIDASI INPUT ---
            btnOK.Click += (s, e) =>
            {
                if (!TimeSpan.TryParse(txtStart.Text, out var _))
                {
                    lblError.Text = "Format Start Time salah (contoh: 08:00)";
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (!TimeSpan.TryParse(txtEnd.Text, out var _))
                {
                    lblError.Text = "Format End Time salah (contoh: 09:00)";
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (TimeSpan.Parse(txtEnd.Text) <= TimeSpan.Parse(txtStart.Text))
                {
                    lblError.Text = "End Time harus lebih besar dari Start Time";
                    this.DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtActivity.Text))
                {
                    lblError.Text = "Activity tidak boleh kosong";
                    this.DialogResult = DialogResult.None;
                    return;
                }
                lblError.Text = "";
            };

            // Blok Alt+F4 dan tombol close
            this.FormClosing += (s, e) =>
            {
                if (this.DialogResult != DialogResult.OK && this.DialogResult != DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            };
        }
    }
}