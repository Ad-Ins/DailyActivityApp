using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ClockifyTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing Clockify API Key...");
            
            string apiKey = "YzNiZWFkMjctZWE5MC00NzM0LTlhNjgtYjUxYTA1NTBhZDEz";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            
            try
            {
                // Test user authentication
                Console.WriteLine("1. Testing user authentication...");
                var response = await httpClient.GetAsync("https://api.clockify.me/api/v1/user");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("✓ API Key is valid!");
                    Console.WriteLine($"User data: {json.Substring(0, Math.Min(200, json.Length))}...");
                    
                    // Test workspaces
                    Console.WriteLine("\n2. Testing workspaces access...");
                    var workspaceResponse = await httpClient.GetAsync("https://api.clockify.me/api/v1/workspaces");
                    if (workspaceResponse.IsSuccessStatusCode)
                    {
                        var workspaceJson = await workspaceResponse.Content.ReadAsStringAsync();
                        Console.WriteLine("✓ Workspaces accessible!");
                        Console.WriteLine($"Workspaces: {workspaceJson.Substring(0, Math.Min(300, workspaceJson.Length))}...");
                        
                        Console.WriteLine("\n✓ All tests passed! API key is working correctly.");
                    }
                    else
                    {
                        Console.WriteLine("⚠ Could not access workspaces");
                        Console.WriteLine($"Status: {workspaceResponse.StatusCode}");
                    }
                }
                else
                {
                    Console.WriteLine($"✗ API Key invalid. Status: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}