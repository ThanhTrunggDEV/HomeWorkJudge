using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.Plagiarism;

/// <summary>
/// Outbound port: kiểm tra đạo văn giữa các bài nộp trong cùng 1 phiên.
/// Implementation ở Infrastructure (token-based, AST diff, hoặc công cụ ngoài).
/// </summary>
public interface IPlagiarismDetectionPort
{
    Task<IReadOnlyList<PlagiarismResultDto>> DetectAsync(
        IReadOnlyList<SubmissionFilesDto> submissions,
        CancellationToken cancellationToken = default);
}
