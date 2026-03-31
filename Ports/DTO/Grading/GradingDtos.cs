using System;
using System.Collections.Generic;

namespace Ports.DTO.Grading;

// ── Commands ──────────────────────────────────────────────────────────────────
public sealed record StartGradingCommand(Guid SessionId);
public sealed record RegradeSubmissionCommand(Guid SubmissionId);
public sealed record RegradeSessionCommand(Guid SessionId);

public sealed record ApproveSubmissionCommand(Guid SubmissionId);

public sealed record OverrideCriteriaScoreCommand(
    Guid SubmissionId,
    string CriteriaName,
    double NewScore,
    string Comment
);

public sealed record OverrideTotalScoreCommand(
    Guid SubmissionId,
    double NewScore
);

public sealed record AddTeacherNoteCommand(
    Guid SubmissionId,
    string? Note
);

public sealed record CheckPlagiarismCommand(
    Guid SessionId,
    double ThresholdPercentage = 70.0
);

// ── Results ───────────────────────────────────────────────────────────────────
public sealed record StartGradingResult(int StartedCount);

public sealed record CheckPlagiarismResult(
    IReadOnlyList<Ports.DTO.Submission.PlagiarismResultDto> SuspectedPairs
);
