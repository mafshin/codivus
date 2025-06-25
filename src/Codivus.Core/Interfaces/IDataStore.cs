using System.Threading;
using System.Threading.Tasks;

namespace Codivus.Core.Interfaces
{
    public interface IDataStore
    {
        Task<bool> SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    }
}