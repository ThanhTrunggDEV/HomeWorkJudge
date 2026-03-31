using System.Text;
using InfrastructureService.Common.Errors;
using InfrastructureService.OutBoundAdapters.Report;
using Ports.DTO.Report;
using Ports.DTO.Submission;

namespace HomeWorkJudge.InfrastructureService.Tests.OutBoundAdapters.Report;

public class ReportExportPortTests
{
    [Fact]
    public async Task ExportAsync_Csv_ShouldReturnCsvPayloadAndMetadata()
    {
        var sut = new ReportExportPort();
        var sessionId = Guid.NewGuid();

        var submissions = new[]
        {
            CreateSummary("SV,002", 8.5, "AIGraded"),
            CreateSummary("SV001", 9.0, "Reviewed")
        };

        var result = await sut.ExportAsync(sessionId, submissions, includeCriteriaDetail: false, ExportFormat.Csv);

        var csv = Encoding.UTF8.GetString(result.FileBytes);

        Assert.Equal("text/csv", result.ContentType);
        Assert.EndsWith(".csv", result.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StudentIdentifier,TotalScore,Status", csv);
        Assert.Contains("\"SV,002\"", csv); // escaped value

        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, line => line.StartsWith("SV001,9") && line.Contains(",Reviewed,No,2026-03-31 10:20"));
        Assert.Contains(lines, line => line.StartsWith("\"SV,002\",8.5,AIGraded,No,2026-03-31 10:20"));
    }

    [Fact]
    public async Task ExportAsync_Excel_ShouldReturnXlsxPayloadAndMetadata()
    {
        var sut = new ReportExportPort();

        var result = await sut.ExportAsync(
            Guid.NewGuid(),
            [CreateSummary("SV001", 7.5, "AIGraded")],
            includeCriteriaDetail: true,
            format: ExportFormat.Excel);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.EndsWith(".xlsx", result.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.FileBytes);
    }

    [Fact]
    public async Task ExportAsync_UnsupportedFormat_ShouldThrowInfrastructureException()
    {
        var sut = new ReportExportPort();

        await Assert.ThrowsAsync<InfrastructureException>(() =>
            sut.ExportAsync(Guid.NewGuid(), [], includeCriteriaDetail: false, format: (ExportFormat)999));
    }

    private static SubmissionSummaryDto CreateSummary(string student, double score, string status)
        => new(
            SubmissionId: Guid.NewGuid(),
            SessionId: Guid.NewGuid(),
            StudentIdentifier: student,
            Status: status,
            TotalScore: score,
            IsPlagiarismSuspected: false,
            MaxSimilarityPercentage: null,
            ImportedAt: new DateTime(2026, 3, 31, 10, 20, 0, DateTimeKind.Utc));
}
