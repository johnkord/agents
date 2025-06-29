using System.Text.Json;
using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace Common.Services.Session;

/// <summary>
/// HTTP client for interacting with the Session Service
/// </summary>
public class SessionServiceClient : ISessionServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SessionServiceClient>? _logger;
    private string _baseUrl = "http://localhost:5001";

    public SessionServiceClient(HttpClient httpClient, ILogger<SessionServiceClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void SetBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(_baseUrl);
    }

    public async Task<AgentSession> CreateSessionAsync(string name = "")
    {
        try
        {
            var request = new { name = name };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/sessions", content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            return new AgentSession
            {
                SessionId = sessionData.GetProperty("sessionId").GetString() ?? string.Empty,
                Name = sessionData.GetProperty("name").GetString() ?? string.Empty,
                CreatedAt = sessionData.GetProperty("createdAt").GetDateTime(),
                LastUpdatedAt = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
                Status = Enum.Parse<SessionStatus>(sessionData.GetProperty("status").GetString() ?? "Active")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create session via HTTP");
            throw;
        }
    }

    public async Task<AgentSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/{sessionId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
                
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            return new AgentSession
            {
                SessionId = sessionData.GetProperty("sessionId").GetString() ?? string.Empty,
                Name = sessionData.GetProperty("name").GetString() ?? string.Empty,
                CreatedAt = sessionData.GetProperty("createdAt").GetDateTime(),
                LastUpdatedAt = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
                Status = Enum.Parse<SessionStatus>(sessionData.GetProperty("status").GetString() ?? "Active"),
                ConversationState = sessionData.GetProperty("conversationState").GetString() ?? string.Empty,
                ConfigurationSnapshot = sessionData.GetProperty("configurationSnapshot").GetString() ?? string.Empty,
                Metadata = sessionData.GetProperty("metadata").GetString() ?? string.Empty,
                CurrentPlan = sessionData.GetProperty("currentPlan").GetString() ?? string.Empty,
                ActivityLog = JsonSerializer.Serialize(sessionData.GetProperty("activities"))
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get session {SessionId} via HTTP", sessionId);
            throw;
        }
    }

    public async Task<AgentSession?> GetSessionByNameAsync(string name)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-name/{Uri.EscapeDataString(name)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
                
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionData = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            return new AgentSession
            {
                SessionId = sessionData.GetProperty("sessionId").GetString() ?? string.Empty,
                Name = sessionData.GetProperty("name").GetString() ?? string.Empty,
                CreatedAt = sessionData.GetProperty("createdAt").GetDateTime(),
                LastUpdatedAt = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
                Status = Enum.Parse<SessionStatus>(sessionData.GetProperty("status").GetString() ?? "Active"),
                ConversationState = sessionData.GetProperty("conversationState").GetString() ?? string.Empty,
                ConfigurationSnapshot = sessionData.GetProperty("configurationSnapshot").GetString() ?? string.Empty,
                Metadata = sessionData.GetProperty("metadata").GetString() ?? string.Empty,
                CurrentPlan = sessionData.GetProperty("currentPlan").GetString() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get session by name {Name} via HTTP", name);
            throw;
        }
    }

    public async Task SaveSessionAsync(AgentSession session)
    {
        try
        {
            var request = new
            {
                name = session.Name,
                conversationMessages = session.GetConversationMessages(),
                configurationSnapshot = session.ConfigurationSnapshot,
                metadata = session.Metadata,
                status = session.Status,
                currentPlan = session.GetCurrentPlan(),
                activities = session.GetActivityLog()
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"{_baseUrl}/api/sessions/{session.SessionId}", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save session {SessionId} via HTTP", session.SessionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> ListSessionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            var sessions = new List<AgentSession>();
            if (sessionsData != null)
            {
                foreach (var sessionData in sessionsData)
                {
                    sessions.Add(new AgentSession
                    {
                        SessionId = sessionData.GetProperty("sessionId").GetString() ?? string.Empty,
                        Name = sessionData.GetProperty("name").GetString() ?? string.Empty,
                        CreatedAt = sessionData.GetProperty("createdAt").GetDateTime(),
                        LastUpdatedAt = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
                        Status = Enum.Parse<SessionStatus>(sessionData.GetProperty("status").GetString() ?? "Active")
                    });
                }
            }
            
            return sessions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list sessions via HTTP");
            throw;
        }
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/sessions/{sessionId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;
                
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete session {SessionId} via HTTP", sessionId);
            throw;
        }
    }

    public async Task<bool> ArchiveSessionAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/sessions/{sessionId}/archive", null);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;
                
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to archive session {SessionId} via HTTP", sessionId);
            throw;
        }
    }
    
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByTaskStatusAsync(TaskExecutionStatus taskStatus)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-task-status/{taskStatus}");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sessions by task status {TaskStatus} via HTTP", taskStatus);
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetSessionsByCategoryAsync(string category)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-category/{Uri.EscapeDataString(category)}");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sessions by category {Category} via HTTP", category);
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetSessionsByPriorityAsync(int priority)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-priority/{priority}");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sessions by priority {Priority} via HTTP", priority);
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetSessionsByProgressRangeAsync(double minProgress, double maxProgress)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-progress?minProgress={minProgress}&maxProgress={maxProgress}");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sessions by progress range via HTTP");
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetActiveTasksAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/active");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get active tasks via HTTP");
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetCompletedTasksAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/completed");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get completed tasks via HTTP");
            throw;
        }
    }

    public async Task<IReadOnlyList<AgentSession>> GetSessionsByTagsAsync(params string[] tags)
    {
        try
        {
            var tagsQuery = string.Join(",", tags);
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/by-tags?tags={Uri.EscapeDataString(tagsQuery)}");
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            var sessionsData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);
            
            return ParseSessionsList(sessionsData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get sessions by tags via HTTP");
            throw;
        }
    }
    
    private List<AgentSession> ParseSessionsList(JsonElement[]? sessionsData)
    {
        var sessions = new List<AgentSession>();
        if (sessionsData != null)
        {
            foreach (var sessionData in sessionsData)
            {
                var session = new AgentSession
                {
                    SessionId = sessionData.GetProperty("sessionId").GetString() ?? string.Empty,
                    Name = sessionData.GetProperty("name").GetString() ?? string.Empty,
                    CreatedAt = sessionData.GetProperty("createdAt").GetDateTime(),
                    LastUpdatedAt = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
                    Status = Enum.Parse<SessionStatus>(sessionData.GetProperty("status").GetString() ?? "Active")
                };
                
                // Parse task-related fields if available
                if (sessionData.TryGetProperty("taskTitle", out var taskTitle))
                    session.TaskTitle = taskTitle.GetString() ?? string.Empty;
                
                if (sessionData.TryGetProperty("taskStatus", out var taskStatus))
                    session.TaskStatus = Enum.Parse<TaskExecutionStatus>(taskStatus.GetString() ?? "NotStarted");
                
                if (sessionData.TryGetProperty("currentStep", out var currentStep))
                    session.CurrentStep = currentStep.GetInt32();
                
                if (sessionData.TryGetProperty("totalSteps", out var totalSteps))
                    session.TotalSteps = totalSteps.GetInt32();
                
                if (sessionData.TryGetProperty("completedSteps", out var completedSteps))
                    session.CompletedSteps = completedSteps.GetInt32();
                
                if (sessionData.TryGetProperty("progressPercentage", out var progressPercentage))
                    session.ProgressPercentage = progressPercentage.GetDouble();
                
                sessions.Add(session);
            }
        }
        
        return sessions;
    }
}