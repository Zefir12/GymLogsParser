using System.Text.Json.Serialization;

namespace GymLog.Api.AI;

public sealed class DeepSeekChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<DeepSeekMessage> Messages { get; set; } = [];

    [JsonPropertyName("thinking")]
    public DeepSeekThinking Thinking { get; set; } = new();

    [JsonPropertyName("response_format")]
    public DeepSeekResponseFormat ResponseFormat { get; set; } = new();

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public sealed class ParseWorkoutOptions
{
    [JsonPropertyName("barbellWeightsArePerSide")]
    public bool BarbellWeightsArePerSide { get; set; }
}

public sealed class ParseWorkoutRequest
{
    public string Text { get; set; } = string.Empty;

    public bool BarbellWeightsArePerSide { get; set; }
}

public sealed class DeepSeekMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class DeepSeekThinking
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "disabled";
}

public sealed class DeepSeekResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

public sealed class DeepSeekChatResponse
{
    [JsonPropertyName("choices")]
    public List<DeepSeekChoice> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

public sealed class DeepSeekChoice
{
    [JsonPropertyName("message")]
    public DeepSeekResponseMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class DeepSeekResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("prompt_cache_hit_tokens")]
    public int PromptCacheHitTokens { get; set; }

    [JsonPropertyName("prompt_cache_miss_tokens")]
    public int PromptCacheMissTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}