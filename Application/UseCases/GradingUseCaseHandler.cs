using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DomainEvents;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Grading;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Build;
using Ports.OutBoundPorts.Plagiarism;

namespace Application.UseCases;

public sealed class GradingUseCaseHandler : IGradingUseCase
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IGradingSessionRepository _sessionRepo;
    private readonly IRubricRepository _rubricRepo;
    private readonly IAiGradingPort _aiGrading;
    private readonly ICSharpBuildPort _csharpBuild;
    private readonly IPlagiarismDetectionPort _plagiarismPort;
    private readonly IUnitOfWork _uow;
    private readonly IDomainEventDispatcher _dispatcher;

    public GradingUseCaseHandler(
        ISubmissionRepository submissionRepo,
        IGradingSessionRepository sessionRepo,
        IRubricRepository rubricRepo,
        IAiGradingPort aiGrading,
        ICSharpBuildPort csharpBuild,
        IPlagiarismDetectionPort plagiarismPort,
        IUnitOfWork uow,
        IDomainEventDispatcher dispatcher)
    {
        _submissionRepo = submissionRepo;
        _sessionRepo = sessionRepo;
        _rubricRepo = rubricRepo;
        _aiGrading = aiGrading;
        _csharpBuild = csharpBuild;
        _plagiarismPort = plagiarismPort;
        _uow = uow;
        _dispatcher = dispatcher;
    }

    // UC-07: Kích hoạt AI chấm tất cả bài Pending trong phiên
    public async Task<StartGradingResult> StartGradingAsync(StartGradingCommand command, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(new GradingSessionId(command.SessionId), ct)
            ?? throw new DomainException($"Không tìm thấy phiên chấm Id={command.SessionId}.");

        var rubric = await _rubricRepo.GetByIdAsync(session.RubricId, ct)
            ?? throw new DomainException("Không tìm thấy Rubric của phiên chấm.");

        var pendingSubmissions = await _submissionRepo.GetByStatusAsync(session.Id, SubmissionStatus.Pending, ct);
        if (pendingSubmissions.Count == 0)
            return new StartGradingResult(0);

        var criteriaDto = rubric.Criteria.Select(c => new Ports.DTO.Rubric.RubricCriteriaDto(c.Id.Value, c.Name, c.MaxScore, c.Description)).ToList();

        int started = 0;
        foreach (var submission in pendingSubmissions)
        {
            await GradeOneAsync(submission, criteriaDto, ct);
            started++;
        }

        return new StartGradingResult(started);
    }

    // UC-08: Chấm lại 1 bài
    public async Task RegradeSubmissionAsync(RegradeSubmissionCommand command, CancellationToken ct = default)
    {
        var submission = await _submissionRepo.GetByIdAsync(new SubmissionId(command.SubmissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={command.SubmissionId}.");

        var session = await _sessionRepo.GetByIdAsync(submission.SessionId, ct)
            ?? throw new DomainException($"Không tìm thấy phiên chấm Id={submission.SessionId.Value}.");
        var rubric = await _rubricRepo.GetByIdAsync(session.RubricId, ct)
            ?? throw new DomainException("Không tìm thấy Rubric của phiên chấm.");

        var criteriaDto = rubric.Criteria.Select(c => new Ports.DTO.Rubric.RubricCriteriaDto(c.Id.Value, c.Name, c.MaxScore, c.Description)).ToList();
        await GradeOneAsync(submission, criteriaDto, ct);
    }

    // UC-08: Chấm lại toàn bộ phiên (kể cả đã chấm, reset về Pending rồi chấm lại)
    public async Task RegradeSessionAsync(RegradeSessionCommand command, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(new GradingSessionId(command.SessionId), ct)
            ?? throw new DomainException($"Không tìm thấy phiên chấm Id={command.SessionId}.");

        var rubric = await _rubricRepo.GetByIdAsync(session.RubricId, ct)
            ?? throw new DomainException("Không tìm thấy Rubric.");

        var allSubs = await _submissionRepo.GetBySessionIdAsync(session.Id, ct);
        var criteriaDto = rubric.Criteria.Select(c => new Ports.DTO.Rubric.RubricCriteriaDto(c.Id.Value, c.Name, c.MaxScore, c.Description)).ToList();

        foreach (var submission in allSubs)
        {
            submission.ResetForRegrade();
            await GradeOneAsync(submission, criteriaDto, ct);
        }
    }

    // UC-09: Xem chi tiết 1 bài nộp
    public async Task<SubmissionDetailDto> GetSubmissionDetailAsync(Guid submissionId, CancellationToken ct = default)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(submissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={submissionId}.");

        return ToDetailDto(s);
    }

    // UC-09: Xem danh sách bài nộp trong phiên
    public async Task<IReadOnlyList<SubmissionSummaryDto>> GetSubmissionsBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var subs = await _submissionRepo.GetBySessionIdAsync(new GradingSessionId(sessionId), ct);
        return subs.Select(ToSummaryDto).ToList();
    }

    // UC-10: GV duyệt bài
    public async Task ApproveAsync(ApproveSubmissionCommand command, CancellationToken ct = default)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(command.SubmissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={command.SubmissionId}.");

        s.Approve();
        await _submissionRepo.UpdateAsync(s, ct);

        var events = s.DomainEvents.ToList();
        await _uow.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(events, ct);
        s.ClearDomainEvents();
    }

    // UC-10: GV override điểm 1 tiêu chí
    public async Task OverrideCriteriaScoreAsync(OverrideCriteriaScoreCommand command, CancellationToken ct = default)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(command.SubmissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={command.SubmissionId}.");

        s.OverrideCriteriaScore(command.CriteriaName, command.NewScore, command.Comment);
        await _submissionRepo.UpdateAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-10: GV override tổng điểm
    public async Task OverrideTotalScoreAsync(OverrideTotalScoreCommand command, CancellationToken ct = default)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(command.SubmissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={command.SubmissionId}.");

        s.OverrideTotalScore(command.NewScore);
        await _submissionRepo.UpdateAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-10: GV thêm ghi chú
    public async Task AddTeacherNoteAsync(AddTeacherNoteCommand command, CancellationToken ct = default)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(command.SubmissionId), ct)
            ?? throw new DomainException($"Không tìm thấy Submission Id={command.SubmissionId}.");

        s.AddTeacherNote(command.Note);
        await _submissionRepo.UpdateAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // Kiểm tra đạo văn
    public async Task<CheckPlagiarismResult> CheckPlagiarismAsync(CheckPlagiarismCommand command, CancellationToken ct = default)
    {
        var subs = await _submissionRepo.GetBySessionIdAsync(new GradingSessionId(command.SessionId), ct);

        var inputs = subs.Select(s => new Ports.DTO.Submission.SubmissionFilesDto(
            s.Id.Value,
            s.StudentIdentifier,
            s.SourceFiles.Select(f => new Ports.DTO.Submission.SourceFileDto(f.FileName, f.Content)).ToList()
        )).ToList();

        var results = await _plagiarismPort.DetectAsync(inputs, ct);

        var suspected = results.Where(r => r.SimilarityPercentage >= command.ThresholdPercentage).ToList();

        // Cập nhật flag đạo văn trên từng submission
        foreach (var pair in suspected)
        {
            await FlagPlagiarismAsync(pair.SubmissionIdA, pair.SimilarityPercentage, ct);
            await FlagPlagiarismAsync(pair.SubmissionIdB, pair.SimilarityPercentage, ct);
        }

        if (suspected.Count > 0)
            await _uow.SaveChangesAsync(ct);

        return new CheckPlagiarismResult(suspected.Select(r => new PlagiarismResultDto(
            r.SubmissionIdA, r.SubmissionIdB,
            r.StudentIdentifierA, r.StudentIdentifierB,
            r.SimilarityPercentage)).ToList());
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task GradeOneAsync(
        Submission submission,
        IReadOnlyList<Ports.DTO.Rubric.RubricCriteriaDto> criteriaDto,
        CancellationToken ct)
    {
        submission.StartGrading();

        try
        {
            var sourceFileDtos = submission.SourceFiles
                .Select(f => new Ports.DTO.Submission.SourceFileDto(f.FileName, f.Content))
                .ToList();

            // ── BƯỚC 1: Build C# solution ─────────────────────────────────────
            var buildResult = await _csharpBuild.BuildAsync(
                sourceFileDtos,
                submission.Id.Value.ToString(),
                ct);

            if (!buildResult.Success)
            {
                // Build thất bại → 0 điểm, không gọi AI
                submission.MarkBuildFailed(buildResult.BuildLog);
            }
            else
            {
                // ── BƯỚC 2: AI chấm bài ──────────────────────────────────────
                var scores = await _aiGrading.GradeAsync(sourceFileDtos, criteriaDto, ct);

                var rubricResults = scores
                    .Select(s => new RubricResult(s.CriteriaName, s.GivenScore, s.MaxScore, s.Comment))
                    .ToList();

                submission.AttachAIResults(rubricResults);
            }
        }
        catch (Exception ex)
        {
            submission.MarkError(ex.Message);
        }

        var events = submission.DomainEvents.ToList();
        submission.ClearDomainEvents();

        await _submissionRepo.UpdateAsync(submission, ct);
        await _uow.SaveChangesAsync(ct);
        await _dispatcher.DispatchAsync(events, ct);
    }

    private async Task FlagPlagiarismAsync(Guid submissionId, double similarity, CancellationToken ct)
    {
        var s = await _submissionRepo.GetByIdAsync(new SubmissionId(submissionId), ct);
        if (s is null) return;

        if (s.MaxSimilarityPercentage is null || similarity > s.MaxSimilarityPercentage)
        {
            s.FlagAsPlagiarism(similarity);
            await _submissionRepo.UpdateAsync(s, ct);
        }
    }

    private static SubmissionDetailDto ToDetailDto(Submission s) => new(
        SubmissionId: s.Id.Value,
        StudentIdentifier: s.StudentIdentifier,
        SourceFiles: s.SourceFiles.Select(f => new SourceFileDto(f.FileName, f.Content)).ToList(),
        Status: s.Status.ToString(),
        TotalScore: s.TotalScore,
        RubricResults: s.RubricResults.Select(r => new RubricResultDto(r.CriteriaName, r.GivenScore, r.MaxScore, r.Comment)).ToList(),
        IsPlagiarismSuspected: s.IsPlagiarismSuspected,
        TeacherNote: s.TeacherNote,
        ErrorMessage: s.ErrorMessage,
        BuildLog: s.BuildLog
    );

    private static SubmissionSummaryDto ToSummaryDto(Submission s) => new(
        SubmissionId: s.Id.Value,
        SessionId: s.SessionId.Value,
        StudentIdentifier: s.StudentIdentifier,
        Status: s.Status.ToString(),
        TotalScore: s.TotalScore,
        IsPlagiarismSuspected: s.IsPlagiarismSuspected,
        MaxSimilarityPercentage: s.MaxSimilarityPercentage,
        ImportedAt: s.ImportedAt
    );
}
