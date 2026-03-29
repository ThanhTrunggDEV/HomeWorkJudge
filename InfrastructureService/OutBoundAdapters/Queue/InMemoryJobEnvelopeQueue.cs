using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using InfrastructureService.Common.Queue;
using Ports.DTO.Common;

namespace InfrastructureService.OutBoundAdapters.Queue;

internal sealed class InMemoryJobEnvelopeQueue : IJobEnvelopeQueue
{
    private readonly Channel<JobEnvelopeDto> _channel;

    public InMemoryJobEnvelopeQueue()
    {
        _channel = Channel.CreateUnbounded<JobEnvelopeDto>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public ValueTask EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(envelope, cancellationToken);

    public ValueTask<JobEnvelopeDto> DequeueAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);
}
