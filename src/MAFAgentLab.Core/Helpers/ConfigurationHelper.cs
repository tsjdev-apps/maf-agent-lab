using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Provides validated access to provider and tool configuration.</summary>
public static class ConfigurationHelper
{
    /// <summary>Returns a trimmed setting or <see langword="null"/> when it is absent.</summary>
    public static string? GetOptionalSetting(IConfiguration config, string key) =>
        string.IsNullOrWhiteSpace(config[key]) ? null : config[key]!.Trim();

    /// <summary>Returns a required setting or throws when it is absent.</summary>
    public static string GetRequiredSetting(IConfiguration config, string key) =>
        GetOptionalSetting(config, key) ?? throw new InvalidOperationException($"Missing setting: {key}");

    /// <summary>Parses and validates an absolute HTTP or HTTPS endpoint.</summary>
    public static Uri GetEndpoint(IConfiguration config, string key)
    {
        string value = GetRequiredSetting(config, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException($"{key} must be an absolute HTTP(S) URL.");
        return endpoint;
    }

    /// <summary>Returns the configuration keys that have no non-empty value.</summary>
    public static IReadOnlyList<string> GetMissingSettings(IConfiguration config, params string[] keys) =>
        keys.Where(key => GetOptionalSetting(config, key) is null).ToArray();

    /// <summary>Returns missing Azure OpenAI configuration keys.</summary>
    public static IReadOnlyList<string> GetMissingAzureOpenAISettings(IConfiguration config) =>
        GetMissingSettings(config, "AzureOpenAI:Endpoint", "AzureOpenAI:ApiKey", "AzureOpenAI:Deployment");

    /// <summary>Returns missing Ollama configuration keys.</summary>
    public static IReadOnlyList<string> GetMissingOllamaSettings(IConfiguration config) =>
        GetMissingSettings(config, "Ollama:Endpoint", "Ollama:Deployment");

    /// <summary>Returns missing OpenAI configuration keys.</summary>
    public static IReadOnlyList<string> GetMissingOpenAISettings(IConfiguration config) =>
        GetMissingSettings(config, "OpenAI:ApiKey", "OpenAI:Model");

    /// <summary>Returns missing OpenWeatherMap configuration keys.</summary>
    public static IReadOnlyList<string> GetMissingWeatherSettings(IConfiguration config) =>
        GetMissingSettings(config, "OpenWeatherMap:ApiKey");

    /// <summary>Resolves the configured provider or selects the first complete profile.</summary>
    public static string GetAgentProvider(IConfiguration config)
    {
        string? selected = GetOptionalSetting(config, "Agent:Provider");
        if (selected is "AzureOpenAI" or "Ollama" or "OpenAI") return selected;
        if (selected is not (null or "Auto")) throw new InvalidOperationException("Unknown Agent:Provider.");
        if (GetMissingAzureOpenAISettings(config).Count == 0) return "AzureOpenAI";
        if (GetMissingOllamaSettings(config).Count == 0) return "Ollama";
        if (GetMissingOpenAISettings(config).Count == 0) return "OpenAI";
        return "AzureOpenAI";
    }

    /// <summary>Returns the missing configuration for the active provider.</summary>
    public static IReadOnlyList<string> GetMissingAgentSettings(IConfiguration config) => GetAgentProvider(config) switch
    {
        "Ollama" => GetMissingOllamaSettings(config),
        "OpenAI" => GetMissingOpenAISettings(config),
        _ => GetMissingAzureOpenAISettings(config)
    };

    /// <summary>Validates that at least one comparison provider is fully configured.</summary>
    public static IReadOnlyList<string> GetMissingProviderSettings(IConfiguration config)
    {
        IReadOnlyList<string>[] providers = [GetMissingAzureOpenAISettings(config), GetMissingOllamaSettings(config), GetMissingOpenAISettings(config)];
        return providers.Any(settings => settings.Count == 0)
            ? [] : ["Configure at least one provider: AzureOpenAI, Ollama, or OpenAI (see README.md)."];
    }
}
