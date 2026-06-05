using System.ComponentModel.DataAnnotations;

namespace SmartAutoFill.Data;

/// <summary>Maps to the Documents table (Section 5 of the plan).</summary>
public class DocumentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [MaxLength(20)] public string FileType { get; set; } = string.Empty;
    [MaxLength(50)] public string DocCategory { get; set; } = string.Empty;
    [MaxLength(30)] public string Status { get; set; } = "Uploaded";
    public string RawText { get; set; } = string.Empty;
    public string? MaskedText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(255)] public string? CreatedBy { get; set; }

    public List<ExtractedFieldEntity> Fields { get; set; } = new();
}

/// <summary>Maps to the ExtractedFields table.</summary>
public class ExtractedFieldEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public DocumentEntity? Document { get; set; }

    [MaxLength(100)] public string FieldKey { get; set; } = string.Empty;
    public string? ExtractedValue { get; set; }
    public decimal Confidence { get; set; }
    public bool IsMissing { get; set; }
    public bool IsSensitive { get; set; }
    public string? UserEditedValue { get; set; }
    [MaxLength(20)] public string Source { get; set; } = "Azure";
}

/// <summary>Maps to the AuditLog table.</summary>
public class AuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    [MaxLength(100)] public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// RAG knowledge base (plan §4.5). One row per confirmed (masked snippet -> field)
/// mapping, with its embedding, used to retrieve few-shot examples for the LLM.
/// </summary>
public class MappingExampleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(50)] public string DocCategory { get; set; } = string.Empty;
    public string TextSnippet { get; set; } = string.Empty;   // masked, PII-free
    [MaxLength(100)] public string FieldKey { get; set; } = string.Empty;
    public byte[] Embedding { get; set; } = Array.Empty<byte>();
    [MaxLength(20)] public string Source { get; set; } = "Confirmed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
