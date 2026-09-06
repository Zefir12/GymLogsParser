namespace Core.Configuration;

public sealed class DeepSeekOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.deepseek.com/";

    public string Model { get; set; } = "deepseek-v4-flash";

    public int MaxTokens { get; set; } = 3000;

    public int CacheMinutes { get; set; } = 60;
}