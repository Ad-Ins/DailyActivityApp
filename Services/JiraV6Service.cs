using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdinersDailyActivityApp.Services
{
    public class JiraV6Service : IJiraService
    {
        private readonly JiraInstanceConfig _config;
        private readonly HttpClient _httpClient;

        public string InstanceName => _config.Name;
        public string Version => _config.Version;
        public bool IsConnected { get; private set; }

        public JiraV6Service(JiraInstanceConfig config)
        {
            _config = config;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Basic Auth for JIRA v6
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            
            // Add custom headers if configured
            foreach (var header in _config.CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_config.BaseUrl}/rest/api/2/serverInfo");
                IsConnected = response.IsSuccessStatusCode;
                return IsConnected;
            }
            catch
            {
                IsConnected = false;
                return false;
            }
        }

        public async Task<JiraIssue?> GetIssueAsync(string issueKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_config.BaseUrl}/rest/api/2/issue/{issueKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseIssueFromJson(json, issueKey);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<JiraIssue>> GetMyIssuesAsync()
        {
            try
            {
                var jql = $"assignee = currentUser() AND resolution = Unresolved ORDER BY updated DESC";
                return await SearchIssuesAsync(jql);
            }
            catch
            {
                return new List<JiraIssue>();
            }
        }

        public async Task<bool> LogWorkAsync(string issueKey, TimeSpan duration, string comment, DateTime? startTime = null)
        {
            try
            {
                var started = startTime ?? DateTime.Now;
                var timeSpentSeconds = (int)duration.TotalSeconds;
                
                var worklogData = new
                {
                    timeSpentSeconds = timeSpentSeconds,
                    comment = comment,
                    started = started.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(worklogData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/rest/api/2/issue/{issueKey}/worklog", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<JiraProject>> GetProjectsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_config.BaseUrl}/rest/api/2/project");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseProjectsFromJson(json);
                }
                return new List<JiraProject>();
            }
            catch
            {
                return new List<JiraProject>();
            }
        }

        public async Task<List<JiraIssue>> SearchIssuesAsync(string jql)
        {
            try
            {
                var encodedJql = Uri.EscapeDataString(jql);
                var response = await _httpClient.GetAsync($"{_config.BaseUrl}/rest/api/2/search?jql={encodedJql}&maxResults=50&fields=key,summary,status,project,assignee,priority,issuetype");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseIssuesFromSearchJson(json);
                }
                
                return new List<JiraIssue>();
            }
            catch
            {
                return new List<JiraIssue>();
            }
        }

        private JiraIssue? ParseIssueFromJson(string json, string issueKey)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var fields = root.GetProperty("fields");
                
                return new JiraIssue
                {
                    Key = issueKey,
                    Summary = GetStringProperty(fields, "summary"),
                    Description = GetStringProperty(fields, "description"),
                    Status = GetNestedStringProperty(fields, "status", "name"),
                    Priority = GetNestedStringProperty(fields, "priority", "name"),
                    Assignee = GetNestedStringProperty(fields, "assignee", "displayName"),
                    Reporter = GetNestedStringProperty(fields, "reporter", "displayName"),
                    ProjectKey = GetNestedStringProperty(fields, "project", "key"),
                    ProjectName = GetNestedStringProperty(fields, "project", "name"),
                    IssueType = GetNestedStringProperty(fields, "issuetype", "name"),
                    Url = $"{_config.BaseUrl}/browse/{issueKey}",
                    Created = GetDateTimeProperty(fields, "created"),
                    Updated = GetDateTimeProperty(fields, "updated")
                };
            }
            catch
            {
                return null;
            }
        }

        private List<JiraProject> ParseProjectsFromJson(string json)
        {
            var projects = new List<JiraProject>();
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    projects.Add(new JiraProject
                    {
                        Key = GetStringProperty(element, "key"),
                        Name = GetStringProperty(element, "name"),
                        Description = GetStringProperty(element, "description"),
                        Lead = GetNestedStringProperty(element, "lead", "displayName"),
                        Url = $"{_config.BaseUrl}/browse/{GetStringProperty(element, "key")}"
                    });
                }
            }
            catch { }
            return projects;
        }

        private List<JiraIssue> ParseIssuesFromSearchJson(string json)
        {
            var issues = new List<JiraIssue>();
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var issuesArray = doc.RootElement.GetProperty("issues");
                
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    var key = GetStringProperty(issue, "key");
                    var fields = issue.GetProperty("fields");
                    
                    issues.Add(new JiraIssue
                    {
                        Key = key,
                        Summary = GetStringProperty(fields, "summary"),
                        Status = GetNestedStringProperty(fields, "status", "name"),
                        Priority = GetNestedStringProperty(fields, "priority", "name"),
                        Assignee = GetNestedStringProperty(fields, "assignee", "displayName"),
                        ProjectKey = GetNestedStringProperty(fields, "project", "key"),
                        ProjectName = GetNestedStringProperty(fields, "project", "name"),
                        IssueType = GetNestedStringProperty(fields, "issuetype", "name"),
                        Url = $"{_config.BaseUrl}/browse/{key}",
                        Created = GetDateTimeProperty(fields, "created"),
                        Updated = GetDateTimeProperty(fields, "updated")
                    });
                }
            }
            catch { }
            return issues;
        }

        private string GetStringProperty(System.Text.Json.JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() ?? "" : "";
        }

        private string GetNestedStringProperty(System.Text.Json.JsonElement element, string parentProperty, string childProperty)
        {
            if (element.TryGetProperty(parentProperty, out var parent) && parent.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                return parent.TryGetProperty(childProperty, out var child) ? child.GetString() ?? "" : "";
            }
            return "";
        }

        private DateTime GetDateTimeProperty(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && DateTime.TryParse(prop.GetString(), out var date))
            {
                return date;
            }
            return DateTime.MinValue;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}