namespace ModuWeb.SessionSystem
{
    internal interface ISessionService
    {
        Task<T> GetAsync<T>(string sessionId, string key);
        Task SetAsync<T>(string sessionId, string key, T value);
        Task RemoveAsync(string sessionId, string key);
        Task<bool> ExistsAsync(string sessionId);
        Task RefreshAsync(string sessionId);
        Task RemoveSessionAsync(string sessionId);
        Task CleanupExpiredSessions();
    }
}
