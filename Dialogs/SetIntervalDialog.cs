using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialog
{
    public class SetIntervalDialog : Form
    {
        public int IntervalMinutes { get; private set; }

        private NumericUpDown numInterval;
        private Button btnOk;
        private Button btnCancel;

        public SetIntervalDialog(int currentIntervalMinutes)
        {
            this.Text = "Set Popup Interval";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(25, 25, 25);
            this.ForeColor = Color.White;
            this.Size = new Size(300, 180);

            Label lblInfo = new Label
            {
                Text = "Interval (minutes):",
                ForeColor = Color.White,
                Location = new Point(20, 20),
                AutoSize = true
            };

            numInterval = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 720, // 12 jam max
                Value = currentIntervalMinutes > 0 ? currentIntervalMinutes : 60,
                Location = new Point(150, 18),
                Width = 100,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(50, 90),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Width = 80
            };
            btnOk.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(150, 90),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Width = 80
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(lblInfo);
            this.Controls.Add(numInterval);
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
                IntervalMinutes = (int)numInterval.Value;
                if (IntervalMinutes <= 0)
                {
                    MessageBox.Show("Interval must be greater than 0.", "Invalid Interval");
                    e.Cancel = true;
                }
            }
        }
    }
}
