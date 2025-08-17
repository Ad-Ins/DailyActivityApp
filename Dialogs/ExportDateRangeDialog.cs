using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialog
{
    public class ExportDateRangeDialog : Form
    {
        public DateTime FromDate { get; private set; }
        public DateTime ToDate { get; private set; }

        private DateTimePicker dtpFrom = null!;
        private DateTimePicker dtpTo = null!;
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public ExportDateRangeDialog()
        {
            this.Text = "Select Date Range for Export";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.ForeColor = Color.White;
            this.Size = new Size(300, 200);

            Label lblFrom = new Label { Text = "From Date:", ForeColor = Color.White, Location = new Point(20, 20) };
            dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-1), Location = new Point(120, 20), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };

            Label lblTo = new Label { Text = "To Date:", ForeColor = Color.White, Location = new Point(20, 60) };
            dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Now, Location = new Point(120, 60), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White };

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(50, 120),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            btnOk.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(150, 120),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(lblFrom);
            this.Controls.Add(dtpFrom);
            this.Controls.Add(lblTo);
            this.Controls.Add(dtpTo);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (this.DialogResult == DialogResult.OK)
            {
                FromDate = dtpFrom.Value;
                ToDate = dtpTo.Value;
                if (FromDate > ToDate)
                {
                    MessageBox.Show("From date cannot be later than To date.", "Invalid Date Range");
                    e.Cancel = true;
                }
            }
        }
    }
}