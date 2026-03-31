using System;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureService.Common.Resilience;

public sealed class DefaultOperationExecutor : IOperationExecutor
{
    private readonly ResilienceOptions _options;
    private readonly ILogger<DefaultOperationExecutor> _logger;

    public DefaultOperationExecutor(
        IOptions<ResilienceOptions> options,
        ILogger<DefaultOperationExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task ExecuteAsync(
        string operationName,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
        => ExecuteAsync<object?>(
            operationName,
            async ct =>
            {
                await action(ct);
                return null;
            },
            cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var maxAttempts = Math.Max(1, _options.DefaultRetryCount + 1);
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.DefaultTimeoutSeconds));
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(1, _options.RetryDelayMilliseconds));
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                return await action(linkedCts.Token);
            }
            catch (OperationCanceledException oce) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                lastException = oce;
                _logger.LogWarning(
                    "Operation {OperationName} timed out on attempt {Attempt}/{MaxAttempts}.",
                    operationName,
                    attempt,
                    maxAttempts);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    operationName,
                    attempt,
                    maxAttempts);
            }

            await Task.Delay(retryDelay, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Operation '{operationName}' failed after {maxAttempts} attempt(s).", lastException);
    }
}
