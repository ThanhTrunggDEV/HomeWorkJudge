using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;

namespace InfrastructureService.Common.Queue;

public interface IJobEnvelopeQueue
{
    ValueTask EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default);
    ValueTask<JobEnvelopeDto> DequeueAsync(CancellationToken cancellationToken = default);
}
