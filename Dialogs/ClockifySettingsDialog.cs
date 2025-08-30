using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdinersDailyActivityApp.Services;

namespace AdinersDailyActivityApp.Dialogs
{
    public partial class ClockifySettingsDialog : Form
    {
        private TextBox txtApiKey;
        private ComboBox cmbWorkspace;
        private ComboBox cmbProject;
        private CheckBox chkAutoCreateTasks;
        private Button btnTestConnection;
        private Button btnSyncHistory;
        private Button btnSave;
        private Button btnCancel;
        private Label lblStatus;
        
        private ClockifyService _clockifyService;
        
        public string ApiKey { get; private set; }
        public string WorkspaceId { get; private set; }
        public string ProjectId { get; private set; }
        public bool AutoCreateTasks { get; private set; }

        public ClockifySettingsDialog(string apiKey = "", string workspaceId = "", string projectId = "", bool autoCreateTasks = true)
        {
            InitializeComponent();
            _clockifyService = new ClockifyService();
            
            txtApiKey.Text = apiKey;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            chkAutoCreateTasks.Checked = autoCreateTasks;
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _ = LoadWorkspacesAsync();
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(500, 430);
            this.Text = "Clockify Settings";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            // API Key
            var lblApiKey = new Label
            {
                Text = "API Key:",
                Location = new Point(20, 20),
                Size = new Size(100, 23),
                ForeColor = Color.White
            };
            
            txtApiKey = new TextBox
            {
                Location = new Point(130, 20),
                Size = new Size(250, 23),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                UseSystemPasswordChar = true
            };

            btnTestConnection = new Button
            {
                Text = "Test",
                Location = new Point(390, 20),
                Size = new Size(60, 23),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            btnTestConnection.FlatAppearance.BorderSize = 0;
            btnTestConnection.Click += BtnTestConnection_Click;

            // Workspace
            var lblWorkspace = new Label
            {
                Text = "Workspace:",
                Location = new Point(20, 60),
                Size = new Size(100, 23),
                ForeColor = Color.White
            };
            
            cmbWorkspace = new ComboBox
            {
                Location = new Point(130, 60),
                Size = new Size(320, 23),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            cmbWorkspace.SelectedIndexChanged += CmbWorkspace_SelectedIndexChanged;

            // Project
            var lblProject = new Label
            {
                Text = "Project:",
                Location = new Point(20, 100),
                Size = new Size(100, 23),
                ForeColor = Color.White
            };
            
            cmbProject = new ComboBox
            {
                Location = new Point(130, 100),
                Size = new Size(320, 23),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            cmbProject.TextChanged += CmbProject_TextChanged;

            // Auto Create Tasks
            chkAutoCreateTasks = new CheckBox
            {
                Text = "Auto-create tasks for new activities",
                Location = new Point(20, 140),
                Size = new Size(300, 23),
                ForeColor = Color.White,
                Checked = true
            };

            // Sync History Button
            btnSyncHistory = new Button
            {
                Text = "Sync History to Clockify",
                Location = new Point(20, 170),
                Size = new Size(200, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Enabled = false
            };
            btnSyncHistory.FlatAppearance.BorderSize = 0;
            btnSyncHistory.Click += BtnSyncHistory_Click;

            // Status
            lblStatus = new Label
            {
                Location = new Point(20, 210),
                Size = new Size(430, 60),
                ForeColor = Color.FromArgb(100, 200, 255),
                Text = "Enter your Clockify API key and click Test to connect."
            };

            // Buttons
            btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(290, 350),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(380, 350),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[] {
                lblApiKey, txtApiKey, btnTestConnection,
                lblWorkspace, cmbWorkspace,
                lblProject, cmbProject,
                chkAutoCreateTasks, btnSyncHistory, lblStatus,
                btnSave, btnCancel
            });
        }

        private async void BtnTestConnection_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                lblStatus.Text = "Please enter API key.";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                return;
            }

            btnTestConnection.Enabled = false;
            lblStatus.Text = "Testing connection...";
            lblStatus.ForeColor = Color.FromArgb(100, 200, 255);

            await LoadWorkspacesAsync();
        }

        private async Task LoadWorkspacesAsync()
        {
            try
            {
                _clockifyService.SetApiKey(txtApiKey.Text);
                var workspaces = await _clockifyService.GetWorkspacesAsync();
                
                cmbWorkspace.Items.Clear();
                cmbProject.Items.Clear();
                
                if (workspaces.Any())
                {
                    foreach (var workspace in workspaces)
                    {
                        cmbWorkspace.Items.Add(new ComboBoxItem { Text = workspace.Name, Value = workspace.Id });
                    }
                    
                    // Select previously selected workspace
                    if (!string.IsNullOrEmpty(WorkspaceId))
                    {
                        var item = cmbWorkspace.Items.Cast<ComboBoxItem>().FirstOrDefault(x => x.Value == WorkspaceId);
                        if (item != null) 
                        {
                            cmbWorkspace.SelectedItem = item;
                            cmbWorkspace.Text = item.Text; // Ensure text is set
                            // Load projects for selected workspace
                            _ = LoadProjectsForWorkspaceAsync(WorkspaceId);
                        }
                    }
                    else if (cmbWorkspace.Items.Count > 0)
                    {
                        // Auto-select first workspace if none was previously selected
                        cmbWorkspace.SelectedIndex = 0;
                    }
                    
                    lblStatus.Text = $"Connected successfully! Found {workspaces.Count} workspace(s).";
                    lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                    btnSyncHistory.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "Connected but no workspaces found.";
                    lblStatus.ForeColor = Color.FromArgb(255, 193, 7);
                }
            }
            catch
            {
                lblStatus.Text = "Connection failed. Please check your API key.";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                btnTestConnection.Enabled = true;
            }
        }

        private async void CmbWorkspace_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbWorkspace.SelectedItem is ComboBoxItem selectedWorkspace)
            {
                await LoadProjectsForWorkspaceAsync(selectedWorkspace.Value);
            }
        }
        
        private async Task LoadProjectsForWorkspaceAsync(string workspaceId)
        {
            cmbProject.Items.Clear();
            lblStatus.Text = "Loading projects...";
            
            try
            {
                var projects = await _clockifyService.GetProjectsAsync(workspaceId);
                foreach (var project in projects)
                {
                    cmbProject.Items.Add(new ComboBoxItem { Text = project.Name, Value = project.Id });
                }
                
                // Select previously selected project
                if (!string.IsNullOrEmpty(ProjectId))
                {
                    var item = cmbProject.Items.Cast<ComboBoxItem>().FirstOrDefault(x => x.Value == ProjectId);
                    if (item != null) 
                    {
                        cmbProject.SelectedItem = item;
                        cmbProject.Text = item.Text; // Ensure text is set
                    }
                }
                else if (cmbProject.Items.Count > 0)
                {
                    // Auto-select first project if none was previously selected
                    cmbProject.SelectedIndex = 0;
                }
                
                lblStatus.Text = projects.Count > 0 ? 
                    $"Loaded {projects.Count} project(s). All active projects are shown." :
                    "No active projects found in this workspace.";
                lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
            }
            catch
            {
                lblStatus.Text = "Failed to load projects.";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            ApiKey = txtApiKey.Text;
            
            // Get selected workspace and project IDs
            var selectedWorkspace = cmbWorkspace.SelectedItem as ComboBoxItem;
            var selectedProject = cmbProject.SelectedItem as ComboBoxItem;
            
            // If user typed custom text, try to find matching item
            if (selectedWorkspace == null && !string.IsNullOrEmpty(cmbWorkspace.Text))
            {
                selectedWorkspace = cmbWorkspace.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(x => x.Text.Equals(cmbWorkspace.Text, StringComparison.OrdinalIgnoreCase));
            }
            
            if (selectedProject == null && !string.IsNullOrEmpty(cmbProject.Text))
            {
                selectedProject = cmbProject.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(x => x.Text.Equals(cmbProject.Text, StringComparison.OrdinalIgnoreCase));
            }
            
            WorkspaceId = selectedWorkspace?.Value ?? "";
            ProjectId = selectedProject?.Value ?? "";
            AutoCreateTasks = chkAutoCreateTasks.Checked;
        }
        
        private async void BtnSyncHistory_Click(object sender, EventArgs e)
        {
            if (cmbWorkspace.SelectedItem == null || cmbProject.SelectedItem == null)
            {
                lblStatus.Text = "Please select workspace and project first.";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                return;
            }
            
            var result = MessageBox.Show(
                "This will sync all LOCAL activities to Clockify.\nThis may take a while depending on your history size.\n\nContinue?",
                "Sync History to Clockify",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
                
            if (result != DialogResult.Yes) return;
            
            btnSyncHistory.Enabled = false;
            btnSyncHistory.Text = "Syncing...";
            lblStatus.Text = "Syncing history data to Clockify...";
            lblStatus.ForeColor = Color.FromArgb(100, 200, 255);
            
            try
            {
                var workspaceId = (cmbWorkspace.SelectedItem as ComboBoxItem)?.Value ?? "";
                var projectId = (cmbProject.SelectedItem as ComboBoxItem)?.Value ?? "";
                
                int syncedCount = await SyncHistoryToClockifyAsync(workspaceId, projectId);
                
                lblStatus.Text = $"Sync completed! {syncedCount} activities synced to Clockify.";
                lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Sync failed: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                btnSyncHistory.Enabled = true;
                btnSyncHistory.Text = "Sync History to Clockify";
            }
        }
        
        private async Task<int> SyncHistoryToClockifyAsync(string workspaceId, string projectId)
        {
            string logFilePath = GetLogFilePath();
            if (!File.Exists(logFilePath)) return 0;
            
            var lines = File.ReadAllLines(logFilePath);
            var updatedLines = new List<string>();
            int syncedCount = 0;
            
            foreach (string line in lines)
            {
                if (line.Contains("[LOCAL]"))
                {
                    try
                    {
                        var (timestamp, type, activity) = ParseLogLine(line);
                        if (timestamp.HasValue)
                        {
                            // Create task if needed
                            string taskId = null;
                            if (chkAutoCreateTasks.Checked && !string.IsNullOrEmpty(type))
                            {
                                var task = await _clockifyService.CreateTaskAsync(workspaceId, projectId, type);
                                taskId = task?.Id;
                            }
                            
                            // Create time entry
                            var timeEntry = await _clockifyService.CreateHistoryTimeEntryAsync(
                                workspaceId, projectId, taskId, activity, timestamp.Value, timestamp.Value.AddMinutes(30));
                            
                            if (timeEntry != null)
                            {
                                // Update line to SYNCED
                                string updatedLine = line.Replace("[LOCAL]", "[SYNCED]");
                                updatedLines.Add(updatedLine);
                                syncedCount++;
                            }
                            else
                            {
                                updatedLines.Add(line);
                            }
                        }
                        else
                        {
                            updatedLines.Add(line);
                        }
                    }
                    catch
                    {
                        updatedLines.Add(line);
                    }
                }
                else
                {
                    updatedLines.Add(line);
                }
            }
            
            // Update log file
            File.WriteAllLines(logFilePath, updatedLines);
            return syncedCount;
        }
        
        private (DateTime? timestamp, string type, string activity) ParseLogLine(string line)
        {
            try
            {
                int timestampEndIndex = line.IndexOf(']');
                if (timestampEndIndex <= 0) return (null, "", "");
                
                string timestampStr = line.Substring(1, timestampEndIndex - 1);
                if (!DateTime.TryParse(timestampStr, out DateTime timestamp))
                    return (null, "", "");
                
                string rest = line.Substring(timestampEndIndex + 2).Trim();
                
                // Remove sync flag
                if (rest.StartsWith("[LOCAL]"))
                {
                    rest = rest.Substring(7).Trim();
                }
                
                string type = "";
                string activity = rest;
                
                int pipeIndex = rest.IndexOf('|');
                if (pipeIndex > 0)
                {
                    type = rest.Substring(0, pipeIndex).Trim();
                    activity = rest.Substring(pipeIndex + 1).Trim();
                }
                
                return (timestamp, type, activity);
            }
            catch
            {
                return (null, "", "");
            }
        }
        
        private string GetLogFilePath()
        {
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdinersDailyActivity");
            return Path.Combine(appDataDir, "activity_log.txt");
        }

        private async void CmbProject_TextChanged(object sender, EventArgs e)
        {
            if (cmbWorkspace.SelectedItem is ComboBoxItem selectedWorkspace && 
                !string.IsNullOrWhiteSpace(cmbProject.Text) && 
                cmbProject.Text.Length >= 2) // Start searching after 2 characters
            {
                try
                {
                    var projects = await _clockifyService.SearchProjectsAsync(selectedWorkspace.Value, cmbProject.Text);
                    
                    // Save current text and selection
                    string currentText = cmbProject.Text;
                    int selectionStart = cmbProject.SelectionStart;
                    
                    // Update items without triggering events
                    cmbProject.TextChanged -= CmbProject_TextChanged;
                    cmbProject.Items.Clear();
                    
                    foreach (var project in projects)
                    {
                        cmbProject.Items.Add(new ComboBoxItem { Text = project.Name, Value = project.Id });
                    }
                    
                    // Restore text and selection
                    cmbProject.Text = currentText;
                    cmbProject.SelectionStart = selectionStart;
                    cmbProject.TextChanged += CmbProject_TextChanged;
                }
                catch { /* Ignore search errors */ }
            }
        }

        private class ComboBoxItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
            public override string ToString() => Text;
        }
    }
}