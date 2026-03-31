using System;
using System.Collections.Generic;

namespace Ports.DTO.GradingSession;

// ── Commands ──────────────────────────────────────────────────────────────────
public sealed record CreateSessionCommand(
    string Name,
    Guid RubricId,
    IReadOnlyList<string> FilePaths   // Đường dẫn file zip/rar trên máy GV
);

// ── Results ───────────────────────────────────────────────────────────────────
public sealed record CreateSessionResult(
    Guid SessionId,
    int ImportedCount,
    int SkippedCount    // File bị bỏ qua (giải nén không ra source code)
);

// ── Query DTOs ────────────────────────────────────────────────────────────────
public sealed record GradingSessionSummaryDto(
    Guid SessionId,
    string Name,
    Guid RubricId,
    string RubricName,
    int TotalSubmissions,
    int ReviewedCount,
    int ErrorCount,
    DateTime CreatedAt
);
