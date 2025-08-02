using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdinersDailyActivityApp
{
    /// <summary>
    /// Dialog untuk memilih tanggal export log (dark, flat, modern).
    /// </summary>
    public class ExportDialog : Form
    {
        public DateTime StartDate => dtStart.Value.Date;
        public DateTime EndDate => dtEnd.Value.Date;

        private DateTimePicker dtStart;
        private DateTimePicker dtEnd;
        private Button btnExport;

        public ExportDialog()
        {
            this.Text = "Export Log to Excel";
            this.Width = 600;
            this.Height = 320;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 32);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 2,
                Padding = new Padding(60, 40, 60, 40),
                BackColor = this.BackColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Font labelFont = new Font("Segoe UI", 16F, FontStyle.Bold);
            Font inputFont = new Font("Segoe UI", 16F, FontStyle.Regular);
            Color labelColor = Color.WhiteSmoke;
            Color inputBack = Color.FromArgb(36, 36, 48);
            Color inputFore = Color.White;

            var lblStart = new Label
            {
                Text = "Start Date:",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            dtStart = new DateTimePicker
            {
                Font = inputFont,
                Width = 260,
                CalendarMonthBackground = inputBack,
                CalendarForeColor = inputFore,
                BackColor = inputBack,
                ForeColor = inputFore,
                Format = DateTimePickerFormat.Short,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            var lblEnd = new Label
            {
                Text = "End Date:",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            dtEnd = new DateTimePicker
            {
                Font = inputFont,
                Width = 260,
                CalendarMonthBackground = inputBack,
                CalendarForeColor = inputFore,
                BackColor = inputBack,
                ForeColor = inputFore,
                Format = DateTimePickerFormat.Short,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            btnExport = new Button
            {
                Text = "Export",
                Width = 180,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Anchor = AnchorStyles.Right
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += (s, e) => this.DialogResult = DialogResult.OK;

            // Kosongkan cell [2,0] agar tombol Export rata kanan
            layout.Controls.Add(lblStart, 0, 0);
            layout.Controls.Add(dtStart, 1, 0);
            layout.Controls.Add(lblEnd, 0, 1);
            layout.Controls.Add(dtEnd, 1, 1);
            layout.Controls.Add(new Label { BackColor = this.BackColor }, 0, 2);
            layout.Controls.Add(btnExport, 1, 2);

            this.Controls.Add(layout);
        }
    }
}
