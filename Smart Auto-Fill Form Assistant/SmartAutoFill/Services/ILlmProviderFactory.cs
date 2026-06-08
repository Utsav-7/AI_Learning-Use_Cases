namespace SmartAutoFill.Services;

public interface ILlmProviderFactory
{
    /// <summary>Names of all registered providers (for the UI selector).</summary>
    IReadOnlyList<string> AvailableProviders { get; }

    /// <summary>The provider selected by config (Llm:Provider), falling back to the first available.</summary>
    string DefaultProvider { get; }

    /// <summary>Resolve a provider by name; falls back to the default if not found.</summary>
    ILlmProvider Get(string name);
}
