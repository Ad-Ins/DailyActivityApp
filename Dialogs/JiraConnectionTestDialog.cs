using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Dialogs
{
    public class JiraConnectionTestDialog : Form
    {
        private TextBox txtUrl = null!;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Button btnTest = null!;
        private Button btnClose = null!;
        private Label lblStatus = null!;
        private ListBox lstEndpoints = null!;

        public JiraConnectionTestDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "JIRA Connection Tester";
            Size = new Size(600, 500);
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
                Text = "🔍 JIRA Connection Tester",
                Location = new Point(20, y),
                Size = new Size(540, 30),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            y += 40;

            // URL
            AddLabel("Base URL:", y);
            txtUrl = AddTextBox(y + 20, "https://ijira.ad-ins.com");
            y += 50;

            // Username
            AddLabel("Username:", y);
            txtUsername = AddTextBox(y + 20, "");
            y += 50;

            // Password
            AddLabel("Password:", y);
            txtPassword = AddTextBox(y + 20, "");
            txtPassword.UseSystemPasswordChar = true;
            y += 50;

            // Test button
            btnTest = new Button
            {
                Text = "Test Connection",
                Location = new Point(20, y),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnTest.Click += BtnTest_Click;
            y += 40;

            // Status
            lblStatus = new Label
            {
                Location = new Point(20, y),
                Size = new Size(540, 20),
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9)
            };
            y += 30;

            // Endpoints list
            AddLabel("Test Results:", y);
            lstEndpoints = new ListBox
            {
                Location = new Point(20, y + 20),
                Size = new Size(540, 200),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8)
            };
            y += 230;

            // Close button
            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(480, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[] {
                titleLabel, txtUrl, txtUsername, txtPassword,
                btnTest, lblStatus, lstEndpoints, btnClose
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

        private TextBox AddTextBox(int y, string text)
        {
            var textBox = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(540, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                Text = text
            };
            Controls.Add(textBox);
            return textBox;
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                lblStatus.Text = "Please enter a URL";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                return;
            }

            btnTest.Enabled = false;
            lblStatus.Text = "Testing connection...";
            lblStatus.ForeColor = Color.Yellow;
            lstEndpoints.Items.Clear();

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Add basic auth if provided
                if (!string.IsNullOrWhiteSpace(txtUsername.Text) && !string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{txtUsername.Text}:{txtPassword.Text}"));
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                }

                var inputUrl = txtUrl.Text.TrimEnd('/');
                var baseUrl = GetCleanBaseUrl(inputUrl);
                
                lstEndpoints.Items.Add($"Input URL: {inputUrl}");
                lstEndpoints.Items.Add($"Clean Base URL: {baseUrl}");
                lstEndpoints.Items.Add("");
                
                var endpoints = new[]
                {
                    "/rest/api/2/serverInfo",
                    "/rest/api/latest/serverInfo",
                    "/rest/api/2/myself",
                    "/rest/api/2/project",
                    "/secure/Dashboard.jspa",
                    "/secure/ManageFilters.jspa",
                    "/sr/jira.issueviews:searchrequest-xml/temp/SearchRequest.xml?tempMax=1",
                    "/rpc/soap/jirasoapservice-v2",
                    "/plugins/servlet/applinks/whoami"
                };

                bool anySuccess = false;
                
                // Test original URL first if it has a path
                if (inputUrl != baseUrl)
                {
                    try
                    {
                        var response = await httpClient.GetAsync(inputUrl);
                        var status = $"{response.StatusCode} ({(int)response.StatusCode})";
                        var result = response.IsSuccessStatusCode ? "✓ SUCCESS" : 
                                   response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "🔐 AUTH REQUIRED" :
                                   response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "🚫 FORBIDDEN" :
                                   "✗ FAILED";
                        
                        lstEndpoints.Items.Add($"{result} - ORIGINAL URL - {status}");
                        
                        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            anySuccess = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lstEndpoints.Items.Add($"✗ ERROR - ORIGINAL URL - {ex.Message}");
                    }
                }
                
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await httpClient.GetAsync($"{baseUrl}{endpoint}");
                        var status = $"{response.StatusCode} ({(int)response.StatusCode})";
                        var result = response.IsSuccessStatusCode ? "✓ SUCCESS" : 
                                   response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "🔐 AUTH REQUIRED" :
                                   response.StatusCode == System.Net.HttpStatusCode.Forbidden ? "🚫 FORBIDDEN" :
                                   "✗ FAILED";
                        
                        lstEndpoints.Items.Add($"{result} - {endpoint} - {status}");
                        
                        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            anySuccess = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        lstEndpoints.Items.Add($"✗ ERROR - {endpoint} - {ex.Message}");
                    }
                }

                if (anySuccess)
                {
                    lblStatus.Text = "✓ JIRA server detected! Some endpoints are accessible.";
                    lblStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    lblStatus.Text = "✗ No JIRA endpoints found. Check URL and credentials.";
                    lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"✗ Connection failed: {ex.Message}";
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
            }
            finally
            {
                btnTest.Enabled = true;
            }
        }
        
        private string GetCleanBaseUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                // Remove specific paths and keep only scheme + host + port
                var cleanUrl = $"{uri.Scheme}://{uri.Host}";
                if (!uri.IsDefaultPort)
                    cleanUrl += $":{uri.Port}";
                return cleanUrl;
            }
            catch
            {
                return url; // Return original if parsing fails
            }
        }
    }
}