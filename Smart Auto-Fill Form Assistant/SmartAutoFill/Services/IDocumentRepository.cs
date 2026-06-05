using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public interface IDocumentRepository
{
    /// <summary>Persist an extraction result (+ masked text) and return the new document id.</summary>
    Task<Guid> SaveAsync(ExtractionResult result, string? maskedText, string? createdBy, CancellationToken ct = default);

    /// <summary>Update field values + status after user review/submit, and write an audit entry.</summary>
    Task UpdateAfterReviewAsync(Guid documentId, ExtractionResult result, CancellationToken ct = default);
}
