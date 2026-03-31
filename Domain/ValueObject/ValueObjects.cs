using System;

namespace Domain.ValueObject;

// ── Strongly-typed IDs ──────────────────────────────────────────────────────
public readonly record struct RubricId(Guid Value);
public readonly record struct RubricCriteriaId(Guid Value);
public readonly record struct GradingSessionId(Guid Value);
public readonly record struct SubmissionId(Guid Value);

// ── Enums ───────────────────────────────────────────────────────────────────
public enum SubmissionStatus
{
    Pending,      // Đã import, chưa chấm
    Grading,      // Đang build / AI đang xử lý
    BuildFailed,  // Build C# thất bại → 0 điểm, không chấm AI
    AIGraded,     // AI chấm xong, chờ GV review
    Reviewed,     // GV đã duyệt / chốt điểm
    Error         // AI chấm lỗi
}

// ── Value Objects ───────────────────────────────────────────────────────────

/// <summary>
/// Một file source code bên trong bài nộp (giải nén từ zip/rar).
/// Lưu trong DB dưới dạng JSON column.
/// </summary>
public record SourceFile(
    string FileName,  // VD: "main.cpp", "utils.h"
    string Content    // Nội dung text của file
);

/// <summary>
/// Kết quả mức độ tương đồng giữa 2 bài nộp trong cùng phiên chấm.
/// </summary>
public record PlagiarismSimilarity(
    SubmissionId SubmissionIdA,
    SubmissionId SubmissionIdB,
    string StudentIdentifierA,
    string StudentIdentifierB,
    double SimilarityPercentage   // 0.0 – 100.0
);

/// <summary>
/// Kết quả chấm cho một tiêu chí rubric (từ AI hoặc GV).
/// </summary>
public record RubricResult(
    string CriteriaName,  // Tên tiêu chí
    double GivenScore,    // Điểm được cho
    double MaxScore,      // Điểm tối đa của tiêu chí
    string Comment        // Nhận xét / lý do (AI hoặc GV)
);

/// <summary>
/// Một tiêu chí trong Rubric — immutable, managed by Rubric AR.
/// Có Id để Rubric tìm và thay thế khi cần cập nhật.
/// </summary>
public sealed record RubricCriteria(
    RubricCriteriaId Id,
    string Name,
    double MaxScore,
    string Description,
    int SortOrder
);
