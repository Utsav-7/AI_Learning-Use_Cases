using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartAutoFill.Data;
using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public class RagService : IRagService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<RagService> _logger;

    public RagService(AppDbContext db, IEmbeddingService embeddings, ILogger<RagService> logger)
    {
        _db = db;
        _embeddings = embeddings;
        _logger = logger;
    }

    public async Task<string> GetExamplesBlockAsync(string category, string maskedText, int k = 4, CancellationToken ct = default)
    {
        var query = await _embeddings.EmbedAsync(maskedText, ct);
        if (query.Length == 0) return string.Empty; // embeddings unavailable

        // Filter by category first (plan gotcha), then cosine-rank in C# (SQL Express has no VECTOR type).
        var candidates = await _db.MappingExamples
            .Where(e => e.DocCategory == category)
            .ToListAsync(ct);
        if (candidates.Count == 0) return string.Empty;

        var top = candidates
            .Select(e => (e, score: IEmbeddingService.Cosine(query, IEmbeddingService.FromBytes(e.Embedding))))
            .OrderByDescending(x => x.score)
            .Take(k)
            .Where(x => x.score > 0.3) // ignore weak matches
            .ToList();
        if (top.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Here are examples of CORRECT mappings from past documents (same category):");
        foreach (var (e, _) in top)
            sb.AppendLine($"- Text: \"{e.TextSnippet}\"  ->  field \"{e.FieldKey}\"");
        return sb.ToString();
    }

    public async Task SaveExamplesAsync(string category, IEnumerable<ExtractedField> fields, IMaskingService masking, CancellationToken ct = default)
    {
        foreach (var f in fields)
        {
            if (string.IsNullOrWhiteSpace(f.Value)) continue;

            // Embed/store the MASKED snippet only — never real PII in the KB.
            var snippet = masking.Mask($"{f.DisplayName}: {f.Value}").MaskedText;
            var vec = await _embeddings.EmbedAsync(snippet, ct);
            if (vec.Length == 0) return; // embeddings unavailable — skip the whole batch

            _db.MappingExamples.Add(new MappingExampleEntity
            {
                DocCategory = category,
                TextSnippet = snippet,
                FieldKey = f.Key,
                Embedding = IEmbeddingService.ToBytes(vec),
                Source = f.UserEdited ? "User" : "Confirmed"
            });
        }

        try { await _db.SaveChangesAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not save RAG examples."); }
    }
}
