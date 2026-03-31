using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Resilience;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Plagiarism;

namespace InfrastructureService.OutBoundAdapters.Plagiarism;

/// <summary>
/// Implements IPlagiarismDetectionPort bằng Jaccard similarity trên tokens + shingles.
/// So sánh O(n²) tất cả cặp bài nộp. Phù hợp cho phòng học ~100 sv.
/// </summary>
public sealed partial class LocalPlagiarismDetectionPort : IPlagiarismDetectionPort
{
    private readonly IOperationExecutor _executor;

    public LocalPlagiarismDetectionPort(IOperationExecutor executor)
        => _executor = executor;

    public Task<IReadOnlyList<PlagiarismResultDto>> DetectAsync(
        IReadOnlyList<SubmissionFilesDto> submissions,
        CancellationToken ct = default)
        => _executor.ExecuteAsync(
            "plagiarism.detect-session",
            token => DetectInternalAsync(submissions, token),
            ct);

    private static Task<IReadOnlyList<PlagiarismResultDto>> DetectInternalAsync(
        IReadOnlyList<SubmissionFilesDto> submissions,
        CancellationToken ct)
    {
        var result = new List<PlagiarismResultDto>();

        for (int i = 0; i < submissions.Count; i++)
        {
            for (int j = i + 1; j < submissions.Count; j++)
            {
                ct.ThrowIfCancellationRequested();

                var leftCode  = ConcatCode(submissions[i].SourceFiles);
                var rightCode = ConcatCode(submissions[j].SourceFiles);

                var tokenSim   = Jaccard(Tokenize(leftCode), Tokenize(rightCode));
                var shingleSim = Jaccard(Shingles(leftCode, 5), Shingles(rightCode, 5));
                var similarity = Math.Round(Math.Clamp(tokenSim * 0.6 + shingleSim * 0.4, 0, 1) * 100, 2);

                result.Add(new PlagiarismResultDto(
                    SubmissionIdA: submissions[i].SubmissionId,
                    SubmissionIdB: submissions[j].SubmissionId,
                    StudentIdentifierA: submissions[i].StudentIdentifier,
                    StudentIdentifierB: submissions[j].StudentIdentifier,
                    SimilarityPercentage: similarity));
            }
        }

        return Task.FromResult<IReadOnlyList<PlagiarismResultDto>>(result);
    }

    private static string ConcatCode(IReadOnlyList<SourceFileDto> files)
        => string.Join("\n", files.Select(f => f.Content));

    private static HashSet<string> Tokenize(string src)
    {
        if (string.IsNullOrWhiteSpace(src)) return [];
        return [.. TokenRegex().Matches(src)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t.Length > 1)];
    }

    private static HashSet<string> Shingles(string src, int k)
    {
        var norm = new string(src.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
        if (norm.Length <= k) return string.IsNullOrEmpty(norm) ? [] : [norm];
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= norm.Length - k; i++) set.Add(norm.Substring(i, k));
        return set;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union        = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    [GeneratedRegex("[A-Za-z_][A-Za-z0-9_]*|[0-9]+", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}