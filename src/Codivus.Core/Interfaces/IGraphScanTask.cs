using System.Collections.Generic;
using Codivus.Core.Models;

namespace Codivus.Core.Interfaces
{
    public interface IGraphScanTask : IQueueTask
    {
        string RepositoryId { get; set; }
        string ScanId { get; set; }
        ScanScope Scope { get; set; }
        string TargetPath { get; set; }
        string? ProjectId { get; set; }
        List<string> FileIds { get; set; }
        GraphScanOptions Options { get; set; }
        GraphScanCheckpoint Checkpoint { get; set; }
    }
}