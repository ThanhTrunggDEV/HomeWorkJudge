using System;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;

namespace Ports.InBoundPorts.Submission;

public interface ISubmitCodeUseCase
{
    Task<SubmitCodeResponseDto> HandleAsync(
        SubmitCodeRequestDto request,
        CancellationToken cancellationToken = default);
}
