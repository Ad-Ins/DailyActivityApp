using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AdinersDailyActivityApp.Services
{
    public class JiraV4Service : IJiraService
    {
        private readonly JiraInstanceConfig _config;
        private readonly HttpClient _httpClient;

        public string InstanceName => _config.Name;
        public string Version => _config.Version;
        public bool IsConnected { get; private set; }

        public JiraV4Service(JiraInstanceConfig config)
        {
            _config = config;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Basic Auth for legacy JIRA
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Clean base URL - remove specific paths like /defectlog
                var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
                
                // Test multiple endpoints for JIRA v4
                var endpoints = new[]
                {
                    "/rest/api/2/serverInfo",
                    "/rest/api/latest/serverInfo", 
                    "/sr/jira.issueviews:searchrequest-xml/temp/SearchRequest.xml?tempMax=1",
                    "/secure/Dashboard.jspa",
                    "/defectlog" // Test original path if it was provided
                };
                
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var testUrl = endpoint == "/defectlog" ? _config.BaseUrl : $"{baseUrl}{endpoint}";
                        var response = await _httpClient.GetAsync(testUrl);
                        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            IsConnected = true;
                            return true;
                        }
                    }
                    catch { continue; }
                }
                
                // Fallback to SOAP endpoint test
                try
                {
                    var soapRequest = CreateSoapRequest("getServerInfo", "");
                    var response = await _httpClient.PostAsync($"{baseUrl}/rpc/soap/jirasoapservice-v2", soapRequest);
                    IsConnected = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
                    return IsConnected;
                }
                catch
                {
                    IsConnected = false;
                    return false;
                }
            }
            catch
            {
                IsConnected = false;
                return false;
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

        public async Task<JiraIssue?> GetIssueAsync(string issueKey)
        {
            try
            {
                var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
                // Try REST API first
                var response = await _httpClient.GetAsync($"{baseUrl}/rest/api/2/issue/{issueKey}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseIssueFromJson(json, issueKey);
                }
                
                // Fallback to SOAP
                return await GetIssueViaSoapAsync(issueKey);
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
                var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
                var started = startTime ?? DateTime.Now;
                var timeSpentSeconds = (int)duration.TotalSeconds;
                
                // Try REST API first
                var worklogData = new
                {
                    timeSpentSeconds = timeSpentSeconds,
                    comment = comment,
                    started = started.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(worklogData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{baseUrl}/rest/api/2/issue/{issueKey}/worklog", content);
                if (response.IsSuccessStatusCode)
                    return true;
                
                // Fallback to SOAP
                return await LogWorkViaSoapAsync(issueKey, duration, comment, started);
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
                var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
                var response = await _httpClient.GetAsync($"{baseUrl}/rest/api/2/project");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseProjectsFromJson(json);
                }
                
                return await GetProjectsViaSoapAsync();
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
                var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
                var encodedJql = Uri.EscapeDataString(jql);
                var response = await _httpClient.GetAsync($"{baseUrl}/rest/api/2/search?jql={encodedJql}&maxResults=50");
                
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

        private StringContent CreateSoapRequest(string method, string parameters)
        {
            var soapEnvelope = $@"
                <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
                    <soap:Body>
                        <{method} xmlns=""http://soap.rpc.jira.atlassian.com"">
                            {parameters}
                        </{method}>
                    </soap:Body>
                </soap:Envelope>";
            
            return new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        }

        private async Task<JiraIssue?> GetIssueViaSoapAsync(string issueKey)
        {
            // Simplified SOAP implementation - would need full SOAP client for production
            var baseUrl = GetCleanBaseUrl(_config.BaseUrl);
            return new JiraIssue
            {
                Key = issueKey,
                Summary = "Legacy JIRA Issue",
                Status = "Unknown",
                ProjectKey = issueKey.Split('-')[0],
                Url = $"{baseUrl}/browse/{issueKey}"
            };
        }

        private async Task<bool> LogWorkViaSoapAsync(string issueKey, TimeSpan duration, string comment, DateTime started)
        {
            // Simplified - would implement full SOAP worklog creation
            return false;
        }

        private async Task<List<JiraProject>> GetProjectsViaSoapAsync()
        {
            // Simplified - would implement SOAP project retrieval
            return new List<JiraProject>();
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
                    Summary = fields.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : "",
                    Description = fields.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    Status = fields.TryGetProperty("status", out var status) ? 
                        status.TryGetProperty("name", out var statusName) ? statusName.GetString() ?? "" : "" : "",
                    ProjectKey = fields.TryGetProperty("project", out var project) ? 
                        project.TryGetProperty("key", out var projKey) ? projKey.GetString() ?? "" : "" : "",
                    Url = $"{GetCleanBaseUrl(_config.BaseUrl)}/browse/{issueKey}"
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
                        Key = element.TryGetProperty("key", out var key) ? key.GetString() ?? "" : "",
                        Name = element.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        Description = element.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : ""
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
                    var key = issue.TryGetProperty("key", out var keyProp) ? keyProp.GetString() ?? "" : "";
                    var fields = issue.GetProperty("fields");
                    
                    issues.Add(new JiraIssue
                    {
                        Key = key,
                        Summary = fields.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : "",
                        Status = fields.TryGetProperty("status", out var status) ? 
                            status.TryGetProperty("name", out var statusName) ? statusName.GetString() ?? "" : "" : "",
                        ProjectKey = fields.TryGetProperty("project", out var project) ? 
                            project.TryGetProperty("key", out var projKey) ? projKey.GetString() ?? "" : "" : "",
                        Url = $"{GetCleanBaseUrl(_config.BaseUrl)}/browse/{key}"
                    });
                }
            }
            catch { }
            return issues;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}