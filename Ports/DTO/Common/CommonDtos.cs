using System.Collections.Generic;
using System;

namespace Ports.DTO.Common;

public sealed record ErrorDto(string Code, string Message, string? Details = null);

public sealed record JobEnvelopeDto(
    string JobName,
    string Payload,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    int RetryCount = 0);

public sealed record PagedRequestDto(int PageNumber = 1, int PageSize = 20);

public sealed record PagedResponseDto<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount);
