using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;

namespace InfrastructureService.Common.Queue;

public interface IBackgroundJobProcessor
{
    Task ProcessAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default);
}
