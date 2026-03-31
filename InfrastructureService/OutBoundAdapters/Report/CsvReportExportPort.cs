using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using InfrastructureService.Common.Errors;
using Ports.DTO.Report;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Report;

namespace InfrastructureService.OutBoundAdapters.Report;

/// <summary>
/// Implements IReportExportPort: xuất bảng điểm ra CSV hoặc Excel (.xlsx).
/// </summary>
public sealed class ReportExportPort : IReportExportPort
{
    public Task<ExportScoreResult> ExportAsync(
        Guid sessionId,
        IReadOnlyList<SubmissionSummaryDto> submissions,
        bool includeCriteriaDetail,
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return format switch
        {
            ExportFormat.Csv   => Task.FromResult(BuildCsv(sessionId, submissions)),
            ExportFormat.Excel => Task.FromResult(BuildXlsx(sessionId, submissions)),
            _                  => throw new InfrastructureException("REPORT_UNSUPPORTED_FORMAT",
                                      $"Format '{format}' không được hỗ trợ.")
        };
    }

    // ── CSV ──────────────────────────────────────────────────────────────────

    private static ExportScoreResult BuildCsv(Guid sessionId, IReadOnlyList<SubmissionSummaryDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("StudentIdentifier,TotalScore,Status,IsPlagiarismSuspected,ImportedAt");

        foreach (var item in items.OrderBy(i => i.StudentIdentifier))
        {
            sb.AppendLine(string.Join(",",
                Escape(item.StudentIdentifier),
                item.TotalScore.ToString("0.##", CultureInfo.InvariantCulture),
                Escape(item.Status),
                item.IsPlagiarismSuspected ? "Yes" : "No",
                item.ImportedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        }

        var fileName = $"scores_{sessionId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return new ExportScoreResult(Encoding.UTF8.GetBytes(sb.ToString()), fileName, "text/csv");
    }

    // ── Excel ────────────────────────────────────────────────────────────────

    private static ExportScoreResult BuildXlsx(Guid sessionId, IReadOnlyList<SubmissionSummaryDto> items)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Scores");

        var headers = new[] { "StudentIdentifier", "TotalScore", "Status", "PlagiarismSuspected", "ImportedAt" };
        for (int col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x73, 0xE8);
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var item in items.OrderBy(i => i.StudentIdentifier))
        {
            sheet.Cell(row, 1).Value = item.StudentIdentifier;
            sheet.Cell(row, 2).Value = item.TotalScore;
            sheet.Cell(row, 3).Value = item.Status;
            sheet.Cell(row, 4).Value = item.IsPlagiarismSuspected ? "Yes" : "No";
            sheet.Cell(row, 5).Value = item.ImportedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var fileName = $"scores_{sessionId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return new ExportScoreResult(ms.ToArray(), fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static string Escape(string v)
    {
        if (v.Contains('"')) v = v.Replace("\"", "\"\"");
        return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? $"\"{v}\"" : v;
    }
}
