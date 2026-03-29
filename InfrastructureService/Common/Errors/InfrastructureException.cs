using System;

namespace InfrastructureService.Common.Errors;

public sealed class InfrastructureException : Exception
{
    public InfrastructureException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
