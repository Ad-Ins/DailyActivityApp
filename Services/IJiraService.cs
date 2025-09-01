using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdinersDailyActivityApp.Services
{
    public interface IJiraService
    {
        string InstanceName { get; }
        string Version { get; }
        bool IsConnected { get; }
        
        Task<bool> TestConnectionAsync();
        Task<JiraIssue?> GetIssueAsync(string issueKey);
        Task<List<JiraIssue>> GetMyIssuesAsync();
        Task<bool> LogWorkAsync(string issueKey, TimeSpan duration, string comment, DateTime? startTime = null);
        Task<List<JiraProject>> GetProjectsAsync();
        Task<List<JiraIssue>> SearchIssuesAsync(string jql);
    }

    public class JiraIssue
    {
        public string Key { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public string Assignee { get; set; } = "";
        public string Reporter { get; set; } = "";
        public string ProjectKey { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string IssueType { get; set; } = "";
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Url { get; set; } = "";
        public Dictionary<string, object> CustomFields { get; set; } = new();
    }

    public class JiraProject
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Lead { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class JiraWorklog
    {
        public string Id { get; set; } = "";
        public string IssueKey { get; set; } = "";
        public TimeSpan TimeSpent { get; set; }
        public string Comment { get; set; } = "";
        public DateTime Started { get; set; }
        public string Author { get; set; } = "";
    }
}