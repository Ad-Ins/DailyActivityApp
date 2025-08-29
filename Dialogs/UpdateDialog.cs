using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdinersDailyActivityApp.Services;

namespace AdinersDailyActivityApp.Dialogs
{
    public partial class UpdateDialog : Form
    {
        private readonly GitHubRelease release;
        private Label lblMessage = null!;
        private ProgressBar progressBar = null!;
        private Button btnDownload = null!;
        private Button btnLater = null!;
        private Button btnOpenGitHub = null!;
        private Label lblProgress = null!;
        private bool isDownloading = false;

        public UpdateDialog(GitHubRelease release)
        {
            this.release = release;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(450, 280);
            this.Text = "Update Available";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.TopMost = false; // Non-blocking

            // Message
            lblMessage = new Label
            {
                Text = $"New version available: {release.tag_name}\n\n" +
                       $"Current version: {UpdateService.CurrentVersion}\n" +
                       $"Released: {release.published_at:dd/MM/yyyy}\n\n" +
                       "Would you like to download and install the update?",
                Location = new Point(20, 20),
                Size = new Size(390, 120),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 150),
                Size = new Size(390, 20),
                Visible = false
            };

            // Progress label
            lblProgress = new Label
            {
                Location = new Point(20, 175),
                Size = new Size(390, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Visible = false
            };

            // Buttons
            btnDownload = new Button
            {
                Text = "Download & Install",
                Size = new Size(120, 30),
                Location = new Point(20, 210),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White
            };
            btnDownload.FlatAppearance.BorderSize = 0;
            btnDownload.Click += BtnDownload_Click;

            btnOpenGitHub = new Button
            {
                Text = "Open GitHub",
                Size = new Size(100, 30),
                Location = new Point(150, 210),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            btnOpenGitHub.FlatAppearance.BorderSize = 0;
            btnOpenGitHub.Click += BtnOpenGitHub_Click;

            btnLater = new Button
            {
                Text = "Later",
                Size = new Size(80, 30),
                Location = new Point(330, 210),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel
            };
            btnLater.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[] { lblMessage, progressBar, lblProgress, btnDownload, btnOpenGitHub, btnLater });
        }

        private async void BtnDownload_Click(object? sender, EventArgs e)
        {
            if (isDownloading) return;

            isDownloading = true;
            btnDownload.Enabled = false;
            btnOpenGitHub.Enabled = false;
            progressBar.Visible = true;
            lblProgress.Visible = true;
            lblProgress.Text = "Downloading...";

            var progress = new Progress<int>(percentage =>
            {
                progressBar.Value = percentage;
                lblProgress.Text = $"Downloading... {percentage}%";
            });

            try
            {
                string? downloadPath = await UpdateService.DownloadUpdateAsync(release, progress);
                
                if (downloadPath != null)
                {
                    lblProgress.Text = "Download complete! Starting installer...";
                    
                    // Start installer
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = downloadPath,
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    };
                    
                    System.Diagnostics.Process.Start(startInfo);
                    
                    // Close application
                    Application.Exit();
                }
                else
                {
                    lblProgress.Text = "Download failed. Please try again.";
                    btnDownload.Enabled = true;
                    btnOpenGitHub.Enabled = true;
                    isDownloading = false;
                }
            }
            catch (Exception ex)
            {
                lblProgress.Text = $"Error: {ex.Message}";
                btnDownload.Enabled = true;
                btnOpenGitHub.Enabled = true;
                isDownloading = false;
            }
        }

        private void BtnOpenGitHub_Click(object? sender, EventArgs e)
        {
            UpdateService.OpenDownloadPage();
            this.Close();
        }
    }
}