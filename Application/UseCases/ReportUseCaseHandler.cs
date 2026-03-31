using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Report;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Report;
using Ports.OutBoundPorts.Report;

namespace Application.UseCases;

public sealed class ReportUseCaseHandler : IReportUseCase
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IGradingSessionRepository _sessionRepo;
    private readonly IReportExportPort _exportPort;

    public ReportUseCaseHandler(
        ISubmissionRepository submissionRepo,
        IGradingSessionRepository sessionRepo,
        IReportExportPort exportPort)
    {
        _submissionRepo = submissionRepo;
        _sessionRepo = sessionRepo;
        _exportPort = exportPort;
    }

    // UC-12: Export bảng điểm
    public async Task<ExportScoreResult> ExportAsync(ExportScoreCommand command, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(new GradingSessionId(command.SessionId), ct)
            ?? throw new Domain.Exception.DomainException($"Không tìm thấy phiên chấm Id={command.SessionId}.");

        var subs = await _submissionRepo.GetBySessionIdAsync(session.Id, ct);

        var summaries = subs.Select(s => new SubmissionSummaryDto(
            SubmissionId: s.Id.Value,
            SessionId: s.SessionId.Value,
            StudentIdentifier: s.StudentIdentifier,
            Status: s.Status.ToString(),
            TotalScore: s.TotalScore,
            IsPlagiarismSuspected: s.IsPlagiarismSuspected,
            MaxSimilarityPercentage: s.MaxSimilarityPercentage,
            ImportedAt: s.ImportedAt
        )).ToList();

        return await _exportPort.ExportAsync(
            session.Id.Value,
            summaries,
            command.IncludeCriteriaDetail,
            command.Format,
            ct);
    }
}
