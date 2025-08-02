using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace AdinersDailyActivityApp
{
    public class JiraConnectionDialog : Form
    {
        public string JiraUrl => txtUrl.Text.Trim();
        public string Username => txtUsername.Text.Trim();
        public string Password => txtPassword.Text; // plain, encrypt before save

        private TextBox txtUrl, txtUsername, txtPassword;
        private Label lblError;
        private Button btnTest, btnSave;
        private bool testPassed = false;

        public JiraConnectionDialog(string url, string username, string encryptedPassword)
        {
            this.Text = "Jira Connection Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Width = 520;
            this.Height = 340;
            this.BackColor = Color.FromArgb(24, 24, 32);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 6,
                ColumnCount = 2,
                Padding = new Padding(32, 24, 32, 24),
                BackColor = this.BackColor
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            Font labelFont = new Font("Segoe UI", 12F, FontStyle.Bold);
            Font inputFont = new Font("Segoe UI", 12F, FontStyle.Regular);
            Color labelColor = Color.WhiteSmoke;
            Color inputBack = Color.FromArgb(36, 36, 48);
            Color inputFore = Color.White;

            var lblUrl = new Label
            {
                Text = "Jira URL",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtUrl = new TextBox
            {
                Text = url,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            var lblUsername = new Label
            {
                Text = "Username",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtUsername = new TextBox
            {
                Text = username,
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            var lblPassword = new Label
            {
                Text = "Password",
                Font = labelFont,
                ForeColor = labelColor,
                BackColor = this.BackColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            txtPassword = new TextBox
            {
                Text = string.IsNullOrEmpty(encryptedPassword) ? "" : AppConfig.Decrypt(encryptedPassword),
                Font = inputFont,
                BackColor = inputBack,
                ForeColor = inputFore,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true
            };

            lblError = new Label
            {
                ForeColor = Color.OrangeRed,
                Text = "",
                Font = new Font("Segoe UI", 10F, FontStyle.Italic),
                BackColor = this.BackColor,
                Dock = DockStyle.Fill,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter
            };

            btnTest = new Button
            {
                Text = "Test Connection",
                Width = 160,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Anchor = AnchorStyles.Right
            };
            btnTest.FlatAppearance.BorderSize = 0;

            btnSave = new Button
            {
                Text = "Save",
                Width = 120,
                Height = 38,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Anchor = AnchorStyles.Right,
                Enabled = false
            };
            btnSave.FlatAppearance.BorderSize = 0;

            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Height = 50,
                BackColor = this.BackColor
            };
            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnTest);

            layout.Controls.Add(lblUrl, 0, 0);
            layout.Controls.Add(txtUrl, 1, 0);
            layout.Controls.Add(lblUsername, 0, 1);
            layout.Controls.Add(txtUsername, 1, 1);
            layout.Controls.Add(lblPassword, 0, 2);
            layout.Controls.Add(txtPassword, 1, 2);
            layout.Controls.Add(lblError, 0, 3);
            layout.SetColumnSpan(lblError, 2);
            layout.Controls.Add(btnPanel, 1, 4);

            this.Controls.Add(layout);
            this.AcceptButton = btnSave;
            this.CancelButton = btnSave;

            btnTest.Click += async (s, e) =>
            {
                lblError.Text = "Testing connection...";
                btnTest.Enabled = false;
                btnSave.Enabled = false;
                try
                {
                    // Test connection to Jira
                    var url = txtUrl.Text.Trim();
                    var user = txtUsername.Text.Trim();
                    var pass = txtPassword.Text;
                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                    {
                        lblError.Text = "URL, Username, dan Password wajib diisi.";
                        return;
                    }

                    // Test Jira connection (SOAP login)
                    // var client = new ServiceReference.JiraSoapServiceClient(ServiceReference.JiraSoapServiceClient.EndpointConfiguration.jirasoapservice_v2, url);
                    // var token = await client.loginAsync(user, pass);
                    // if (!string.IsNullOrEmpty(token?.loginReturn))
                    // {
                    //     lblError.ForeColor = Color.LightGreen;
                    //     lblError.Text = "Connection OK!";
                    //     testPassed = true;
                    //     btnSave.Enabled = true;
                    // }
                    // else
                    // {
                    //     lblError.ForeColor = Color.OrangeRed;
                    //     lblError.Text = "Login gagal.";
                    //     testPassed = false;
                    // }
                }
                catch (Exception ex)
                {
                    lblError.ForeColor = Color.OrangeRed;
                    lblError.Text = "Error: " + ex.Message;
                    testPassed = false;
                }
                finally
                {
                    btnTest.Enabled = true;
                }
            };

            btnSave.Click += (s, e) =>
            {
                if (!testPassed)
                {
                    lblError.Text = "Silakan test connection dulu!";
                    this.DialogResult = DialogResult.None;
                    return;
                }
            };
        }
    }
}