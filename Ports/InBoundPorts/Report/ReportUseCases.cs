using System;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Report;

namespace Ports.InBoundPorts.Report;

public interface IReportUseCase
{
    // UC-12: Xuất bảng điểm ra CSV hoặc Excel
    Task<ExportScoreResult> ExportAsync(ExportScoreCommand command, CancellationToken ct = default);
}
