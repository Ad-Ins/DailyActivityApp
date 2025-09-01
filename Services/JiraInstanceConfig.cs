using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AdinersDailyActivityApp.Services
{
    public class JiraInstanceConfig
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string AuthType { get; set; } = "Basic"; // Basic, OAuth, Token
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string ApiToken { get; set; } = "";
        public string OAuthToken { get; set; } = "";
        public List<string> Projects { get; set; } = new();
        public bool Enabled { get; set; } = true;
        public int Priority { get; set; } = 1; // Lower number = higher priority
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        
        [JsonIgnore]
        public bool IsLegacy => Version.StartsWith("4.") || Version.StartsWith("3.");
        
        [JsonIgnore]
        public bool IsModern => Version.StartsWith("6.") || Version.StartsWith("7.") || Version.StartsWith("8.");
        
        [JsonIgnore]
        public bool IsCloud => Version.Equals("Cloud", System.StringComparison.OrdinalIgnoreCase);
    }

    public class MultiJiraConfig
    {
        public List<JiraInstanceConfig> Instances { get; set; } = new();
        public string DefaultInstance { get; set; } = "";
        public bool AutoDetectInstance { get; set; } = true;
        public bool EnableCrossInstanceSearch { get; set; } = true;
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public bool EnableCaching { get; set; } = true;
        public int CacheExpiryMinutes { get; set; } = 15;
    }
}