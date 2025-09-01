using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdinersDailyActivityApp.Services
{
    public class MultiJiraService
    {
        private readonly List<IJiraService> _jiraServices = new();
        private readonly MultiJiraConfig _config;
        private readonly Dictionary<string, IJiraService> _projectToServiceMap = new();
        private readonly Dictionary<string, List<JiraIssue>> _issueCache = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        public MultiJiraService()
        {
            _config = LoadConfig();
            InitializeServices();
        }

        public List<IJiraService> GetAllServices() => _jiraServices.ToList();
        public List<IJiraService> GetConnectedServices() => _jiraServices.Where(s => s.IsConnected).ToList();

        public async Task<bool> TestAllConnectionsAsync()
        {
            var tasks = _jiraServices.Select(service => service.TestConnectionAsync());
            var results = await Task.WhenAll(tasks);
            return results.Any(r => r);
        }

        public async Task<JiraIssue?> GetIssueAsync(string issueKey)
        {
            // Try to detect which JIRA instance based on project key
            var projectKey = ExtractProjectKey(issueKey);
            var targetService = DetectJiraService(projectKey);

            if (targetService != null)
            {
                var issue = await targetService.GetIssueAsync(issueKey);
                if (issue != null) return issue;
            }

            // Fallback: try all connected services
            foreach (var service in GetConnectedServices())
            {
                if (service == targetService) continue; // Already tried
                
                var issue = await service.GetIssueAsync(issueKey);
                if (issue != null)
                {
                    // Cache the mapping for future use
                    _projectToServiceMap[projectKey] = service;
                    return issue;
                }
            }

            return null;
        }

        public async Task<List<JiraIssue>> GetAllMyIssuesAsync()
        {
            if (_config.EnableCaching && IsCacheValid())
            {
                return _issueCache.Values.SelectMany(issues => issues).ToList();
            }

            var allIssues = new List<JiraIssue>();
            var tasks = GetConnectedServices().Select(async service =>
            {
                try
                {
                    var issues = await service.GetMyIssuesAsync();
                    lock (allIssues)
                    {
                        allIssues.AddRange(issues);
                        if (_config.EnableCaching)
                        {
                            _issueCache[service.InstanceName] = issues;
                        }
                    }
                }
                catch { /* Ignore individual service failures */ }
            });

            await Task.WhenAll(tasks);
            
            if (_config.EnableCaching)
            {
                _lastCacheUpdate = DateTime.Now;
            }

            return allIssues.OrderByDescending(i => i.Updated).ToList();
        }

        public async Task<bool> LogWorkAsync(string issueKey, TimeSpan duration, string comment, DateTime? startTime = null)
        {
            var projectKey = ExtractProjectKey(issueKey);
            var targetService = DetectJiraService(projectKey);

            if (targetService != null)
            {
                return await targetService.LogWorkAsync(issueKey, duration, comment, startTime);
            }

            // Fallback: try all connected services until one succeeds
            foreach (var service in GetConnectedServices())
            {
                try
                {
                    var success = await service.LogWorkAsync(issueKey, duration, comment, startTime);
                    if (success)
                    {
                        // Cache the mapping for future use
                        _projectToServiceMap[projectKey] = service;
                        return true;
                    }
                }
                catch { /* Continue to next service */ }
            }

            return false;
        }

        public async Task<List<JiraProject>> GetAllProjectsAsync()
        {
            var allProjects = new List<JiraProject>();
            var tasks = GetConnectedServices().Select(async service =>
            {
                try
                {
                    var projects = await service.GetProjectsAsync();
                    lock (allProjects)
                    {
                        // Add service name to project for identification
                        foreach (var project in projects)
                        {
                            project.Name = $"[{service.InstanceName}] {project.Name}";
                        }
                        allProjects.AddRange(projects);
                    }
                }
                catch { /* Ignore individual service failures */ }
            });

            await Task.WhenAll(tasks);
            return allProjects.OrderBy(p => p.Name).ToList();
        }

        public async Task<List<JiraIssue>> SearchAllInstancesAsync(string jql)
        {
            if (!_config.EnableCrossInstanceSearch)
            {
                // Use default instance only
                var defaultService = GetDefaultService();
                if (defaultService != null)
                {
                    return await defaultService.SearchIssuesAsync(jql);
                }
                return new List<JiraIssue>();
            }

            var allIssues = new List<JiraIssue>();
            var tasks = GetConnectedServices().Select(async service =>
            {
                try
                {
                    var issues = await service.SearchIssuesAsync(jql);
                    lock (allIssues)
                    {
                        allIssues.AddRange(issues);
                    }
                }
                catch { /* Ignore individual service failures */ }
            });

            await Task.WhenAll(tasks);
            return allIssues.OrderByDescending(i => i.Updated).ToList();
        }

        public IJiraService? GetServiceByName(string name)
        {
            return _jiraServices.FirstOrDefault(s => s.InstanceName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public IJiraService? GetServiceForProject(string projectKey)
        {
            return DetectJiraService(projectKey);
        }

        public void InvalidateCache()
        {
            _issueCache.Clear();
            _lastCacheUpdate = DateTime.MinValue;
        }

        private void InitializeServices()
        {
            foreach (var instanceConfig in _config.Instances.Where(i => i.Enabled))
            {
                IJiraService service = instanceConfig.Version switch
                {
                    var v when v.StartsWith("4.") || v.StartsWith("3.") => new JiraV4Service(instanceConfig),
                    var v when v.StartsWith("6.") || v.StartsWith("7.") || v.StartsWith("8.") => new JiraV6Service(instanceConfig),
                    "Cloud" => new JiraCloudService(instanceConfig),
                    _ => new JiraV6Service(instanceConfig) // Default to v6 for unknown versions
                };

                _jiraServices.Add(service);

                // Pre-populate project mappings
                foreach (var projectKey in instanceConfig.Projects)
                {
                    _projectToServiceMap[projectKey.ToUpper()] = service;
                }
            }

            // Sort by priority
            _jiraServices.Sort((a, b) => GetServicePriority(a).CompareTo(GetServicePriority(b)));
        }

        private IJiraService? DetectJiraService(string projectKey)
        {
            if (string.IsNullOrEmpty(projectKey)) return GetDefaultService();

            var upperProjectKey = projectKey.ToUpper();
            
            // Check explicit mapping first
            if (_projectToServiceMap.TryGetValue(upperProjectKey, out var mappedService))
            {
                return mappedService;
            }

            // Check configured project lists
            foreach (var service in _jiraServices)
            {
                var config = GetConfigForService(service);
                if (config?.Projects.Any(p => p.Equals(upperProjectKey, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    _projectToServiceMap[upperProjectKey] = service;
                    return service;
                }
            }

            return GetDefaultService();
        }

        private IJiraService? GetDefaultService()
        {
            if (!string.IsNullOrEmpty(_config.DefaultInstance))
            {
                var defaultService = GetServiceByName(_config.DefaultInstance);
                if (defaultService?.IsConnected == true) return defaultService;
            }

            // Return first connected service with highest priority
            return GetConnectedServices().FirstOrDefault();
        }

        private JiraInstanceConfig? GetConfigForService(IJiraService service)
        {
            return _config.Instances.FirstOrDefault(c => c.Name.Equals(service.InstanceName, StringComparison.OrdinalIgnoreCase));
        }

        private int GetServicePriority(IJiraService service)
        {
            var config = GetConfigForService(service);
            return config?.Priority ?? 999;
        }

        private string ExtractProjectKey(string issueKey)
        {
            if (string.IsNullOrEmpty(issueKey)) return "";
            
            var dashIndex = issueKey.IndexOf('-');
            return dashIndex > 0 ? issueKey.Substring(0, dashIndex) : issueKey;
        }

        private bool IsCacheValid()
        {
            return _lastCacheUpdate != DateTime.MinValue && 
                   (DateTime.Now - _lastCacheUpdate).TotalMinutes < _config.CacheExpiryMinutes;
        }

        private MultiJiraConfig LoadConfig()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdinersDailyActivity");
                string configPath = Path.Combine(configDir, "multi_jira_config.json");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<MultiJiraConfig>(json) ?? CreateDefaultConfig();
                }
            }
            catch { }

            return CreateDefaultConfig();
        }

        public void SaveConfig()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdinersDailyActivity");
                Directory.CreateDirectory(configDir);
                string configPath = Path.Combine(configDir, "multi_jira_config.json");

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        private MultiJiraConfig CreateDefaultConfig()
        {
            return new MultiJiraConfig
            {
                Instances = new List<JiraInstanceConfig>
                {
                    new JiraInstanceConfig
                    {
                        Name = "Legacy JIRA",
                        Version = "4.0",
                        BaseUrl = "http://jira-legacy.company.com",
                        AuthType = "Basic",
                        Projects = new List<string> { "LEGACY", "OLD" },
                        Enabled = false,
                        Priority = 3
                    },
                    new JiraInstanceConfig
                    {
                        Name = "Production JIRA",
                        Version = "6.1.7",
                        BaseUrl = "http://jira-prod.company.com",
                        AuthType = "Basic",
                        Projects = new List<string> { "PROD", "MAIN" },
                        Enabled = false,
                        Priority = 2
                    },
                    new JiraInstanceConfig
                    {
                        Name = "Cloud JIRA",
                        Version = "Cloud",
                        BaseUrl = "https://company.atlassian.net",
                        AuthType = "Token",
                        Projects = new List<string> { "CLOUD", "NEW" },
                        Enabled = false,
                        Priority = 1
                    }
                },
                DefaultInstance = "Cloud JIRA",
                AutoDetectInstance = true,
                EnableCrossInstanceSearch = true,
                ConnectionTimeoutSeconds = 30,
                EnableCaching = true,
                CacheExpiryMinutes = 15
            };
        }

        public MultiJiraConfig GetConfig() => _config;

        public void Dispose()
        {
            foreach (var service in _jiraServices)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}