using System;
using System.Collections.Generic;

namespace Ports.DTO.Submission;

/// <summary>Một file source code bên trong bài nộp (sau khi giải nén zip).</summary>
public sealed record SourceFileDto(
    string FileName,
    string Content
);

/// <summary>Đại diện 1 bài nộp khi gửi sang plagiarism detector.</summary>
public sealed record SubmissionFilesDto(
    Guid SubmissionId,
    string StudentIdentifier,
    IReadOnlyList<SourceFileDto> SourceFiles
);

/// <summary>Thông tin 1 bài nộp trong danh sách phiên chấm.</summary>
public sealed record SubmissionSummaryDto(
    Guid SubmissionId,
    Guid SessionId,
    string StudentIdentifier,
    string Status,
    double TotalScore,
    bool IsPlagiarismSuspected,
    double? MaxSimilarityPercentage,
    DateTime ImportedAt
);

/// <summary>Chi tiết bài nộp để GV review.</summary>
public sealed record SubmissionDetailDto(
    Guid SubmissionId,
    string StudentIdentifier,
    IReadOnlyList<SourceFileDto> SourceFiles,
    string Status,
    double TotalScore,
    IReadOnlyList<RubricResultDto> RubricResults,
    bool IsPlagiarismSuspected,
    string? TeacherNote,
    string? ErrorMessage,
    string? BuildLog        // output của dotnet build; null nếu build thành công / chưa build
);

/// <summary>Kết quả chấm 1 tiêu chí (AI hoặc GV đã override).</summary>
public sealed record RubricResultDto(
    string CriteriaName,
    double GivenScore,
    double MaxScore,
    string Comment
);

/// <summary>Thống kê tổng hợp của 1 phiên chấm.</summary>
public sealed record SessionStatisticsDto(
    int TotalCount,
    int PendingCount,
    int GradingCount,
    int AIGradedCount,
    int ReviewedCount,
    int ErrorCount,
    double? AverageScore,
    double? MinScore,
    double? MaxScore
);

/// <summary>Kết quả phát hiện đạo văn giữa 2 bài nộp.</summary>
public sealed record PlagiarismResultDto(
    Guid SubmissionIdA,
    Guid SubmissionIdB,
    string StudentIdentifierA,
    string StudentIdentifierB,
    double SimilarityPercentage
);
