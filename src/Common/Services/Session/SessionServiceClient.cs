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

    // ---------- helper to map JsonElement → AgentSession -----------------
    private static AgentSession ParseAgentSession(JsonElement sessionData)
    {
        string GetOptionalString(string name) =>
            sessionData.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null
                ? p.GetString() ?? string.Empty
                : string.Empty;

        var statusStr = GetOptionalString("status");

        return new AgentSession
        {
            SessionId             = GetOptionalString("sessionId"),
            Name                  = GetOptionalString("name"),
            CreatedAt             = sessionData.GetProperty("createdAt").GetDateTime(),
            LastUpdatedAt         = sessionData.GetProperty("lastUpdatedAt").GetDateTime(),
            Status                = Enum.Parse<SessionStatus>(string.IsNullOrWhiteSpace(statusStr) ? "Active" : statusStr),
            ConversationState     = GetOptionalString("conversationState"),
            ConfigurationSnapshot = GetOptionalString("configurationSnapshot"),
            Metadata              = GetOptionalString("metadata"),
            TaskStateMarkdown     = GetOptionalString("taskStateMarkdown")
        };
    }
    // ---------------------------------------------------------------------

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
            return ParseAgentSession(sessionData);
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
            return ParseAgentSession(sessionData);
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
            return ParseAgentSession(sessionData);
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
                taskStateMarkdown = session.TaskStateMarkdown          // NEW
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
                foreach (var s in sessionsData)
                    sessions.Add(ParseAgentSession(s));            // use helper
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

    public async Task AddSessionActivityAsync(string sessionId, SessionActivity activity)
    {
        try
        {
            var json = JsonSerializer.Serialize(activity);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/sessions/{sessionId}/activities", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add session activity for {SessionId} via HTTP", sessionId);
            throw;
        }
    }

    public async Task<List<SessionActivity>> GetSessionActivitiesAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/sessions/{sessionId}/activities");
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<SessionActivity>>(responseJson) ?? new List<SessionActivity>();
            return activities;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get session activities for {SessionId} via HTTP", sessionId);
            throw;
        }
    }
}