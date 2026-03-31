using System;

namespace Ports.DTO.Report;

// ── Commands ──────────────────────────────────────────────────────────────────
public sealed record ExportScoreCommand(
    Guid SessionId,
    ExportFormat Format,
    bool IncludeCriteriaDetail  // true = xuất chi tiết từng tiêu chí + nhận xét
);

// ── Results ───────────────────────────────────────────────────────────────────
public sealed record ExportScoreResult(
    byte[] FileBytes,
    string FileName,    // VD: "CS101_Bai1_2024.csv"
    string ContentType
);

// ── Enums ─────────────────────────────────────────────────────────────────────
public enum ExportFormat { Csv, Excel }
