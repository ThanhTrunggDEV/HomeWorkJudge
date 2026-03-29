using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Classroom;

namespace Ports.InBoundPorts.Classroom;

public interface ICreateClassroomUseCase
{
    Task<CreateClassroomResponseDto> HandleAsync(
        CreateClassroomRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IJoinClassroomUseCase
{
    Task<JoinClassroomResponseDto> HandleAsync(
        JoinClassroomRequestDto request,
        CancellationToken cancellationToken = default);
}
