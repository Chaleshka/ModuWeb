using Microsoft.AspNetCore.DataProtection.KeyManagement;
using ModuWeb;
using ModuWeb.Storage;
using System.Text.Json;

namespace ModuWeb.SessionSystem
{
    public class LiteDbSessionService : ISessionService
    {
        private readonly IStorageService _storage;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(30);

        public LiteDbSessionService(IStorageService storage)
        {
            _storage = storage;
        }

        public async Task<T> GetAsync<T>(string sessionId, string key)
        {
            try
            {
                var session = await _storage.GetAsync<SessionData>("sessions", sessionId);
                if (session == null || session.ExpiresAt < DateTime.UtcNow)
                    return default;

                session.LastAccessed = DateTime.UtcNow;
                await _storage.SetAsync("sessions", sessionId, session);

                if (session.Data.TryGetValue(key, out var value))
                {
                    return JsonSerializer.Deserialize<T>(value.ToString());
                }
                return default;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting session data for key {key}.\nException: {ex}", "LiteDbSessionService");
                return default;
            }
        }

        public async Task SetAsync<T>(string sessionId, string key, T value)
        {
            try
            {
                var session = await _storage.GetAsync<SessionData>("sessions", sessionId)
                    ?? new SessionData
                    {
                        Id = sessionId,
                        Data = new Dictionary<string, object>(),
                        CreatedAt = DateTime.UtcNow
                    };

                session.Data[key] = value;
                session.LastAccessed = DateTime.UtcNow;
                session.ExpiresAt = DateTime.UtcNow.Add(_defaultTimeout);

                await _storage.SetAsync("sessions", sessionId, session);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting session data for key {key}.\nException: {ex}", "LiteDbSessionService");
                throw;
            }
        }

        public async Task RemoveAsync(string sessionId, string key)
        {
            try
            {
                var session = await _storage.GetAsync<SessionData>("sessions", sessionId);
                if (session?.Data != null)
                {
                    session.Data.Remove(key);
                    await _storage.SetAsync("sessions", sessionId, session);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing session data for key {key}.\nException: {ex}", "LiteDbSessionService");
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string sessionId)
        {
            try
            {
                var session = await _storage.GetAsync<SessionData>("sessions", sessionId);
                return session != null && session.ExpiresAt > DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking session existence.\nException: {ex}", "LiteDbSessionService");
                return false;
            }
        }

        public async Task RefreshAsync(string sessionId)
        {
            try
            {
                var session = await _storage.GetAsync<SessionData>("sessions", sessionId);
                if (session != null)
                {
                    session.LastAccessed = DateTime.UtcNow;
                    session.ExpiresAt = DateTime.UtcNow.Add(_defaultTimeout);
                    await _storage.SetAsync("sessions", sessionId, session);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error refreshing session.\nException: {ex}", "LiteDbSessionService");
                throw;
            }
        }

        public async Task RemoveSessionAsync(string sessionId)
        {
            try
            {
                await _storage.DeleteAsync<SessionData>("sessions", sessionId);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error removing session.\nException: {ex}", "LiteDbSessionService");
                throw;
            }
        }

        public async Task CleanupExpiredSessions()
        {
            try
            {
                var sessions = await _storage.GetAllAsync<SessionData>("sessions");
                var expired = sessions.Where(s => s.ExpiresAt < DateTime.UtcNow);

                foreach (var session in expired)
                {
                    await _storage.DeleteAsync<SessionData>("sessions", session.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during session cleanup.\nException: {ex}", "LiteDbSessionService");
            }
        }
    }
}

