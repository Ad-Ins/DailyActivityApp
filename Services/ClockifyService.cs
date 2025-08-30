using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdinersDailyActivityApp.Services
{
    public class ClockifyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.clockify.me/api/v1";
        private string _apiKey;
        private string _workspaceId;

        public ClockifyService()
        {
            _httpClient = new HttpClient();
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        public async Task<List<ClockifyWorkspace>> GetWorkspacesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<ClockifyWorkspace>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return new List<ClockifyWorkspace>();
        }

        public async Task<List<ClockifyProject>> GetProjectsAsync(string workspaceId, int pageSize = 200)
        {
            try
            {
                // Get all projects with pagination support
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/projects?page-size={pageSize}&archived=false");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var projects = JsonSerializer.Deserialize<List<ClockifyProject>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    // Sort by name for better UX
                    return projects?.OrderBy(p => p.Name).ToList() ?? new List<ClockifyProject>();
                }
            }
            catch { }
            return new List<ClockifyProject>();
        }
        
        public async Task<List<ClockifyProject>> GetActiveProjectsAsync(string workspaceId)
        {
            // Get only active (non-archived) projects
            return await GetProjectsAsync(workspaceId);
        }
        
        public async Task<List<ClockifyProject>> SearchProjectsAsync(string workspaceId, string searchTerm)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/projects?name={searchTerm}&archived=false");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var projects = JsonSerializer.Deserialize<List<ClockifyProject>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return projects?.OrderBy(p => p.Name).ToList() ?? new List<ClockifyProject>();
                }
            }
            catch { }
            return new List<ClockifyProject>();
        }

        public async Task<ClockifyTask> CreateTaskAsync(string workspaceId, string projectId, string taskName)
        {
            try
            {
                var taskData = new { name = taskName };
                var json = JsonSerializer.Serialize(taskData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/workspaces/{workspaceId}/projects/{projectId}/tasks", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyTask>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        public async Task<ClockifyTimeEntry> StartTimeEntryAsync(string workspaceId, string projectId, string taskId, string description)
        {
            try
            {
                var timeEntryData = new
                {
                    start = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    projectId = projectId,
                    taskId = taskId,
                    description = description
                };
                
                var json = JsonSerializer.Serialize(timeEntryData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/workspaces/{workspaceId}/time-entries", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyTimeEntry>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        public async Task<ClockifyTimeEntry> StopTimeEntryAsync(string workspaceId, string timeEntryId)
        {
            try
            {
                var stopData = new { end = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") };
                var json = JsonSerializer.Serialize(stopData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{_baseUrl}/workspaces/{workspaceId}/time-entries/{timeEntryId}", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyTimeEntry>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }
        
        public async Task<ClockifyTimeEntry> CreateHistoryTimeEntryAsync(string workspaceId, string projectId, string taskId, string description, DateTime start, DateTime end)
        {
            try
            {
                var timeEntryData = new
                {
                    start = start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    end = end.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    projectId = projectId,
                    taskId = taskId,
                    description = description
                };
                
                var json = JsonSerializer.Serialize(timeEntryData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_baseUrl}/workspaces/{workspaceId}/time-entries", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyTimeEntry>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }
        
        public async Task<ClockifyTimeEntry> CreateTimeEntryAsync(string workspaceId, string projectId, string taskId, string description, DateTime start, DateTime end)
        {
            return await CreateHistoryTimeEntryAsync(workspaceId, projectId, taskId, description, start, end);
        }
        
        public async Task<ClockifyTimeEntry> GetCurrentTimeEntryAsync(string workspaceId, string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/user/{userId}/time-entries?in-progress=true");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var entries = JsonSerializer.Deserialize<List<ClockifyTimeEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return entries?.FirstOrDefault();
                }
            }
            catch { }
            return null;
        }
        
        public async Task<ClockifyUser> GetCurrentUserAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/user");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyUser>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }
        
        public async Task<bool> DeleteTimeEntryAsync(string workspaceId, string timeEntryId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/workspaces/{workspaceId}/time-entries/{timeEntryId}");
                return response.IsSuccessStatusCode;
            }
            catch { }
            return false;
        }
        
        public async Task<List<ClockifyTask>> GetTasksAsync(string workspaceId, string projectId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/projects/{projectId}/tasks?is-active=true");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tasks = JsonSerializer.Deserialize<List<ClockifyTask>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return tasks?.Where(t => t.Status == "ACTIVE").OrderBy(t => t.Name).ToList() ?? new List<ClockifyTask>();
                }
            }
            catch { }
            return new List<ClockifyTask>();
        }
        
        public async Task<ClockifyDashboardData> GetDashboardDataAsync(string workspaceId, string userId)
        {
            try
            {
                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                
                var todayStart = today.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var todayEnd = today.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
                var weekStartStr = weekStart.ToString("yyyy-MM-ddTHH:mm:ssZ");
                
                var response = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/user/{userId}/time-entries?start={todayStart}&end={todayEnd}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var todayEntries = JsonSerializer.Deserialize<List<ClockifyTimeEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ClockifyTimeEntry>();
                    
                    var weekResponse = await _httpClient.GetAsync($"{_baseUrl}/workspaces/{workspaceId}/user/{userId}/time-entries?start={weekStartStr}&end={todayEnd}");
                    var weekEntries = new List<ClockifyTimeEntry>();
                    if (weekResponse.IsSuccessStatusCode)
                    {
                        var weekJson = await weekResponse.Content.ReadAsStringAsync();
                        weekEntries = JsonSerializer.Deserialize<List<ClockifyTimeEntry>>(weekJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ClockifyTimeEntry>();
                    }
                    
                    return new ClockifyDashboardData
                    {
                        TodayEntries = todayEntries,
                        WeekEntries = weekEntries
                    };
                }
            }
            catch { }
            return new ClockifyDashboardData();
        }
        
        public async Task<ClockifyTimeEntry> UpdateTimeEntryAsync(string workspaceId, string timeEntryId, string projectId, string taskId, string description, DateTime start, DateTime end)
        {
            try
            {
                var updateData = new
                {
                    start = start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    end = end.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    projectId = projectId,
                    taskId = taskId,
                    description = description
                };
                
                var json = JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"{_baseUrl}/workspaces/{workspaceId}/time-entries/{timeEntryId}", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ClockifyTimeEntry>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }
    }

    public class ClockifyWorkspace
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class ClockifyProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
    }

    public class ClockifyTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProjectId { get; set; }
        public string Status { get; set; }
    }

    public class ClockifyTimeEntry
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public ClockifyTimeInterval TimeInterval { get; set; }
        public string ProjectId { get; set; }
        public string TaskId { get; set; }
        public string WorkspaceId { get; set; }
    }
    
    public class ClockifyTimeInterval
    {
        public string Start { get; set; }
        public string End { get; set; }
    }
    
    public class ClockifyUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
    
    public class ClockifyDashboardData
    {
        public List<ClockifyTimeEntry> TodayEntries { get; set; } = new();
        public List<ClockifyTimeEntry> WeekEntries { get; set; } = new();
    }
}