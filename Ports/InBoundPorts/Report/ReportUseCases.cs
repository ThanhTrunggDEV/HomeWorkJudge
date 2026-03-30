using System;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Report;

namespace Ports.InBoundPorts.Report;

public interface IExportScoreReportUseCase
{
    Task<ExportScoreReportResponseDto> HandleAsync(
        Guid classroomId,
    Guid? assignmentId,
        string format,
        CancellationToken cancellationToken = default);
}
