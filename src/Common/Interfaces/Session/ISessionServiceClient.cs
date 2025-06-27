using Common.Interfaces.Session;
using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// HTTP client interface for interacting with the Session Service
/// </summary>
public interface ISessionServiceClient : ISessionManager
{
    /// <summary>
    /// Set the base URL for the session service
    /// </summary>
    void SetBaseUrl(string baseUrl);
}