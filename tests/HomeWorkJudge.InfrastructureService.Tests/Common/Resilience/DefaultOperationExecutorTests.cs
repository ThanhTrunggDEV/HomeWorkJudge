using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeWorkJudge.InfrastructureService.Tests.Common.Resilience;

public class DefaultOperationExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_Generic_WhenActionSucceeds_ShouldReturnValue()
    {
        var sut = CreateSut(new ResilienceOptions
        {
            DefaultRetryCount = 1,
            DefaultTimeoutSeconds = 5,
            RetryDelayMilliseconds = 1
        });

        var result = await sut.ExecuteAsync("test-op", _ => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_NonGeneric_ShouldInvokeActionOnce()
    {
        var sut = CreateSut(new ResilienceOptions
        {
            DefaultRetryCount = 2,
            DefaultTimeoutSeconds = 5,
            RetryDelayMilliseconds = 1
        });

        var called = 0;
        await sut.ExecuteAsync("test-op", _ =>
        {
            called++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, called);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFirstAttemptFails_ShouldRetryAndSucceed()
    {
        var sut = CreateSut(new ResilienceOptions
        {
            DefaultRetryCount = 2,
            DefaultTimeoutSeconds = 5,
            RetryDelayMilliseconds = 1
        });

        var attempts = 0;
        var result = await sut.ExecuteAsync("retry-op", _ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("transient");
            }

            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionAlwaysFails_ShouldThrowInvalidOperationException()
    {
        var sut = CreateSut(new ResilienceOptions
        {
            DefaultRetryCount = 1,
            DefaultTimeoutSeconds = 5,
            RetryDelayMilliseconds = 1
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync<int>("fail-op", _ => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionIsNull_ShouldThrowArgumentNullException()
    {
        var sut = CreateSut(new ResilienceOptions());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.ExecuteAsync<int>("null-op", null!));
    }

    private static DefaultOperationExecutor CreateSut(ResilienceOptions options)
        => new(Options.Create(options), NullLogger<DefaultOperationExecutor>.Instance);
}
