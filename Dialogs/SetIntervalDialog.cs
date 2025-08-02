using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdinersDailyActivityApp
{
    public class SetIntervalDialog : Form
    {
        public int IntervalMinutes { get; private set; }

        private NumericUpDown numInterval;
        private Label lblError;

        public SetIntervalDialog(int currentInterval)
        {
            this.Text = "Set Interval";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.Width = 420;
            this.Height = 220;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 2,
                Padding = new Padding(32, 24, 32, 24),
                BackColor = this.BackColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Ubah dari 60 ke 50 agar tidak terlalu turun

            Font labelFont = new Font("Segoe UI", 14F, FontStyle.Bold);
            Font inputFont = new Font("Segoe UI", 14F, FontStyle.Regular);
            Color labelColor = Color.WhiteSmoke;
            Color inputBack = Color.FromArgb(36, 36, 48);
            Color inputFore = Color.White;

            var lblInterval = new Label
            {
                Text = "Interval (minutes):",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            numInterval = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 480,
                Value = currentInterval,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            lblError = new Label
            {
                ForeColor = Color.OrangeRed,
                Text = "",
                Font = new Font("Segoe UI", 11F, FontStyle.Italic),
                BackColor = this.BackColor,
                Dock = DockStyle.Fill,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                Padding = new Padding(0, 0, 0, 0),
                BackColor = this.BackColor,
                Height = 44
            };
            var btnOK = new Button
            {
                Text = "OK",
                Width = 100,
                Height = 38,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 38,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnPanel.Controls.Add(btnOK);
            btnPanel.Controls.Add(btnCancel);

            layout.Controls.Add(lblInterval, 0, 0);
            layout.Controls.Add(numInterval, 1, 0);
            layout.Controls.Add(lblError, 0, 1);
            layout.SetColumnSpan(lblError, 2);
            // Pindahkan btnPanel ke baris ke-2 kolom ke-1 (kanan), agar tombol selalu rata kanan dan tidak turun
            layout.Controls.Add(btnPanel, 1, 2);

            this.Controls.Add(layout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            btnOK.Click += (s, e) =>
            {
                if (numInterval.Value < 1)
                {
                    lblError.Text = "Interval minimal 1 menit.";
                    this.DialogResult = DialogResult.None;
                    return;
                }
                lblError.Text = "";
                IntervalMinutes = (int)numInterval.Value;
            };
        }
    }
}