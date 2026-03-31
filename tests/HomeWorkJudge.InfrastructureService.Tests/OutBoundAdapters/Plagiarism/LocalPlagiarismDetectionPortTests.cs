using InfrastructureService.Common.Resilience;
using InfrastructureService.OutBoundAdapters.Plagiarism;
using Ports.DTO.Submission;

namespace HomeWorkJudge.InfrastructureService.Tests.OutBoundAdapters.Plagiarism;

public class LocalPlagiarismDetectionPortTests
{
    [Fact]
    public async Task DetectAsync_WithThreeSubmissions_ShouldReturnAllPairs()
    {
        var sut = new LocalPlagiarismDetectionPort(new PassThroughExecutor());

        var submissions = new[]
        {
            CreateSubmission("SV001", "int a = 1; int b = a + 2;"),
            CreateSubmission("SV002", "int a = 1; int b = a + 2;"),
            CreateSubmission("SV003", "Console.WriteLine(123);")
        };

        var result = await sut.DetectAsync(submissions);

        Assert.Equal(3, result.Count); // nC2 với n = 3
        Assert.All(result, r => Assert.InRange(r.SimilarityPercentage, 0, 100));

        var topPair = result.OrderByDescending(x => x.SimilarityPercentage).First();
        Assert.True(topPair.SimilarityPercentage >= 70);
    }

    [Fact]
    public async Task DetectAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        var sut = new LocalPlagiarismDetectionPort(new PassThroughExecutor());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var submissions = new[]
        {
            CreateSubmission("SV001", "int a = 1;"),
            CreateSubmission("SV002", "int a = 2;")
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.DetectAsync(submissions, cts.Token));
    }

    private static SubmissionFilesDto CreateSubmission(string studentId, string code)
        => new(
            SubmissionId: Guid.NewGuid(),
            StudentIdentifier: studentId,
            SourceFiles: [new SourceFileDto("Program.cs", code)]);

    private sealed class PassThroughExecutor : IOperationExecutor
    {
        public async Task ExecuteAsync(string operationName, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => await action(cancellationToken);

        public async Task<T> ExecuteAsync<T>(string operationName, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => await action(cancellationToken);
    }
}
