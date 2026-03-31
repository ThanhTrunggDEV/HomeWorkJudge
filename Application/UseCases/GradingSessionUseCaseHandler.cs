using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.GradingSession;
using Ports.DTO.Submission;
using Ports.InBoundPorts.GradingSession;
using Ports.OutBoundPorts.Storage;

namespace Application.UseCases;

public sealed class GradingSessionUseCaseHandler : IGradingSessionUseCase
{
    private readonly IGradingSessionRepository _sessionRepo;
    private readonly IRubricRepository _rubricRepo;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IFileExtractorPort _fileExtractor;
    private readonly IUnitOfWork _uow;

    public GradingSessionUseCaseHandler(
        IGradingSessionRepository sessionRepo,
        IRubricRepository rubricRepo,
        ISubmissionRepository submissionRepo,
        IFileExtractorPort fileExtractor,
        IUnitOfWork uow)
    {
        _sessionRepo = sessionRepo;
        _rubricRepo = rubricRepo;
        _submissionRepo = submissionRepo;
        _fileExtractor = fileExtractor;
        _uow = uow;
    }

    // UC-05: Tạo phiên chấm + import bài nộp từ danh sách file zip/rar
    public async Task<CreateSessionResult> CreateAsync(CreateSessionCommand command, CancellationToken ct = default)
    {
        // Validate rubric tồn tại
        var rubric = await _rubricRepo.GetByIdAsync(new RubricId(command.RubricId), ct)
            ?? throw new DomainException($"Không tìm thấy Rubric Id={command.RubricId}.");

        var session = new GradingSession(
            new GradingSessionId(Guid.NewGuid()),
            command.Name,
            rubric.Id);

        await _sessionRepo.AddAsync(session, ct);

        // Giải nén từng file → tạo Submission
        int imported = 0, skipped = 0;
        var submissions = new List<Submission>();

        foreach (var filePath in command.FilePaths)
        {
            try
            {
                var sourceFiles = await _fileExtractor.ExtractAsync(filePath, ct);

                // StudentIdentifier = tên file (bỏ extension)
                var studentId = System.IO.Path.GetFileNameWithoutExtension(filePath);

                var submission = new Submission(
                    new SubmissionId(Guid.NewGuid()),
                    session.Id,
                    studentId,
                    sourceFiles.Select(f => new SourceFile(f.FileName, f.Content)).ToList());

                submissions.Add(submission);
                imported++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // File không giải nén được hoặc không có source code hợp lệ
                skipped++;
            }
        }

        if (submissions.Count > 0)
            await _submissionRepo.AddRangeAsync(submissions, ct);

        await _uow.SaveChangesAsync(ct);

        return new CreateSessionResult(session.Id.Value, imported, skipped);
    }

    // UC-06: Xem danh sách phiên chấm
    public async Task<IReadOnlyList<GradingSessionSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var sessions = await _sessionRepo.GetAllAsync(ct);
        var result = new List<GradingSessionSummaryDto>();

        foreach (var s in sessions)
        {
            var allSubs = await _submissionRepo.GetBySessionIdAsync(s.Id, ct);
            var rubric = await _rubricRepo.GetByIdAsync(s.RubricId, ct);

            result.Add(new GradingSessionSummaryDto(
                SessionId: s.Id.Value,
                Name: s.Name,
                RubricId: s.RubricId.Value,
                RubricName: rubric?.Name ?? "(deleted)",
                TotalSubmissions: allSubs.Count,
                ReviewedCount: allSubs.Count(x => x.Status == SubmissionStatus.Reviewed),
                ErrorCount: allSubs.Count(x => x.Status == SubmissionStatus.Error),
                CreatedAt: s.CreatedAt));
        }

        return result;
    }


    // UC-11: Thống kê phiên chấm
    public async Task<SessionStatisticsDto> GetStatisticsAsync(Guid sessionId, CancellationToken ct = default)
    {
        var subs = await _submissionRepo.GetBySessionIdAsync(new GradingSessionId(sessionId), ct);

        var scores = subs
            .Where(s => s.Status == SubmissionStatus.AIGraded || s.Status == SubmissionStatus.Reviewed)
            .Select(s => s.TotalScore)
            .ToList();

        return new SessionStatisticsDto(
            TotalCount: subs.Count,
            PendingCount: subs.Count(s => s.Status == SubmissionStatus.Pending),
            GradingCount: subs.Count(s => s.Status == SubmissionStatus.Grading),
            AIGradedCount: subs.Count(s => s.Status == SubmissionStatus.AIGraded),
            ReviewedCount: subs.Count(s => s.Status == SubmissionStatus.Reviewed),
            ErrorCount: subs.Count(s => s.Status == SubmissionStatus.Error),
            AverageScore: scores.Count > 0 ? scores.Average() : null,
            MinScore: scores.Count > 0 ? scores.Min() : null,
            MaxScore: scores.Count > 0 ? scores.Max() : null);
    }

    // Xoá phiên chấm (cascade xoá submissions)
    public async Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        await _sessionRepo.DeleteAsync(new GradingSessionId(sessionId), ct);
        await _uow.SaveChangesAsync(ct);
    }
}
