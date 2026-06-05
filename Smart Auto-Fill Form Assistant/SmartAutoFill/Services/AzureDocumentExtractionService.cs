using System.Text.RegularExpressions;
using Azure;
using Azure.AI.DocumentIntelligence;
using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public class AzureDocumentExtractionService : IDocumentExtractionService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<AzureDocumentExtractionService> _logger;

    public AzureDocumentExtractionService(
        IConfiguration config,
        ILogger<AzureDocumentExtractionService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureDocumentIntelligence:Endpoint"];
        var apiKey = config["AzureDocumentIntelligence:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Azure Document Intelligence is not configured. Set " +
                "'AzureDocumentIntelligence:Endpoint' and ':ApiKey' in appsettings.json or user-secrets.");

        _client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<ExtractionResult> ExtractAsync(
        Stream fileStream,
        string fileName,
        string category,
        CancellationToken cancellationToken = default)
    {
        var schema = FormCatalog.GetByCategory(category);

        // Buffer the upload into memory so we can hand BinaryData to the SDK.
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, cancellationToken);
        var bytes = BinaryData.FromBytes(ms.ToArray());

        _logger.LogInformation("Analyzing {File} with model {Model}", fileName, schema.AzureModelId);

        var options = new AnalyzeDocumentOptions(schema.AzureModelId, bytes);
        Operation<AnalyzeResult> operation =
            await _client.AnalyzeDocumentAsync(WaitUntil.Completed, options, cancellationToken);
        AnalyzeResult result = operation.Value;

        var extraction = new ExtractionResult
        {
            Category = category,
            FileName = fileName,
            RawText = result.Content ?? string.Empty
        };

        if (schema.HeuristicExtraction)
            MapHeuristicFields(result, schema, extraction);
        else if (schema.Fields.Count > 0)
            MapPrebuiltFields(result, schema, extraction);
        else
            MapKeyValuePairs(result, extraction);

        return extraction;
    }

    /// <summary>
    /// For documents with no prebuilt model (e.g. resumes): OCR text is available, so we
    /// regex-prefill email/phone and leave the rest empty for the Ollama mapping layer.
    /// </summary>
    private static void MapHeuristicFields(AnalyzeResult result, FormSchema schema, ExtractionResult extraction)
    {
        var text = result.Content ?? string.Empty;

        var email = Regex.Match(text, @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b").Value;
        var phone = Regex.Match(text, @"(?:\+?\d{1,3}[\s\-]?)?(?:\d[\s\-]?){10}").Value.Trim();
        var linkedIn = Regex.Match(text, @"(?:https?:\/\/)?(?:www\.)?linkedin\.com\/[^\s]+", RegexOptions.IgnoreCase).Value;

        foreach (var def in schema.Fields)
        {
            var field = new ExtractedField
            {
                Key = def.Key,
                DisplayName = def.DisplayName,
                IsSensitive = def.IsSensitive,
                Source = "Azure"
            };

            (string? value, double confidence) = def.Key switch
            {
                "Email" when !string.IsNullOrWhiteSpace(email)       => (email, 0.90),
                "Phone" when !string.IsNullOrWhiteSpace(phone)       => (phone, 0.80),
                "LinkedIn" when !string.IsNullOrWhiteSpace(linkedIn) => (linkedIn, 0.85),
                _ => (null, 0.0)
            };

            field.Value = value;
            field.Confidence = confidence;
            extraction.Fields.Add(field);
        }
    }

    /// <summary>Map a prebuilt model's typed fields into the predefined schema (Req 4, 5, 8).</summary>
    private static void MapPrebuiltFields(AnalyzeResult result, FormSchema schema, ExtractionResult extraction)
    {
        var analyzed = result.Documents.FirstOrDefault();

        foreach (var def in schema.Fields)
        {
            var field = new ExtractedField
            {
                Key = def.Key,
                DisplayName = def.DisplayName,
                IsSensitive = def.IsSensitive,
                Source = "Azure"
            };

            if (analyzed is not null)
            {
                foreach (var azureName in def.AzureFieldNames)
                {
                    if (analyzed.Fields.TryGetValue(azureName, out var docField) &&
                        !string.IsNullOrWhiteSpace(docField.Content))
                    {
                        field.Value = docField.Content;
                        field.Confidence = docField.Confidence ?? 0;
                        break; // first matching alias wins
                    }
                }
            }

            extraction.Fields.Add(field);
        }
    }

    /// <summary>For the layout model: surface detected key-value pairs as fields.</summary>
    private static void MapKeyValuePairs(AnalyzeResult result, ExtractionResult extraction)
    {
        if (result.KeyValuePairs is null) return;

        var i = 0;
        foreach (var kvp in result.KeyValuePairs)
        {
            var key = kvp.Key?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;

            extraction.Fields.Add(new ExtractedField
            {
                Key = $"kvp_{i++}",
                DisplayName = key,
                Value = kvp.Value?.Content,
                Confidence = kvp.Confidence,
                Source = "Azure"
            });
        }
    }
}
