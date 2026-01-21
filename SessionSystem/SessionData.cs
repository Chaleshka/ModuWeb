using LiteDB;

namespace ModuWeb.SessionSystem
{
    internal class SessionData
    {
        [BsonId]
        public string Id { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
