namespace ModuWeb.Storage
{
    public interface IStorageService
    {
        Task<T> GetAsync<T>(string collection, string id);
        Task<IEnumerable<T>> GetAllAsync<T>(string collection);
        Task SetAsync<T>(string collection, string id, T value);
        Task DeleteAsync<T>(string collection, string id);
        Task<bool> ExistsAsync<T>(string collection, string id);
    }
}
