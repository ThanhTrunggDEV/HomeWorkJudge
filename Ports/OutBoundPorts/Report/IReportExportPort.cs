using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Report;

namespace Ports.OutBoundPorts.Report;

public interface IReportExportPort
{
    Task<ExportScoreReportResponseDto> ExportScoreboardAsync(
        IReadOnlyList<ScoreboardItemDto> items,
        string format,
        CancellationToken cancellationToken = default);
}
