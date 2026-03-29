using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;

namespace Ports.OutBoundPorts.Queue;

public interface IBackgroundJobQueuePort
{
    Task EnqueueAsync(
    JobEnvelopeDto envelope,
        CancellationToken cancellationToken = default);
}
