using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.Build;

/// <summary>
/// Outbound port: Build solution/project C# từ danh sách source file của học sinh.
/// Trả về kết quả build (thành công/thất bại) và full build log.
/// </summary>
public interface ICSharpBuildPort
{
    /// <summary>
    /// Ghi source files ra thư mục temp, chạy "dotnet build", trả về kết quả.
    /// </summary>
    Task<BuildResult> BuildAsync(
        IReadOnlyList<SourceFileDto> sourceFiles,
        string submissionId,
        CancellationToken ct = default);
}

/// <summary>Kết quả build C# solution/project.</summary>
public sealed record BuildResult(
    bool Success,
    string BuildLog  // stdout + stderr của dotnet build
);
