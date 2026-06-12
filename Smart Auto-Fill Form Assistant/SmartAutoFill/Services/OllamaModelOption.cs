namespace SmartAutoFill.Services;

/// <summary>One entry under appsettings "Ollama:Models" — a dropdown label + Ollama model id.</summary>
public class OllamaModelOption
{
    public string Name { get; set; } = "Ollama";
    public string Model { get; set; } = "llama3.1:8b";
}
