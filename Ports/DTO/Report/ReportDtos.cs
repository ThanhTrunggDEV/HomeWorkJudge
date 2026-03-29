using System;

namespace Ports.DTO.Report;

public sealed record ScoreboardItemDto(
    Guid StudentId,
    string StudentName,
    double AverageScore,
    int SubmissionCount);

public sealed record ExportScoreReportResponseDto(
    string FileName,
    string ContentType,
    byte[] Content);
