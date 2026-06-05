using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public interface IDocumentExtractionService
{
    /// <summary>
    /// Runs OCR + field extraction (Req 2, 3, 4, 8) on an uploaded document
    /// using the Azure prebuilt model for the given category, then maps the
    /// results into the predefined form schema.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(
        Stream fileStream,
        string fileName,
        string category,
        CancellationToken cancellationToken = default);
}
