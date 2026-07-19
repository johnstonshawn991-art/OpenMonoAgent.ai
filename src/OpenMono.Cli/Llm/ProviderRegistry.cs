using OpenMono.Config;

namespace OpenMono.Llm;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry()
    {

        Register(new LocalLlamaProvider());
        Register(new OpenAiProvider());
        Register(new AnthropicProvider());
        Register(new OllamaProvider());
        Register(new OpenRouterProvider());
        Register(new SakanaProvider());
    }

    public void Register(IProvider provider) => _providers[provider.Name] = provider;

    public IProvider? Resolve(string name) => _providers.GetValueOrDefault(name);

    public IReadOnlyCollection<IProvider> All => _providers.Values;

    public ILlmClient CreateClient(AppConfig config)
    {

        if (config.Providers.Count > 0)
        {
            var activeProvider = config.Providers.FirstOrDefault(p => p.Value.Active);
            if (activeProvider.Value is not null)
            {
                var provider = Resolve(activeProvider.Key);
                if (provider is not null)
                {
                    var providerConfig = new ProviderConfig
                    {
                        Name = activeProvider.Key,
                        ApiKey = activeProvider.Value.ApiKey,
                        Endpoint = activeProvider.Value.Endpoint,
                        Model = activeProvider.Value.Model,
                    };
                    return provider.CreateClient(providerConfig);
                }
            }
        }

        return new OpenAiCompatClient(config.Llm) { ApiKey = config.Llm.ApiKey };
    }

    public IReadOnlyList<string> ListModels()
    {
        return _providers.Values
            .SelectMany(p => p.SupportedModels.Select(m => $"{p.Name}/{m}"))
            .ToList();
    }
}

internal sealed class LocalLlamaProvider : IProvider
{
    public string Name => "local";
    public string[] SupportedModels => ["any-gguf-model"];

    public ILlmClient CreateClient(ProviderConfig config) =>
        new OpenAiCompatClient(new LlmConfig
        {
            Endpoint = config.Endpoint ?? "http://localhost:7474",
            Model = config.Model ?? "",
        }) { ApiKey = config.ApiKey };

    public bool ValidateConfig(ProviderConfig config, out string? error)
    {
        error = null;
        return true;
    }
}

internal sealed class OpenAiProvider : IProvider
{
    public string Name => "openai";
    public string[] SupportedModels => ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "o1", "o3-mini"];

    public ILlmClient CreateClient(ProviderConfig config)
    {
        var apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return new OpenAiCompatClient(new LlmConfig
        {
            Endpoint = config.Endpoint ?? "https://api.openai.com",
            Model = config.Model ?? "gpt-4o",
        })
        { ApiKey = apiKey };
    }

    public bool ValidateConfig(ProviderConfig config, out string? error)
    {
        var key = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            error = "OpenAI API key required. Set OPENAI_API_KEY or configure in settings.";
            return false;
        }
        error = null;
        return true;
    }
}

internal sealed class OllamaProvider : IProvider
{
    public string Name => "ollama";
    public string[] SupportedModels => ["llama3", "codellama", "qwen2.5-coder", "deepseek-coder-v2"];

    public ILlmClient CreateClient(ProviderConfig config) =>
        new OpenAiCompatClient(new LlmConfig
        {
            Endpoint = config.Endpoint ?? "http://localhost:11434",
            Model = config.Model ?? "qwen2.5-coder",
        });

    public bool ValidateConfig(ProviderConfig config, out string? error)
    {
        error = null;
        return true;
    }
}

internal sealed class OpenRouterProvider : IProvider
{
    public string Name => "openrouter";
    public string[] SupportedModels =>
    [
        "openrouter/fusion",
        "anthropic/claude-opus-4",
        "openai/gpt-4o",
        "google/gemini-2.5-pro-preview",
    ];

    public ILlmClient CreateClient(ProviderConfig config)
    {
        var apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        return new OpenAiCompatClient(new LlmConfig
        {
            Endpoint = config.Endpoint ?? "https://openrouter.ai/api",
            Model = config.Model ?? "openrouter/fusion",
            MaxConcurrentRequests = 1,
        })
        {
            ApiKey = apiKey,
            ExtraHeaders = new Dictionary<string, string>
            {
                ["HTTP-Referer"] = "https://openmono.ai",
                ["X-Title"] = "OpenMono",
            },
        };
    }

    public bool ValidateConfig(ProviderConfig config, out string? error)
    {
        var key = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            error = "OpenRouter API key required. Set OPENROUTER_API_KEY or configure in settings.";
            return false;
        }
        error = null;
        return true;
    }
}

internal sealed class SakanaProvider : IProvider
{
    public string Name => "sakana";
    public string[] SupportedModels => ["fugu", "fugu-ultra-20260615"];

    public ILlmClient CreateClient(ProviderConfig config)
    {
        var apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("SAKANA_API_KEY");
        return new OpenAiCompatClient(new LlmConfig
        {
            Endpoint = config.Endpoint ?? "https://api.sakana.ai",
            Model = config.Model ?? "fugu",
            MaxConcurrentRequests = 1,
        })
        { ApiKey = apiKey };
    }

    public bool ValidateConfig(ProviderConfig config, out string? error)
    {
        var key = config.ApiKey ?? Environment.GetEnvironmentVariable("SAKANA_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            error = "Sakana API key required. Set SAKANA_API_KEY or configure in settings.";
            return false;
        }
        error = null;
        return true;
    }
}
