using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Event;
using Domain.Exception;
using Domain.ValueObject;

namespace Domain.Entity;

/// <summary>
/// Aggregate Root: Bài nộp của một sinh viên trong một phiên chấm.
/// Chứa source code (giải nén từ zip) và kết quả chấm AI/GV.
/// </summary>
public class Submission : EntityBase
{
    public SubmissionId Id { get; private set; }
    public GradingSessionId SessionId { get; private set; }

    /// <summary>Tên file gốc bỏ phần mở rộng. VD: "2021001.zip" → "2021001".</summary>
    public string StudentIdentifier { get; private set; }

    /// <summary>Danh sách file source code (lọc từ zip). Lưu JSON column trong SQLite.</summary>
    public IReadOnlyList<SourceFile> SourceFiles { get; private set; } = [];

    public DateTime ImportedAt { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public double TotalScore { get; private set; }

    private readonly List<RubricResult> _rubricResults = [];
    public IReadOnlyList<RubricResult> RubricResults => _rubricResults.AsReadOnly();

    public string? TeacherNote { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>True nếu bài này bị nghi ngờ đạo văn sau khi kiểm tra.</summary>
    public bool IsPlagiarismSuspected { get; private set; }

    /// <summary>Phần trăm tương đồng cao nhất với bài khác trong phiên (0–100).</summary>
    public double? MaxSimilarityPercentage { get; private set; }

    private Submission() { StudentIdentifier = null!; } // EF Core

    public Submission(
        SubmissionId id,
        GradingSessionId sessionId,
        string studentIdentifier,
        IReadOnlyList<SourceFile> sourceFiles)
    {
        if (string.IsNullOrWhiteSpace(studentIdentifier))
            throw new ArgumentException("StudentIdentifier không được rỗng.", nameof(studentIdentifier));
        if (sourceFiles == null || sourceFiles.Count == 0)
            throw new ArgumentException("Bài nộp phải có ít nhất 1 file source code.", nameof(sourceFiles));

        Id = id;
        SessionId = sessionId;
        StudentIdentifier = studentIdentifier.Trim();
        SourceFiles = [.. sourceFiles];
        ImportedAt = DateTime.UtcNow;
        Status = SubmissionStatus.Pending;
        TotalScore = 0;

        Raise(new SubmissionsImportedEvent(sessionId, 1, DateTimeOffset.UtcNow));
    }

    // ── AI Grading lifecycle ─────────────────────────────────────────────────

    /// <summary>Bắt đầu chấm AI. Guard: chỉ từ Pending hoặc Error.</summary>
    public void StartGrading()
    {
        if (Status is not (SubmissionStatus.Pending or SubmissionStatus.Error))
            throw new DomainException(
                $"Không thể bắt đầu chấm từ trạng thái '{Status}'.");

        Status = SubmissionStatus.Grading;
        ErrorMessage = null;
        Raise(new SubmissionGradingStartedEvent(Id, DateTimeOffset.UtcNow));
    }

    /// <summary>Lưu kết quả AI. Guard: chỉ từ Grading.</summary>
    public void AttachAIResults(IReadOnlyList<RubricResult> results)
    {
        if (Status != SubmissionStatus.Grading)
            throw new DomainException(
                $"Không thể gắn kết quả AI khi trạng thái là '{Status}'.");
        if (results == null || results.Count == 0)
            throw new ArgumentException("Kết quả AI không được rỗng.", nameof(results));

        _rubricResults.Clear();
        _rubricResults.AddRange(results);
        TotalScore = _rubricResults.Sum(r => r.GivenScore);
        Status = SubmissionStatus.AIGraded;

        Raise(new SubmissionAIGradedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }

    /// <summary>Đánh dấu lỗi AI. Guard: chỉ từ Grading.</summary>
    public void MarkError(string errorMessage)
    {
        if (Status != SubmissionStatus.Grading)
            throw new DomainException(
                $"Không thể đánh dấu lỗi khi trạng thái là '{Status}'.");

        Status = SubmissionStatus.Error;
        ErrorMessage = errorMessage;
        Raise(new SubmissionAIFailedEvent(Id, errorMessage, DateTimeOffset.UtcNow));
    }

    // ── Review / Override ────────────────────────────────────────────────────

    /// <summary>GV duyệt điểm AI. Guard: chỉ từ AIGraded (idempotent nếu đã Reviewed).</summary>
    public void Approve()
    {
        if (Status == SubmissionStatus.Reviewed)
            return; // Idempotent — đã duyệt rồi

        if (Status != SubmissionStatus.AIGraded)
            throw new DomainException(
                $"Chỉ duyệt được bài ở trạng thái AIGraded, hiện đang '{Status}'.");

        Status = SubmissionStatus.Reviewed;
        Raise(new SubmissionReviewedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }

    /// <summary>GV sửa điểm 1 tiêu chí → tính lại tổng.</summary>
    public void OverrideCriteriaScore(string criteriaName, double newScore, string comment)
    {
        if (Status != SubmissionStatus.AIGraded)
            throw new DomainException(
                $"Chỉ sửa tiêu chí khi ở trạng thái AIGraded, hiện đang '{Status}'.");

        var existing = _rubricResults.FirstOrDefault(r =>
            r.CriteriaName.Equals(criteriaName, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException($"Không tìm thấy tiêu chí '{criteriaName}'.");

        if (newScore < 0 || newScore > existing.MaxScore)
            throw new ArgumentOutOfRangeException(nameof(newScore),
                $"Điểm phải trong khoảng [0, {existing.MaxScore}].");

        var index = _rubricResults.IndexOf(existing);
        _rubricResults[index] = existing with { GivenScore = newScore, Comment = comment };
        TotalScore = _rubricResults.Sum(r => r.GivenScore);
        Status = SubmissionStatus.Reviewed;

        Raise(new SubmissionReviewedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }

    /// <summary>GV ghi đè tổng điểm trực tiếp (bỏ qua tiêu chí).</summary>
    public void OverrideTotalScore(double newScore)
    {
        if (newScore < 0)
            throw new ArgumentOutOfRangeException(nameof(newScore), "Tổng điểm không được âm.");

        TotalScore = newScore;
        Status = SubmissionStatus.Reviewed;
        Raise(new SubmissionReviewedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }


    /// <summary>Reset về Pending để AI chấm lại (UC-08).</summary>
    public void ResetForRegrade()
    {
        _rubricResults.Clear();
        TotalScore = 0;
        ErrorMessage = null;
        Status = SubmissionStatus.Pending;
    }

    /// <summary>Thêm hoặc cập nhật ghi chú GV.</summary>
    public void AddTeacherNote(string? note)
    {
        TeacherNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>GV đánh dấu bài bị nghi ngờ đạo văn (gọi bởi PlagiarismCheckPolicy).</summary>
    public void FlagAsPlagiarism(double maxSimilarityPercentage)
    {
        if (maxSimilarityPercentage < 0 || maxSimilarityPercentage > 100)
            throw new ArgumentOutOfRangeException(nameof(maxSimilarityPercentage));

        IsPlagiarismSuspected = true;
        MaxSimilarityPercentage = maxSimilarityPercentage;
    }

    /// <summary>GV xác nhận bài không phải đạo văn (bỏ flag).</summary>
    public void ClearPlagiarismFlag()
    {
        IsPlagiarismSuspected = false;
        MaxSimilarityPercentage = null;
    }
}
