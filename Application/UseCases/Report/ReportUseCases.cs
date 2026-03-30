using System;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Report;
using Ports.InBoundPorts.Query;
using Ports.InBoundPorts.Report;
using Ports.OutBoundPorts.Report;

namespace Application.UseCases.Report;

public sealed class ExportScoreReportUseCase : IExportScoreReportUseCase
{
    private readonly IGetScoreboardUseCase _getScoreboardUseCase;
    private readonly IReportExportPort _reportExportPort;

    public ExportScoreReportUseCase(
        IGetScoreboardUseCase getScoreboardUseCase,
        IReportExportPort reportExportPort)
    {
        _getScoreboardUseCase = getScoreboardUseCase;
        _reportExportPort = reportExportPort;
    }

    public async Task<ExportScoreReportResponseDto> HandleAsync(
        Guid classroomId,
        string format,
        CancellationToken cancellationToken = default)
    {
        var scoreboard = await _getScoreboardUseCase.HandleAsync(classroomId, cancellationToken);
        return await _reportExportPort.ExportScoreboardAsync(scoreboard, format, cancellationToken);
    }
}
