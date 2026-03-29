using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;

namespace InfrastructureService.Common.Queue;

public interface IDeadLetterJobSink
{
    Task SaveAsync(JobEnvelopeDto envelope, string reason, CancellationToken cancellationToken = default);
}
