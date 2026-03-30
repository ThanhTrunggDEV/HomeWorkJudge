using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.User;
using Ports.InBoundPorts.User;

namespace Application.UseCases.User;

public sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterUserResponseDto> HandleAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainException("Password is required.");
        }

        if (request.Password.Length < 8)
        {
            throw new DomainException("Password must be at least 8 characters.");
        }

        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new DomainException("Email already exists.");
        }

        var passwordHash = PasswordHasher.Hash(request.Password);

        var user = new Domain.Entity.User(
            new UserId(Guid.NewGuid()),
            request.Email.Trim(),
            request.FullName.Trim(),
            UserRole.Student,
            passwordHash);

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterUserResponseDto(user.Id.Value, user.Email, EnumMapper.ToDto(user.Role));
    }
}

public sealed class LoginUseCase : ILoginUseCase
{
    private readonly IUserRepository _userRepository;

    public LoginUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResponseDto> HandleAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainException("Password is required.");
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null)
        {
            throw new DomainException("Invalid credentials.");
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new DomainException("Invalid credentials.");
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);
        var token = SecureTokenGenerator.Generate();

        return new LoginResponseDto(user.Id.Value, token, expiresAt);
    }
}

public sealed class AssignUserRoleUseCase : IAssignUserRoleUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignUserRoleUseCase(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(AssignUserRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userRepository.GetByIdAsync(new UserId(request.UserId))
            ?? throw new DomainException("User not found.");

        user.ChangeRole(EnumMapper.ToDomain(request.Role));

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
