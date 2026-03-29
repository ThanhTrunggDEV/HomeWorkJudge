using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Options;
using Ports.DTO.Report;
using Ports.OutBoundPorts.Report;

namespace InfrastructureService.OutBoundAdapters.Report;

public sealed class CsvReportExportPort : IReportExportPort
{
    private readonly ReportOptions _options;

    public CsvReportExportPort(IOptions<ReportOptions> options)
    {
        _options = options.Value;
    }

    public Task<ExportScoreReportResponseDto> ExportScoreboardAsync(
        IReadOnlyList<ScoreboardItemDto> items,
        string format,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var requestedFormat = string.IsNullOrWhiteSpace(format) ? _options.DefaultFormat : format;

        if (!string.Equals(requestedFormat, "csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InfrastructureException(
                "REPORT_UNSUPPORTED_FORMAT",
                $"Format '{requestedFormat}' is not supported. Only 'csv' is currently available.");
        }

        var csv = BuildCsv(items);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"scoreboard_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        var response = new ExportScoreReportResponseDto(
            fileName,
            "text/csv",
            bytes);

        return Task.FromResult(response);
    }

    private static string BuildCsv(IReadOnlyList<ScoreboardItemDto> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("StudentId,StudentName,AverageScore,SubmissionCount");

        foreach (var item in items)
        {
            builder.AppendLine(string.Join(",",
                Escape(item.StudentId.ToString()),
                Escape(item.StudentName),
                Escape(item.AverageScore.ToString("0.##", CultureInfo.InvariantCulture)),
                Escape(item.SubmissionCount.ToString())));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value}\"";
        }

        return value;
    }
}
