using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AdinersDailyActivityApp.Services;

namespace AdinersDailyActivityApp.Dialogs
{
    public class MultiJiraSettingsDialog : Form
    {
        private readonly MultiJiraService _multiJiraService;
        private ListBox lstInstances = null!;
        private Button btnAdd = null!;
        private Button btnEdit = null!;
        private Button btnDelete = null!;
        private Button btnTest = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;
        private CheckBox chkAutoDetect = null!;
        private CheckBox chkCrossInstanceSearch = null!;
        private ComboBox cmbDefaultInstance = null!;
        private Label lblStatus = null!;

        public MultiJiraSettingsDialog(MultiJiraService multiJiraService)
        {
            _multiJiraService = multiJiraService;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "Multi-JIRA Configuration";
            Size = new Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            // Title
            var titleLabel = new Label
            {
                Text = "🔗 Multi-JIRA Integration Settings",
                Location = new Point(20, 20),
                Size = new Size(640, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };

            // Instances list
            var instancesLabel = new Label
            {
                Text = "JIRA Instances:",
                Location = new Point(20, 60),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            lstInstances = new ListBox
            {
                Location = new Point(20, 85),
                Size = new Size(450, 200),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };

            // Instance management buttons
            btnAdd = CreateButton("Add", new Point(480, 85), new Size(80, 30));
            btnEdit = CreateButton("Edit", new Point(480, 120), new Size(80, 30));
            btnDelete = CreateButton("Delete", new Point(480, 155), new Size(80, 30));
            btnTest = CreateButton("Test All", new Point(480, 190), new Size(80, 30));

            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnDelete.Click += BtnDelete_Click;
            btnTest.Click += BtnTest_Click;

            // Global settings
            var settingsLabel = new Label
            {
                Text = "Global Settings:",
                Location = new Point(20, 300),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            chkAutoDetect = new CheckBox
            {
                Text = "Auto-detect JIRA instance based on project key",
                Location = new Point(20, 325),
                Size = new Size(350, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            chkCrossInstanceSearch = new CheckBox
            {
                Text = "Enable cross-instance search",
                Location = new Point(20, 350),
                Size = new Size(250, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            var defaultLabel = new Label
            {
                Text = "Default Instance:",
                Location = new Point(20, 380),
                Size = new Size(120, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            cmbDefaultInstance = new ComboBox
            {
                Location = new Point(150, 378),
                Size = new Size(200, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };

            // Status label
            lblStatus = new Label
            {
                Location = new Point(20, 415),
                Size = new Size(640, 20),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 9)
            };

            // Action buttons
            btnSave = CreateButton("Save", new Point(480, 440), new Size(80, 30));
            btnCancel = CreateButton("Cancel", new Point(570, 440), new Size(80, 30));

            btnSave.BackColor = Color.FromArgb(0, 120, 215);
            btnCancel.BackColor = Color.FromArgb(50, 50, 50);

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] {
                titleLabel, instancesLabel, lstInstances,
                btnAdd, btnEdit, btnDelete, btnTest,
                settingsLabel, chkAutoDetect, chkCrossInstanceSearch,
                defaultLabel, cmbDefaultInstance, lblStatus,
                btnSave, btnCancel
            });
        }

        private Button CreateButton(string text, Point location, Size size)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
        }

        private void LoadSettings()
        {
            var config = _multiJiraService.GetConfig();
            
            // Load instances
            lstInstances.Items.Clear();
            foreach (var instance in config.Instances)
            {
                var status = instance.Enabled ? "✓" : "✗";
                var connectionStatus = "";
                var service = _multiJiraService.GetServiceByName(instance.Name);
                if (service != null)
                {
                    connectionStatus = service.IsConnected ? " [Connected]" : " [Disconnected]";
                }
                
                lstInstances.Items.Add($"{status} {instance.Name} ({instance.Version}){connectionStatus}");
            }

            // Load global settings
            chkAutoDetect.Checked = config.AutoDetectInstance;
            chkCrossInstanceSearch.Checked = config.EnableCrossInstanceSearch;

            // Load default instance dropdown
            cmbDefaultInstance.Items.Clear();
            cmbDefaultInstance.Items.Add("(None)");
            foreach (var instance in config.Instances)
            {
                cmbDefaultInstance.Items.Add(instance.Name);
            }

            if (!string.IsNullOrEmpty(config.DefaultInstance))
            {
                cmbDefaultInstance.SelectedItem = config.DefaultInstance;
            }
            else
            {
                cmbDefaultInstance.SelectedIndex = 0;
            }

            UpdateStatus();
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            using var dialog = new JiraInstanceDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var config = _multiJiraService.GetConfig();
                config.Instances.Add(dialog.InstanceConfig);
                LoadSettings();
            }
        }

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (lstInstances.SelectedIndex >= 0)
            {
                var config = _multiJiraService.GetConfig();
                var instance = config.Instances[lstInstances.SelectedIndex];
                
                using var dialog = new JiraInstanceDialog(instance);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    config.Instances[lstInstances.SelectedIndex] = dialog.InstanceConfig;
                    LoadSettings();
                }
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (lstInstances.SelectedIndex >= 0)
            {
                var result = MessageBox.Show("Are you sure you want to delete this JIRA instance?", 
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    var config = _multiJiraService.GetConfig();
                    config.Instances.RemoveAt(lstInstances.SelectedIndex);
                    LoadSettings();
                }
            }
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            lblStatus.Text = "Testing connections...";
            lblStatus.ForeColor = Color.Yellow;
            
            btnTest.Enabled = false;
            
            try
            {
                var success = await _multiJiraService.TestAllConnectionsAsync();
                LoadSettings(); // Refresh connection status
                
                var connectedCount = _multiJiraService.GetConnectedServices().Count;
                var totalCount = _multiJiraService.GetAllServices().Count;
                
                lblStatus.Text = $"Connection test completed. {connectedCount}/{totalCount} instances connected.";
                lblStatus.ForeColor = connectedCount > 0 ? Color.LightGreen : Color.FromArgb(220, 53, 69);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Connection test failed: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var config = _multiJiraService.GetConfig();
            
            // Update global settings
            config.AutoDetectInstance = chkAutoDetect.Checked;
            config.EnableCrossInstanceSearch = chkCrossInstanceSearch.Checked;
            config.DefaultInstance = cmbDefaultInstance.SelectedItem?.ToString() == "(None)" ? 
                "" : cmbDefaultInstance.SelectedItem?.ToString() ?? "";

            _multiJiraService.SaveConfig();
            
            lblStatus.Text = "Settings saved successfully!";
            lblStatus.ForeColor = Color.LightGreen;
            
            DialogResult = DialogResult.OK;
        }

        private void UpdateStatus()
        {
            var config = _multiJiraService.GetConfig();
            var enabledCount = config.Instances.Count(i => i.Enabled);
            var connectedCount = _multiJiraService.GetConnectedServices().Count;
            
            lblStatus.Text = $"{enabledCount} instances configured, {connectedCount} connected";
            lblStatus.ForeColor = connectedCount > 0 ? Color.LightGreen : Color.Gray;
        }
    }
}