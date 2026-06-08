namespace SmartAutoFill.Services;

public class LlmProviderFactory : ILlmProviderFactory
{
    private readonly Dictionary<string, ILlmProvider> _providers;

    public string DefaultProvider { get; }

    public LlmProviderFactory(IEnumerable<ILlmProvider> providers, IConfiguration config)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var configured = config["Llm:Provider"];
        DefaultProvider =
            (!string.IsNullOrWhiteSpace(configured) && _providers.ContainsKey(configured))
                ? _providers.Keys.First(k => string.Equals(k, configured, StringComparison.OrdinalIgnoreCase))
                : _providers.Keys.First();
    }

    public IReadOnlyList<string> AvailableProviders => _providers.Keys.ToList();

    public ILlmProvider Get(string name) =>
        !string.IsNullOrWhiteSpace(name) && _providers.TryGetValue(name, out var p)
            ? p
            : _providers[DefaultProvider];
}
