using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AdinersDailyActivityApp.Services;

namespace AdinersDailyActivityApp.Dialogs
{
    public class JiraInstanceDialog : Form
    {
        private TextBox txtName = null!;
        private ComboBox cmbVersion = null!;
        private TextBox txtBaseUrl = null!;
        private ComboBox cmbAuthType = null!;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private TextBox txtApiToken = null!;
        private TextBox txtProjects = null!;
        private CheckBox chkEnabled = null!;
        private NumericUpDown numPriority = null!;
        private Button btnTest = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;
        private Panel pnlBasicAuth = null!;
        private Panel pnlTokenAuth = null!;

        public JiraInstanceConfig InstanceConfig { get; private set; } = new();

        public JiraInstanceDialog(JiraInstanceConfig? existingConfig = null)
        {
            if (existingConfig != null)
            {
                InstanceConfig = new JiraInstanceConfig
                {
                    Name = existingConfig.Name,
                    Version = existingConfig.Version,
                    BaseUrl = existingConfig.BaseUrl,
                    AuthType = existingConfig.AuthType,
                    Username = existingConfig.Username,
                    Password = existingConfig.Password,
                    ApiToken = existingConfig.ApiToken,
                    Projects = existingConfig.Projects.ToList(),
                    Enabled = existingConfig.Enabled,
                    Priority = existingConfig.Priority
                };
            }

            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            Text = "JIRA Instance Configuration";
            Size = new Size(500, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            var y = 20;

            // Title
            var titleLabel = new Label
            {
                Text = "⚙️ JIRA Instance Configuration",
                Location = new Point(20, y),
                Size = new Size(440, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            y += 40;

            // Name
            AddLabel("Instance Name:", y);
            txtName = AddTextBox(y + 20, "Production JIRA");
            y += 50;

            // Version
            AddLabel("JIRA Version:", y);
            cmbVersion = new ComboBox
            {
                Location = new Point(20, y + 20),
                Size = new Size(200, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };
            cmbVersion.Items.AddRange(new[] { "4.0", "6.1.7", "7.x", "8.x", "Cloud" });
            y += 50;

            // Base URL
            AddLabel("Base URL:", y);
            txtBaseUrl = AddTextBox(y + 20, "https://jira.company.com");
            y += 50;

            // Auth Type
            AddLabel("Authentication Type:", y);
            cmbAuthType = new ComboBox
            {
                Location = new Point(20, y + 20),
                Size = new Size(150, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };
            cmbAuthType.Items.AddRange(new[] { "Basic", "Token", "OAuth" });
            cmbAuthType.SelectedIndexChanged += CmbAuthType_SelectedIndexChanged;
            y += 50;

            // Basic Auth Panel
            pnlBasicAuth = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(440, 80),
                BackColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblUsername = new Label
            {
                Text = "Username:",
                Location = new Point(10, 10),
                Size = new Size(80, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            txtUsername = new TextBox
            {
                Location = new Point(100, 8),
                Size = new Size(320, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };

            var lblPassword = new Label
            {
                Text = "Password:",
                Location = new Point(10, 40),
                Size = new Size(80, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            txtPassword = new TextBox
            {
                Location = new Point(100, 38),
                Size = new Size(320, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                UseSystemPasswordChar = true
            };

            pnlBasicAuth.Controls.AddRange(new Control[] { lblUsername, txtUsername, lblPassword, txtPassword });
            y += 90;

            // Token Auth Panel
            pnlTokenAuth = new Panel
            {
                Location = new Point(20, y - 90),
                Size = new Size(440, 50),
                BackColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            var lblToken = new Label
            {
                Text = "API Token:",
                Location = new Point(10, 15),
                Size = new Size(80, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            txtApiToken = new TextBox
            {
                Location = new Point(100, 13),
                Size = new Size(320, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                UseSystemPasswordChar = true
            };

            pnlTokenAuth.Controls.AddRange(new Control[] { lblToken, txtApiToken });

            // Projects
            AddLabel("Project Keys (comma-separated):", y);
            txtProjects = AddTextBox(y + 20, "PROJ1, PROJ2, PROJ3");
            y += 50;

            // Settings
            chkEnabled = new CheckBox
            {
                Text = "Enabled",
                Location = new Point(20, y),
                Size = new Size(100, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Checked = true
            };

            var lblPriority = new Label
            {
                Text = "Priority:",
                Location = new Point(150, y),
                Size = new Size(60, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            numPriority = new NumericUpDown
            {
                Location = new Point(220, y - 2),
                Size = new Size(60, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                Minimum = 1,
                Maximum = 10,
                Value = 1
            };
            y += 40;

            // Test button
            btnTest = new Button
            {
                Text = "Test Connection",
                Location = new Point(20, y),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnTest.Click += BtnTest_Click;
            y += 40;

            // Status
            lblStatus = new Label
            {
                Location = new Point(20, y),
                Size = new Size(440, 20),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9)
            };
            y += 30;

            // Action buttons
            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(300, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(390, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] {
                titleLabel, cmbVersion, txtBaseUrl, cmbAuthType,
                pnlBasicAuth, pnlTokenAuth, txtProjects,
                chkEnabled, lblPriority, numPriority,
                btnTest, lblStatus, btnSave, btnCancel
            });
        }

        private void AddLabel(string text, int y)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(20, y),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            Controls.Add(label);
        }

        private TextBox AddTextBox(int y, string placeholder)
        {
            var textBox = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(440, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                Text = placeholder
            };
            Controls.Add(textBox);
            return textBox;
        }

        private void CmbAuthType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var authType = cmbAuthType.SelectedItem?.ToString();
            pnlBasicAuth.Visible = authType == "Basic";
            pnlTokenAuth.Visible = authType == "Token";
        }

        private void LoadConfig()
        {
            txtName.Text = InstanceConfig.Name;
            cmbVersion.SelectedItem = InstanceConfig.Version;
            txtBaseUrl.Text = InstanceConfig.BaseUrl;
            cmbAuthType.SelectedItem = InstanceConfig.AuthType;
            txtUsername.Text = InstanceConfig.Username;
            txtPassword.Text = InstanceConfig.Password;
            txtApiToken.Text = InstanceConfig.ApiToken;
            txtProjects.Text = string.Join(", ", InstanceConfig.Projects);
            chkEnabled.Checked = InstanceConfig.Enabled;
            numPriority.Value = InstanceConfig.Priority;

            // Set default values if empty
            if (string.IsNullOrEmpty(InstanceConfig.Version))
                cmbVersion.SelectedItem = "Cloud";
            if (string.IsNullOrEmpty(InstanceConfig.AuthType))
                cmbAuthType.SelectedItem = "Basic";

            CmbAuthType_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            SaveToConfig();
            
            lblStatus.Text = "Testing connection...";
            lblStatus.ForeColor = Color.Yellow;
            btnTest.Enabled = false;

            try
            {
                IJiraService service = InstanceConfig.Version switch
                {
                    var v when v.StartsWith("4.") => new JiraV4Service(InstanceConfig),
                    var v when v.StartsWith("6.") || v.StartsWith("7.") || v.StartsWith("8.") => new JiraV6Service(InstanceConfig),
                    "Cloud" => new JiraCloudService(InstanceConfig),
                    _ => new JiraV6Service(InstanceConfig)
                };

                var success = await service.TestConnectionAsync();
                
                if (success)
                {
                    lblStatus.Text = "✓ Connection successful!";
                    lblStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    lblStatus.Text = "✗ Connection failed. Check your settings.";
                    lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"✗ Error: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (ValidateInput())
            {
                SaveToConfig();
                DialogResult = DialogResult.OK;
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter an instance name.", "Validation Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtBaseUrl.Text))
            {
                MessageBox.Show("Please enter a base URL.", "Validation Error");
                return false;
            }

            var authType = cmbAuthType.SelectedItem?.ToString();
            if (authType == "Basic" && (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text)))
            {
                MessageBox.Show("Please enter username and password for Basic authentication.", "Validation Error");
                return false;
            }

            if (authType == "Token" && string.IsNullOrWhiteSpace(txtApiToken.Text))
            {
                MessageBox.Show("Please enter an API token for Token authentication.", "Validation Error");
                return false;
            }

            return true;
        }

        private void SaveToConfig()
        {
            InstanceConfig.Name = txtName.Text.Trim();
            InstanceConfig.Version = cmbVersion.SelectedItem?.ToString() ?? "Cloud";
            InstanceConfig.BaseUrl = txtBaseUrl.Text.Trim().TrimEnd('/');
            InstanceConfig.AuthType = cmbAuthType.SelectedItem?.ToString() ?? "Basic";
            InstanceConfig.Username = txtUsername.Text.Trim();
            InstanceConfig.Password = txtPassword.Text;
            InstanceConfig.ApiToken = txtApiToken.Text.Trim();
            InstanceConfig.Enabled = chkEnabled.Checked;
            InstanceConfig.Priority = (int)numPriority.Value;

            // Parse projects
            InstanceConfig.Projects.Clear();
            if (!string.IsNullOrWhiteSpace(txtProjects.Text))
            {
                var projects = txtProjects.Text.Split(',')
                    .Select(p => p.Trim().ToUpper())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                InstanceConfig.Projects.AddRange(projects);
            }
        }
    }
}