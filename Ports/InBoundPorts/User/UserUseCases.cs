using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.User;

namespace Ports.InBoundPorts.User;

public interface IRegisterUserUseCase
{
    Task<RegisterUserResponseDto> HandleAsync(
        RegisterUserRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface ILoginUseCase
{
    Task<LoginResponseDto> HandleAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IAssignUserRoleUseCase
{
    Task HandleAsync(
        AssignUserRoleRequestDto request,
        CancellationToken cancellationToken = default);
}
