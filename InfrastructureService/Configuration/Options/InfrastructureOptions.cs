namespace InfrastructureService.Configuration.Options;

public sealed class InfrastructureOptions
{
    public AiOptions AI { get; set; } = new();
    public PlagiarismOptions Plagiarism { get; set; } = new();
    public ResilienceOptions Resilience { get; set; } = new();
}

public sealed class AiOptions
{
    public int TimeoutSeconds { get; set; } = 60;
    public int RetryCount { get; set; } = 2;
    public GeminiProviderOptions Gemini { get; set; } = new();
}

public sealed class GeminiProviderOptions
{
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public double Temperature { get; set; } = 0.2;
}

public sealed class PlagiarismOptions
{
    public double DefaultThresholdPercentage { get; set; } = 70.0;
}

public sealed class ResilienceOptions
{
    public int DefaultTimeoutSeconds { get; set; } = 60;
    public int DefaultRetryCount { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 500;
}
