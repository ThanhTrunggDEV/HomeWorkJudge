using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Rubric;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.AI;

/// <summary>
/// Outbound port: AI chấm bài theo rubric (UC-07).
/// Nhận danh sách source file + tiêu chí rubric → trả về điểm từng tiêu chí.
/// AI tự nhận diện ngôn ngữ lập trình từ nội dung file.
/// </summary>
public interface IAiGradingPort
{
    Task<IReadOnlyList<RubricScoreDto>> GradeAsync(
        IReadOnlyList<SourceFileDto> sourceFiles,
        IReadOnlyList<RubricCriteriaDto> criteria,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outbound port: AI tạo bản nháp rubric từ mô tả đề bài (UC-02).
/// GV nhận về danh sách tiêu chí gợi ý, có thể chỉnh sửa trước khi lưu.
/// </summary>
public interface IAiRubricGeneratorPort
{
    Task<IReadOnlyList<RubricCriteriaDto>> GenerateAsync(
        string assignmentDescription,
        CancellationToken cancellationToken = default);
}
