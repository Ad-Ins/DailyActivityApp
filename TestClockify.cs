using System;
using System.Linq;
using System.Threading.Tasks;
using AdinersDailyActivityApp.Services;

namespace AdinersDailyActivityApp
{
    public class TestClockify
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Testing Clockify API...");
            
            string apiKey = "YzNiZWFkMjctZWE5MC00NzM0LTlhNjgtYjUxYTA1NTBhZDEz";
            
            var clockifyService = new ClockifyService();
            clockifyService.SetApiKey(apiKey);
            
            try
            {
                // Test 1: Get current user
                Console.WriteLine("1. Testing user authentication...");
                var user = await clockifyService.GetCurrentUserAsync();
                if (user != null)
                {
                    Console.WriteLine($"✓ User authenticated: {user.Name} ({user.Email})");
                }
                else
                {
                    Console.WriteLine("✗ Failed to authenticate user");
                    return;
                }
                
                // Test 2: Get workspaces
                Console.WriteLine("\n2. Testing workspaces...");
                var workspaces = await clockifyService.GetWorkspacesAsync();
                if (workspaces.Count > 0)
                {
                    Console.WriteLine($"✓ Found {workspaces.Count} workspace(s):");
                    foreach (var workspace in workspaces)
                    {
                        Console.WriteLine($"   - {workspace.Name} (ID: {workspace.Id})");
                    }
                    
                    // Test 3: Get projects for first workspace
                    string workspaceId = workspaces[0].Id;
                    Console.WriteLine($"\n3. Testing projects in workspace '{workspaces[0].Name}'...");
                    var projects = await clockifyService.GetProjectsAsync(workspaceId);
                    if (projects.Count > 0)
                    {
                        Console.WriteLine($"✓ Found {projects.Count} project(s):");
                        foreach (var project in projects.Take(5)) // Show first 5
                        {
                            Console.WriteLine($"   - {project.Name} (ID: {project.Id})");
                        }
                        
                        // Test 4: Get tasks for first project
                        if (projects.Count > 0)
                        {
                            string projectId = projects[0].Id;
                            Console.WriteLine($"\n4. Testing tasks in project '{projects[0].Name}'...");
                            var tasks = await clockifyService.GetTasksAsync(workspaceId, projectId);
                            Console.WriteLine($"✓ Found {tasks.Count} task(s):");
                            foreach (var task in tasks.Take(5)) // Show first 5
                            {
                                Console.WriteLine($"   - {task.Name} (ID: {task.Id})");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠ No projects found in workspace");
                    }
                }
                else
                {
                    Console.WriteLine("✗ No workspaces found");
                }
                
                Console.WriteLine("\n✓ All tests completed successfully!");
                Console.WriteLine("The Clockify API key is working correctly.");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error testing Clockify API: {ex.Message}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}