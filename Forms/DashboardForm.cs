using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdinersDailyActivityApp.Forms
{
    public partial class DashboardForm : Form
    {
        private ComboBox cmbGroupBy;
        private Panel barChartPanel;
        private Panel pieChartPanel;
        private Panel topPanel;
        private Panel chartPanel;
        private DateTimePicker dtpFromDate;
        private DateTimePicker dtpToDate;
        private ComboBox cmbDatePreset;
        private List<ActivityEntry> activities = new List<ActivityEntry>();
        private DateTime currentFromDate;
        private DateTime currentToDate;
        private Dictionary<string, TimeSpan> currentBarData = new Dictionary<string, TimeSpan>();
        private Dictionary<string, TimeSpan> currentPieData = new Dictionary<string, TimeSpan>();
        
        public class ActivityEntry
        {
            public DateTime Date { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string Type { get; set; } = "";
            public string Activity { get; set; } = "";
            public TimeSpan Duration => EndTime - StartTime;
        }
        
        public DashboardForm()
        {
            // Set default date range to last 7 days
            currentToDate = DateTime.Today;
            currentFromDate = DateTime.Today.AddDays(-6);
            
            InitializeComponent();
            LoadDashboardData();
        }

        private void InitializeComponent()
        {
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(30, 35, 40);
            this.ForeColor = Color.White;
            this.Text = "Activity Dashboard";
            this.FormBorderStyle = FormBorderStyle.None;
            this.KeyPreview = true;
            this.TopMost = false; // Allow proper control interaction
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // Top Panel with metrics
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                BackColor = Color.FromArgb(40, 45, 50),
                Padding = new Padding(20)
            };

            var metricsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Group By
            metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // Date Preset
            metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); // Date Range

            // Group By ComboBox
            var groupPanel = CreateMetricPanel("Group By", "");
            cmbGroupBy = new ComboBox
            {
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(50, 55, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Standard, // Change to Standard for better interaction
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = true,
                TabStop = true
            };
            cmbGroupBy.Items.AddRange(new[] { "By User", "By Task", "By Project" });
            cmbGroupBy.SelectedIndex = 1; // Default to "By Task"
            cmbGroupBy.SelectedIndexChanged += (s, e) => {
                System.Diagnostics.Debug.WriteLine($"Group By changed to: {cmbGroupBy.SelectedItem}");
                LoadDashboardData();
            };
            cmbGroupBy.Click += (s, e) => System.Diagnostics.Debug.WriteLine("Group By clicked");
            groupPanel.Controls.Add(cmbGroupBy);
            cmbGroupBy.BringToFront();
            
            // Date Preset ComboBox
            var datePresetPanel = CreateMetricPanel("Date Range", "");
            cmbDatePreset = new ComboBox
            {
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(50, 55, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Standard, // Change to Standard for better interaction
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Bottom,
                Height = 30,
                Enabled = true,
                TabStop = true
            };
            cmbDatePreset.Items.AddRange(new[] { "Today", "Yesterday", "This week", "Last week", "Past two weeks", "This month", "Last month", "This year", "Last year", "Custom" });
            cmbDatePreset.SelectedIndex = 2; // Default to "This week"
            cmbDatePreset.SelectedIndexChanged += OnDatePresetChanged;
            cmbDatePreset.Click += (s, e) => System.Diagnostics.Debug.WriteLine("Date Preset clicked");
            datePresetPanel.Controls.Add(cmbDatePreset);
            cmbDatePreset.BringToFront();
            
            // Add close button
            var closeButton = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Size = new Size(40, 40),
                Location = new Point(this.Width - 60, 20),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(closeButton);
            
            // Set initial date preset
            SetDatePreset("This week");
            
            // Force initial load
            this.Load += (s, e) => LoadDashboardData();

            // Date Range Panel
            var dateRangePanel = CreateMetricPanel("From - To", "");
            var dateLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            dateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            
            dtpFromDate = new DateTimePicker
            {
                Font = new Font("Segoe UI", 8),
                Format = DateTimePickerFormat.Short,
                Value = currentFromDate,
                Dock = DockStyle.Fill,
                Enabled = true,
                TabStop = true
            };
            dtpFromDate.ValueChanged += (s, e) => { currentFromDate = dtpFromDate.Value; LoadDashboardData(); };
            
            var dashLabel = new Label
            {
                Text = "-",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Dock = DockStyle.Fill
            };
            
            dtpToDate = new DateTimePicker
            {
                Font = new Font("Segoe UI", 8),
                Format = DateTimePickerFormat.Short,
                Value = currentToDate,
                Dock = DockStyle.Fill,
                Enabled = true,
                TabStop = true
            };
            dtpToDate.ValueChanged += (s, e) => { currentToDate = dtpToDate.Value; LoadDashboardData(); };
            
            dateLayout.Controls.Add(dtpFromDate, 0, 0);
            dateLayout.Controls.Add(dashLabel, 1, 0);
            dateLayout.Controls.Add(dtpToDate, 2, 0);
            dateRangePanel.Controls.Add(dateLayout);
            dateLayout.BringToFront();
            dtpFromDate.BringToFront();
            dtpToDate.BringToFront();
            
            metricsLayout.Controls.Add(groupPanel, 0, 0);
            metricsLayout.Controls.Add(datePresetPanel, 1, 0);
            metricsLayout.Controls.Add(dateRangePanel, 2, 0);
            topPanel.Controls.Add(metricsLayout);

            // Chart Panel
            chartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 35, 40),
                Padding = new Padding(20)
            };

            var chartLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            chartLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

            // Bar Chart Panel
            barChartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 35, 40),
                Margin = new Padding(0, 0, 10, 0)
            };
            barChartPanel.Paint += BarChartPanel_Paint;

            // Pie Chart Panel
            pieChartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 35, 40),
                Margin = new Padding(10, 0, 0, 0)
            };
            pieChartPanel.Paint += PieChartPanel_Paint;

            chartLayout.Controls.Add(barChartPanel, 0, 0);
            chartLayout.Controls.Add(pieChartPanel, 1, 0);
            chartPanel.Controls.Add(chartLayout);

            this.Controls.Add(chartPanel);
            this.Controls.Add(topPanel);
            
            // Ensure proper z-order
            topPanel.BringToFront();
            closeButton.BringToFront();
        }

        private Panel CreateMetricPanel(string title, string value)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(10)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150),
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var valueLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private void BarChartPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = barChartPanel.ClientRectangle;
            
            // Draw title
            var groupBy = cmbGroupBy?.SelectedItem?.ToString() ?? "By Project";
            string dateRangeText = currentFromDate == currentToDate ? 
                currentFromDate.ToString("MMM dd, yyyy") : 
                $"{currentFromDate:MMM dd} - {currentToDate:MMM dd, yyyy}";
                
            string titleText = groupBy switch
            {
                "By Task" => $"Activity Types - Time Spent ({dateRangeText})",
                "By User" => $"Daily Hours ({dateRangeText})",
                _ => $"Daily Hours ({dateRangeText})"
            };
            
            using (var titleBrush = new SolidBrush(Color.White))
            using (var titleFont = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                var titleSize = g.MeasureString(titleText, titleFont);
                g.DrawString(titleText, titleFont, titleBrush, 
                    (rect.Width - titleSize.Width) / 2, 10);
            }
            
            if (!currentBarData.Any()) return;
            
            // Chart area
            var chartRect = new Rectangle(50, 50, rect.Width - 100, rect.Height - 100);
            var maxValue = currentBarData.Values.Max().TotalHours;
            if (maxValue == 0) maxValue = 1;
            
            var barWidth = chartRect.Width / Math.Max(currentBarData.Count, 1);
            var x = chartRect.X;
            
            using (var barBrush = new SolidBrush(Color.FromArgb(138, 43, 226)))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 8))
            {
                foreach (var item in currentBarData)
                {
                    var barHeight = (int)((item.Value.TotalHours / maxValue) * chartRect.Height * 0.8);
                    var barRect = new Rectangle(x + 5, chartRect.Bottom - barHeight, barWidth - 10, barHeight);
                    
                    g.FillRectangle(barBrush, barRect);
                    
                    // Label
                    var labelText = item.Key.Length > 8 ? item.Key.Substring(0, 8) + "..." : item.Key;
                    var labelSize = g.MeasureString(labelText, font);
                    g.DrawString(labelText, font, textBrush, 
                        x + (barWidth - labelSize.Width) / 2, chartRect.Bottom + 5);
                    
                    // Value label
                    if (item.Value.TotalMinutes > 0)
                    {
                        var valueText = $"{item.Value.TotalHours:F1}h";
                        var valueSize = g.MeasureString(valueText, font);
                        g.DrawString(valueText, font, textBrush, 
                            x + (barWidth - valueSize.Width) / 2, barRect.Y - 20);
                    }
                    
                    x += barWidth;
                }
            }
        }

        private void PieChartPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = pieChartPanel.ClientRectangle;
            
            // Draw title
            using (var titleBrush = new SolidBrush(Color.White))
            using (var titleFont = new Font("Segoe UI", 14, FontStyle.Bold))
            {
                var titleText = "Time Distribution by Activity";
                var titleSize = g.MeasureString(titleText, titleFont);
                g.DrawString(titleText, titleFont, titleBrush, 
                    (rect.Width - titleSize.Width) / 2, 10);
            }
            
            if (!currentPieData.Any() || currentPieData.Values.All(v => v.TotalMinutes == 0))
            {
                using (var emptyBrush = new SolidBrush(Color.FromArgb(60, 65, 70)))
                using (var textBrush = new SolidBrush(Color.White))
                using (var font = new Font("Segoe UI", 10))
                {
                    var centerX = rect.Width / 2;
                    var centerY = rect.Height / 2;
                    var radius = Math.Min(rect.Width, rect.Height) / 4;
                    
                    g.FillEllipse(emptyBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);
                    
                    var noDataText = "No Data";
                    var textSize = g.MeasureString(noDataText, font);
                    g.DrawString(noDataText, font, textBrush, 
                        centerX - textSize.Width / 2, centerY - textSize.Height / 2);
                }
                return;
            }
            
            var totalMinutes = currentPieData.Values.Sum(v => v.TotalMinutes);
            if (totalMinutes == 0) return;
            
            var colors = new Color[] {
                Color.FromArgb(138, 43, 226),
                Color.FromArgb(255, 99, 132),
                Color.FromArgb(54, 162, 235),
                Color.FromArgb(255, 205, 86),
                Color.FromArgb(75, 192, 192)
            };
            
            var pieCenterX = rect.Width / 2;
            var pieCenterY = rect.Height / 2 + 10;
            var pieRadius = Math.Min(rect.Width, rect.Height) / 4;
            var innerRadius = pieRadius * 0.6f;
            
            float startAngle = 0;
            int colorIndex = 0;
            
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 8))
            {
                foreach (var item in currentPieData.OrderByDescending(x => x.Value.TotalMinutes).Take(5))
                {
                    if (item.Value.TotalMinutes > 0)
                    {
                        var percentage = (float)(item.Value.TotalMinutes / totalMinutes) * 100;
                        var sweepAngle = (float)(item.Value.TotalMinutes / totalMinutes) * 360;
                        
                        using (var brush = new SolidBrush(colors[colorIndex % colors.Length]))
                        {
                            // Draw outer arc
                            g.FillPie(brush, pieCenterX - pieRadius, pieCenterY - pieRadius, pieRadius * 2, pieRadius * 2, startAngle, sweepAngle);
                            
                            // Draw inner circle (doughnut effect)
                            using (var innerBrush = new SolidBrush(Color.FromArgb(30, 35, 40)))
                            {
                                g.FillEllipse(innerBrush, pieCenterX - innerRadius, pieCenterY - innerRadius, innerRadius * 2, innerRadius * 2);
                            }
                            
                            // Draw percentage label
                            var labelAngle = startAngle + sweepAngle / 2;
                            var labelRadius = (pieRadius + innerRadius) / 2;
                            var labelX = pieCenterX + (float)(Math.Cos(labelAngle * Math.PI / 180) * labelRadius);
                            var labelY = pieCenterY + (float)(Math.Sin(labelAngle * Math.PI / 180) * labelRadius);
                            
                            var labelText = $"{percentage:F1}%";
                            var labelSize = g.MeasureString(labelText, font);
                            g.DrawString(labelText, font, textBrush, 
                                labelX - labelSize.Width / 2, labelY - labelSize.Height / 2);
                        }
                        
                        startAngle += sweepAngle;
                        colorIndex++;
                    }
                }
                
                // Draw legend with better formatting
                var legendY = pieCenterY + pieRadius + 30;
                var legendX = 10;
                colorIndex = 0;
                
                using (var legendFont = new Font("Segoe UI", 9, FontStyle.Bold))
                {
                    foreach (var item in currentPieData.OrderByDescending(x => x.Value.TotalMinutes).Take(5))
                    {
                        if (item.Value.TotalMinutes > 0)
                        {
                            using (var brush = new SolidBrush(colors[colorIndex % colors.Length]))
                            {
                                g.FillRectangle(brush, legendX, legendY, 15, 15);
                            }
                            
                            // Format time duration
                            var hours = (int)item.Value.TotalHours;
                            var minutes = item.Value.Minutes;
                            var timeText = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
                            
                            var legendText = $"{item.Key} - {timeText}";
                            if (legendText.Length > 25) legendText = legendText.Substring(0, 22) + "...";
                            
                            g.DrawString(legendText, legendFont, textBrush, legendX + 20, legendY);
                            
                            legendY += 20;
                            colorIndex++;
                        }
                    }
                }
            }
        }

        private void OnDatePresetChanged(object? sender, EventArgs e)
        {
            var preset = cmbDatePreset.SelectedItem?.ToString();
            System.Diagnostics.Debug.WriteLine($"Date Preset changed to: {preset}");
            if (preset != null && preset != "Custom")
            {
                SetDatePreset(preset);
                LoadDashboardData();
            }
        }
        
        private void SetDatePreset(string preset)
        {
            switch (preset)
            {
                case "Today":
                    currentFromDate = DateTime.Today;
                    currentToDate = DateTime.Today;
                    break;
                case "Yesterday":
                    currentFromDate = DateTime.Today.AddDays(-1);
                    currentToDate = DateTime.Today.AddDays(-1);
                    break;
                case "This week":
                    var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                    currentFromDate = startOfWeek;
                    currentToDate = DateTime.Today;
                    break;
                case "Last week":
                    var lastWeekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek - 7);
                    currentFromDate = lastWeekStart;
                    currentToDate = lastWeekStart.AddDays(6);
                    break;
                case "Past two weeks":
                    currentFromDate = DateTime.Today.AddDays(-14);
                    currentToDate = DateTime.Today;
                    break;
                case "This month":
                    currentFromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    currentToDate = DateTime.Today;
                    break;
                case "Last month":
                    var lastMonth = DateTime.Today.AddMonths(-1);
                    currentFromDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    currentToDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
                    break;
                case "This year":
                    currentFromDate = new DateTime(DateTime.Today.Year, 1, 1);
                    currentToDate = DateTime.Today;
                    break;
                case "Last year":
                    var lastYear = DateTime.Today.Year - 1;
                    currentFromDate = new DateTime(lastYear, 1, 1);
                    currentToDate = new DateTime(lastYear, 12, 31);
                    break;
            }
            
            // Update date pickers
            if (dtpFromDate != null) dtpFromDate.Value = currentFromDate;
            if (dtpToDate != null) dtpToDate.Value = currentToDate;
        }
        
        private void LoadDashboardData()
        {
            LoadActivitiesFromLog();
            
            var groupBy = cmbGroupBy?.SelectedItem?.ToString() ?? "By Project";
            var dateRange = GetDateRangeList();
            
            // Filter activities by date range first
            var filteredActivities = activities.Where(a => a.Date >= currentFromDate && a.Date <= currentToDate).ToList();
            
            // Calculate data based on grouping
            Dictionary<string, TimeSpan> chartData;
            Dictionary<string, TimeSpan> pieData;
            
            switch (groupBy)
            {
                case "By User":
                    chartData = GetDataByUser(filteredActivities, dateRange);
                    pieData = GetPieDataByUser(filteredActivities);
                    break;
                case "By Task":
                    chartData = GetDataByTask(filteredActivities);
                    pieData = GetPieDataByTask(filteredActivities);
                    break;
                default: // By Project
                    chartData = GetDataByProject(filteredActivities, dateRange);
                    pieData = GetPieDataByProject(filteredActivities);
                    break;
            }
            
            UpdateBarChart(chartData, dateRange);
            UpdatePieChart(pieData);
            UpdateMetrics(filteredActivities);
        }
        
        private void LoadActivitiesFromLog()
        {
            activities.Clear();
            string logFilePath = GetLogFilePath();
            
            if (!File.Exists(logFilePath)) return;
            
            string[] lines = File.ReadAllLines(logFilePath);
            var entries = new List<(DateTime time, string type, string activity)>();
            
            foreach (string line in lines)
            {
                int timestampEndIndex = line.IndexOf(']');
                if (timestampEndIndex > 0 && timestampEndIndex + 2 < line.Length)
                {
                    string timestampStr = line.Substring(1, timestampEndIndex - 1);
                    if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                    {
                        string rest = line.Substring(timestampEndIndex + 2).Trim();
                        
                        // Remove sync flags
                        if (rest.StartsWith("[SYNCED]") || rest.StartsWith("[LOCAL]"))
                        {
                            int flagEnd = rest.IndexOf(']', 1);
                            if (flagEnd != -1) rest = rest.Substring(flagEnd + 1).Trim();
                        }
                        
                        // Remove Clockify ID if present
                        int cidIndex = rest.IndexOf(" [CID:");
                        if (cidIndex != -1)
                        {
                            int endIndex = rest.IndexOf("]", cidIndex);
                            if (endIndex != -1)
                            {
                                rest = rest.Substring(0, cidIndex) + rest.Substring(endIndex + 1);
                            }
                        }
                        
                        string type = "General";
                        string activity = rest;
                        int pipeIndex = rest.IndexOf('|');
                        if (pipeIndex > 0)
                        {
                            type = rest.Substring(0, pipeIndex).Trim();
                            activity = rest.Substring(pipeIndex + 1).Trim();
                        }
                        
                        entries.Add((time, type, activity));
                    }
                }
            }
            
            // Convert to activities with durations
            entries.Sort((a, b) => a.time.CompareTo(b.time));
            
            // Group by date and process each day
            var dateGroups = entries.GroupBy(e => e.time.Date).OrderBy(g => g.Key);
            
            foreach (var dateGroup in dateGroups)
            {
                DateTime date = dateGroup.Key;
                var dayEntries = dateGroup.OrderBy(e => e.time).ToList();
                
                // Process entries sequentially to calculate durations
                for (int i = 0; i < dayEntries.Count; i++)
                {
                    var currentEntry = dayEntries[i];
                    DateTime startTime;
                    DateTime endTime = currentEntry.time;
                    
                    if (i == 0)
                    {
                        // First entry of the day - assume started at 8 AM or actual time if earlier
                        startTime = new DateTime(date.Year, date.Month, date.Day, 8, 0, 0);
                        if (endTime < startTime)
                        {
                            startTime = endTime.AddHours(-1); // Assume 1 hour duration for early entries
                        }
                    }
                    else
                    {
                        // Use previous entry time as start time
                        startTime = dayEntries[i - 1].time;
                    }
                    
                    // Only add if duration is positive and reasonable (less than 12 hours)
                    if (endTime > startTime && (endTime - startTime).TotalHours <= 12)
                    {
                        activities.Add(new ActivityEntry
                        {
                            Date = date,
                            StartTime = startTime,
                            EndTime = endTime,
                            Type = string.IsNullOrEmpty(currentEntry.type) ? "General" : currentEntry.type,
                            Activity = currentEntry.activity
                        });
                    }
                }
            }
        }
        
        private List<DateTime> GetDateRangeList()
        {
            var days = new List<DateTime>();
            var current = currentFromDate;
            while (current <= currentToDate)
            {
                days.Add(current);
                current = current.AddDays(1);
            }
            return days;
        }
        
        private Dictionary<string, TimeSpan> GetDataByProject(List<ActivityEntry> filteredActivities, List<DateTime> days)
        {
            var data = new Dictionary<string, TimeSpan>();
            foreach (var day in days)
            {
                var dayActivities = filteredActivities.Where(a => a.Date.Date == day.Date);
                var totalDuration = dayActivities.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);
                if (totalDuration.TotalMinutes > 0)
                {
                    data[day.ToString("ddd, MMM dd")] = totalDuration;
                }
            }
            return data;
        }
        
        private Dictionary<string, TimeSpan> GetDataByTask(List<ActivityEntry> filteredActivities)
        {
            var taskData = new Dictionary<string, TimeSpan>();
            
            foreach (var activity in filteredActivities)
            {
                string key = string.IsNullOrEmpty(activity.Type) ? "General" : activity.Type;
                if (!taskData.ContainsKey(key)) taskData[key] = TimeSpan.Zero;
                taskData[key] += activity.Duration;
            }
            return taskData.Where(x => x.Value.TotalMinutes > 0)
                          .OrderByDescending(x => x.Value.TotalMinutes)
                          .Take(10)
                          .ToDictionary(x => x.Key, x => x.Value);
        }
        
        private Dictionary<string, TimeSpan> GetDataByUser(List<ActivityEntry> filteredActivities, List<DateTime> days)
        {
            // For single user app, group by day
            return GetDataByProject(filteredActivities, days);
        }
        
        private Dictionary<string, TimeSpan> GetPieDataByProject(List<ActivityEntry> filteredActivities)
        {
            return filteredActivities.GroupBy(a => string.IsNullOrEmpty(a.Type) ? "General" : a.Type)
                .Where(g => g.Sum(a => a.Duration.TotalMinutes) > 0)
                .ToDictionary(g => g.Key, g => g.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration));
        }
        
        private Dictionary<string, TimeSpan> GetPieDataByTask(List<ActivityEntry> filteredActivities)
        {
            return GetPieDataByProject(filteredActivities); // Same as project for this app
        }
        
        private Dictionary<string, TimeSpan> GetPieDataByUser(List<ActivityEntry> filteredActivities)
        {
            var totalTime = filteredActivities.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);
            if (totalTime.TotalMinutes > 0)
            {
                return new Dictionary<string, TimeSpan> { ["Current User"] = totalTime };
            }
            return new Dictionary<string, TimeSpan>();
        }
        
        private void UpdateBarChart(Dictionary<string, TimeSpan> data, List<DateTime> days)
        {
            currentBarData = data;
            barChartPanel.Invalidate();
        }
        
        private void UpdatePieChart(Dictionary<string, TimeSpan> data)
        {
            currentPieData = data;
            pieChartPanel.Invalidate();
        }
        
        private void UpdateMetrics(List<ActivityEntry> filteredActivities)
        {
            // Metrics removed for simplicity
        }
        
        private string GetLogFilePath()
        {
            string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdinersDailyActivity");
            Directory.CreateDirectory(appDataDir);
            return Path.Combine(appDataDir, "activity_log.txt");
        }
    }
}