using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Report;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.Report;

/// <summary>
/// Outbound port: xuất bảng điểm ra CSV hoặc Excel (UC-12).
/// </summary>
public interface IReportExportPort
{
    Task<ExportScoreResult> ExportAsync(
        Guid sessionId,
        IReadOnlyList<SubmissionSummaryDto> submissions,
        bool includeCriteriaDetail,
        ExportFormat format,
        CancellationToken cancellationToken = default);
}
