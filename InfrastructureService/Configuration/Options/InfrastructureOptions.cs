namespace InfrastructureService.Configuration.Options;

public sealed class InfrastructureOptions
{
    public QueueOptions Queue { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public ReportOptions Report { get; set; } = new();
    public JudgingOptions Judging { get; set; } = new();
    public AiOptions AI { get; set; } = new();
    public RubricOptions Rubric { get; set; } = new();
    public PlagiarismOptions Plagiarism { get; set; } = new();
    public ResilienceOptions Resilience { get; set; } = new();
}

public sealed class QueueOptions
{
    public string Provider { get; set; } = "RabbitMq";
    public string ConsumerMode { get; set; } = "Worker";
    public bool AllowInProcessInDevelopment { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public RabbitMqOptions RabbitMq { get; set; } = new();
}

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "homeworkjudge.jobs";
    public string DeadLetterQueueName { get; set; } = "homeworkjudge.dlq";
}

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string RootPath { get; set; } = "storage";
}

public sealed class ReportOptions
{
    public string DefaultFormat { get; set; } = "csv";
}

public sealed class JudgingOptions
{
    public bool AllowUnsafeLocalExecution { get; set; } = false;
    public int CompileTimeoutSeconds { get; set; } = 10;
    public int ExecuteTimeoutSeconds { get; set; } = 5;
    public int WorkspaceRetentionMinutes { get; set; } = 30;
    public int RetryCount { get; set; } = 1;
}

public sealed class AiOptions
{
    public string Provider { get; set; } = "OpenAI";
    public int TimeoutSeconds { get; set; } = 20;
    public int RetryCount { get; set; } = 1;
    public OpenAiProviderOptions OpenAI { get; set; } = new();
    public GeminiProviderOptions Gemini { get; set; } = new();
}

public sealed class OpenAiProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1-mini";
    public double Temperature { get; set; } = 0.2;
}

public sealed class GeminiProviderOptions
{
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.0-flash-preview";
    public double Temperature { get; set; } = 0.2;
}

public sealed class RubricOptions
{
    public string Mode { get; set; } = "Hybrid";
    public RubricRuleEngineOptions RuleEngine { get; set; } = new();
    public RubricAiOptions AI { get; set; } = new();
}

public sealed class RubricRuleEngineOptions
{
    public bool Enabled { get; set; } = true;
    public double MinDeterministicWeight { get; set; } = 0.6;
}

public sealed class RubricAiOptions
{
    public bool Enabled { get; set; } = true;
    public double MaxAdjustmentPercent { get; set; } = 0.1;
    public int TimeoutSeconds { get; set; } = 20;
    public int RetryCount { get; set; } = 1;
}

public sealed class PlagiarismOptions
{
    public string Provider { get; set; } = "Local";
    public double Threshold { get; set; } = 0.8;
}

public sealed class ResilienceOptions
{
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int DefaultRetryCount { get; set; } = 1;
    public int RetryDelayMilliseconds { get; set; } = 200;
}
