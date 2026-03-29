using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Resilience;
using Ports.OutBoundPorts.Plagiarism;

namespace InfrastructureService.OutBoundAdapters.Plagiarism;

public sealed partial class LocalPlagiarismDetectionPort : IPlagiarismDetectionPort
{
    private readonly IOperationExecutor _operationExecutor;

    public LocalPlagiarismDetectionPort(IOperationExecutor operationExecutor)
    {
        _operationExecutor = operationExecutor;
    }

    public Task<double> CalculateSimilarityAsync(
        string leftSourceCode,
        string rightSourceCode,
        CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "plagiarism.calculate-similarity",
            ct => CalculateInternalAsync(leftSourceCode, rightSourceCode, ct),
            cancellationToken);

    private static Task<double> CalculateInternalAsync(
        string leftSourceCode,
        string rightSourceCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var leftTokenSet = Tokenize(leftSourceCode);
        var rightTokenSet = Tokenize(rightSourceCode);

        var tokenSimilarity = Jaccard(leftTokenSet, rightTokenSet);
        var shingleSimilarity = Jaccard(ToShingles(leftSourceCode, 5), ToShingles(rightSourceCode, 5));

        var similarity = Math.Clamp(tokenSimilarity * 0.6 + shingleSimilarity * 0.4, 0, 1);
        return Task.FromResult(Math.Round(similarity, 4));
    }

    private static HashSet<string> Tokenize(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var matches = TokenRegex().Matches(source);
        var tokens = matches
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length > 1);

        return [.. tokens];
    }

    private static HashSet<string> ToShingles(string source, int shingleLength)
    {
        var normalized = string.Concat((source ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        if (normalized.Length <= shingleLength)
        {
            return string.IsNullOrEmpty(normalized)
                ? []
                : [normalized];
        }

        var shingles = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i <= normalized.Length - shingleLength; i++)
        {
            shingles.Add(normalized.Substring(i, shingleLength));
        }

        return shingles;
    }

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 1;
        }

        var intersectionCount = left.Intersect(right).Count();
        var unionCount = left.Union(right).Count();

        if (unionCount == 0)
        {
            return 1;
        }

        return (double)intersectionCount / unionCount;
    }

    [GeneratedRegex("[A-Za-z_][A-Za-z0-9_]*|[0-9]+", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}