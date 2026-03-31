using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Entity;
using Domain.Exception;
using Domain.ValueObject;

namespace Domain.Policy;

/// <summary>
/// Kiểm tra kết quả AI trả về có hợp lệ so với Rubric không.
/// Gọi trước khi AttachAIResults() để đảm bảo dữ liệu sạch.
/// Throw DomainException nếu có lỗi.
/// </summary>
public class AIResultValidationPolicy
{
    public void Validate(
        IReadOnlyList<RubricResult> aiResults,
        IReadOnlyList<RubricCriteria> rubricCriteria)
    {
        var errors = new List<string>();

        var rubricNames = rubricCriteria
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resultNames = aiResults
            .Select(r => r.CriteriaName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Tiêu chí bị AI bỏ sót
        foreach (var name in rubricNames.Except(resultNames))
            errors.Add($"AI thiếu kết quả cho tiêu chí: '{name}'.");

        // Tiêu chí AI tự thêm không có trong rubric
        foreach (var name in resultNames.Except(rubricNames))
            errors.Add($"AI trả về tiêu chí không tồn tại trong rubric: '{name}'.");

        // Điểm vượt phạm vi
        foreach (var result in aiResults)
        {
            var criteria = rubricCriteria.FirstOrDefault(c =>
                c.Name.Equals(result.CriteriaName, StringComparison.OrdinalIgnoreCase));

            if (criteria is null) continue;

            if (result.GivenScore < 0 || result.GivenScore > criteria.MaxScore)
                errors.Add($"Điểm AI cho '{result.CriteriaName}' ({result.GivenScore}) " +
                           $"vượt phạm vi [0, {criteria.MaxScore}].");
        }

        if (errors.Count > 0)
            throw new DomainException(
                $"Kết quả AI không hợp lệ:\n{string.Join("\n", errors)}");
    }
}

/// <summary>
/// Kiểm tra phiên chấm đã sẵn sàng để chạy AI chưa.
/// Throw DomainException nếu điều kiện không thỏa.
/// </summary>
public class GradingSessionReadyPolicy
{
    public void Validate(
        IReadOnlyList<Submission> submissions,
        Rubric rubric)
    {
        if (!rubric.Criteria.Any())
            throw new DomainException(
                "Rubric chưa có tiêu chí nào. Vui lòng thêm tiêu chí trước khi chấm.");

        if (!submissions.Any(s => s.Status == SubmissionStatus.Pending))
            throw new DomainException(
                "Không có bài nộp nào ở trạng thái Pending. Không có gì để chấm.");
    }
}

/// <summary>
/// Xử lý xung đột khi import bài nộp trùng StudentIdentifier.
/// </summary>
public enum ImportConflictResolution
{
    Skip,       // Bỏ qua file trùng, giữ bài cũ
    Replace     // Ghi đè bài cũ bằng bài mới
}

public class SubmissionImportConflictPolicy
{
    private readonly ImportConflictResolution _resolution;

    public SubmissionImportConflictPolicy(ImportConflictResolution resolution)
        => _resolution = resolution;

    /// <summary>
    /// Lọc danh sách bài cần import dựa theo chiến lược xử lý xung đột.
    /// Throw DomainException nếu resolution không hợp lệ.
    /// </summary>
    public IReadOnlyList<Submission> Resolve(
        IReadOnlyList<Submission> incoming,
        IReadOnlyList<Submission> existing)
    {
        var existingIds = existing
            .Select(s => s.StudentIdentifier)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _resolution switch
        {
            ImportConflictResolution.Skip =>
                incoming.Where(s => !existingIds.Contains(s.StudentIdentifier)).ToList(),

            ImportConflictResolution.Replace =>
                incoming.ToList(), // Application layer chịu trách nhiệm xoá bài cũ trước khi lưu

            _ => throw new DomainException($"ImportConflictResolution không xác định: {_resolution}.")
        };
    }
}

/// <summary>
/// Đánh dấu các bài nộp có độ tương đồng cao (nghi ngờ đạo văn).
/// Similarity % được tính bởi IPlagiarismDetectorPort ở Infrastructure.
/// Throw DomainException nếu threshold không hợp lệ.
/// </summary>
public class PlagiarismCheckPolicy
{
    private readonly double _suspectThreshold;

    /// <param name="suspectThreshold">% tương đồng từ ngưỡng này → nghi đạo văn. Mặc định 70%.</param>
    public PlagiarismCheckPolicy(double suspectThreshold = 70.0)
    {
        if (suspectThreshold < 0 || suspectThreshold > 100)
            throw new DomainException(
                $"Ngưỡng đạo văn phải trong khoảng [0, 100], nhận được: {suspectThreshold}.");

        _suspectThreshold = suspectThreshold;
    }

    /// <summary>
    /// Lọc cặp bài nghi đạo văn, gọi FlagAsPlagiarism() trên submission tương ứng.
    /// Trả về danh sách cặp bị nghi ngờ.
    /// </summary>
    public IReadOnlyList<PlagiarismSimilarity> Apply(
        IReadOnlyList<PlagiarismSimilarity> similarities,
        IReadOnlyList<Submission> submissions)
    {
        var suspected = similarities
            .Where(s => s.SimilarityPercentage >= _suspectThreshold)
            .ToList();

        var submissionMap = submissions.ToDictionary(s => s.Id);

        foreach (var pair in suspected)
        {
            if (submissionMap.TryGetValue(pair.SubmissionIdA, out var subA))
                subA.FlagAsPlagiarism(pair.SimilarityPercentage);

            if (submissionMap.TryGetValue(pair.SubmissionIdB, out var subB))
                subB.FlagAsPlagiarism(pair.SimilarityPercentage);
        }

        return suspected;
    }
}
