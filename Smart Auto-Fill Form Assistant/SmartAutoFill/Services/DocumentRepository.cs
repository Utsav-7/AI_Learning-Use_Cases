using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartAutoFill.Data;
using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task<Guid> SaveAsync(ExtractionResult result, string? maskedText, string? createdBy, CancellationToken ct = default)
    {
        var doc = new DocumentEntity
        {
            FileName = result.FileName,
            FileType = Path.GetExtension(result.FileName).TrimStart('.').ToLowerInvariant(),
            DocCategory = result.Category,
            Status = "Extracted",
            RawText = result.RawText,
            MaskedText = maskedText,
            CreatedBy = createdBy,
            Fields = result.Fields.Select(f => new ExtractedFieldEntity
            {
                FieldKey = f.Key,
                ExtractedValue = f.Value,
                Confidence = (decimal)Math.Clamp(f.Confidence, 0, 1),
                IsMissing = f.IsMissing,
                IsSensitive = f.IsSensitive,
                Source = f.Source
            }).ToList()
        };

        _db.Documents.Add(doc);
        _db.AuditLogs.Add(new AuditLogEntity
        {
            DocumentId = doc.Id,
            Action = "Extracted",
            Details = $"{result.Fields.Count} fields, overall confidence {result.OverallConfidence:P0}"
        });

        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    public async Task UpdateAfterReviewAsync(Guid documentId, ExtractionResult result, CancellationToken ct = default)
    {
        var doc = await _db.Documents
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return;

        foreach (var field in result.Fields)
        {
            var entity = doc.Fields.FirstOrDefault(e => e.FieldKey == field.Key);
            if (entity is null)
            {
                entity = new ExtractedFieldEntity { DocumentId = doc.Id, FieldKey = field.Key };
                doc.Fields.Add(entity);
            }

            if (field.UserEdited)
                entity.UserEditedValue = field.Value;
            entity.ExtractedValue = field.Value;
            entity.Confidence = (decimal)Math.Clamp(field.Confidence, 0, 1);
            entity.IsMissing = field.IsMissing;
            entity.Source = field.Source;
        }

        doc.Status = "Reviewed";

        var finalJson = JsonSerializer.Serialize(
            result.Fields.Select(f => new { f.Key, f.Value, f.Confidence, f.IsMissing, f.UserEdited }));

        _db.AuditLogs.Add(new AuditLogEntity
        {
            DocumentId = doc.Id,
            Action = "Reviewed",
            Details = finalJson
        });

        await _db.SaveChangesAsync(ct);
    }
}
