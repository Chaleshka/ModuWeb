using LiteDB;

namespace ModuWeb.Storage
{
    internal class LiteDbStorageService : IStorageService
    {
        private readonly ILiteDatabase _db;

        public LiteDbStorageService(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _db = new LiteDatabase(path);
        }

        public async Task<T> GetAsync<T>(string collection, string id)
        {
            try
            {
                var col = _db.GetCollection<T>(collection);
                return await Task.FromResult(col.FindById(id));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting item {id} from collection {collection}.\nException: {ex}", "LiteDbStorageService");
                return default;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(string collection)
        {
            try
            {
                var col = _db.GetCollection<T>(collection);
                return await Task.FromResult(col.FindAll());
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting all items from collection {collection}.\nException: {ex}", "LiteDbStorageService");
                return Enumerable.Empty<T>();
            }
        }

        public async Task SetAsync<T>(string collection, string id, T value)
        {
            try
            {
                var col = _db.GetCollection<T>(collection);
                col.Upsert(id, value);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting item {id} in collection {collection}.\nException: {ex}", "LiteDbStorageService");
                throw;
            }
        }

        public async Task DeleteAsync<T>(string collection, string id)
        {
            try
            {
                var col = _db.GetCollection<T>(collection);
                col.Delete(id);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting item {id} from collection {collection}.\nException: {ex}", "LiteDbStorageService");
                throw;
            }
        }

        public async Task<bool> ExistsAsync<T>(string collection, string id)
        {
            try
            {
                var col = _db.GetCollection<T>(collection);
                return await Task.FromResult(col.Exists(id));
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking existence of item {id} in collection {collection}.\nException: {ex}", "LiteDbStorageService");
                return false;
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
