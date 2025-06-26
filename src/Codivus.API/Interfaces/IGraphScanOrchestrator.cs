using System.Threading;
using System.Threading.Tasks;
using Codivus.API.Services;
using Codivus.Core.Models;

namespace Codivus.API.Interfaces
{
    public interface IGraphScanOrchestrator
    {
        Task<string> StartGraphScanAsync(string repositoryId, GraphScanConfiguration configuration, CancellationToken cancellationToken = default);
        Task<GraphScanProgress?> GetScanProgressAsync(string scanId);
        Task<bool> PauseScanAsync(string scanId, CancellationToken cancellationToken = default);
        Task<bool> ResumeScanAsync(string scanId, CancellationToken cancellationToken = default);
        Task<bool> CancelScanAsync(string scanId, CancellationToken cancellationToken = default);
    }
}