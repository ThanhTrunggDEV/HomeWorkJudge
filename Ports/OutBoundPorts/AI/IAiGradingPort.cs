using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.AI;

namespace Ports.OutBoundPorts.AI;

public interface IAiGradingPort
{
    Task<AiGradeSubmissionResponseDto> GradeSubmissionAsync(
        AiGradeSubmissionRequestDto request,
        CancellationToken cancellationToken = default);
}
