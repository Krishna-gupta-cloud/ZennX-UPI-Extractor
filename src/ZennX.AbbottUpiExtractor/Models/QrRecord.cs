using System;

namespace ZennX.AbbottUpiExtractor.Models
{
    public class QrRecord
    {
        public long Id { get; set; }
        public string BatchId { get; set; } = string.Empty;
        public string RetailerZid { get; set; } = string.Empty;
        public string SourceFilename { get; set; } = string.Empty;
        public string SourceType { get; set; } = "File"; // "Clipboard" or "File"
        public string ExtractedUpiId { get; set; } = string.Empty;
        public ExtractionStatus ExtractionStatus { get; set; } = ExtractionStatus.Failed;
        public DuplicateStatus DuplicateStatus { get; set; } = DuplicateStatus.Unique;
        public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
        public bool IsZidRequired { get; set; } = false;
        public string FailureReason { get; set; } = string.Empty;
        public string ReviewNotes { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastModifiedAt { get; set; }
        public string? OriginalImagePath { get; set; }

        public bool IsZidMissing => IsZidRequired && string.IsNullOrWhiteSpace(RetailerZid);

        public bool IsReadyForExport =>
            ExtractionStatus == ExtractionStatus.Success &&
            !IsZidMissing &&
            (DuplicateStatus == DuplicateStatus.Unique || ReviewStatus == ReviewStatus.Reviewed || ReviewStatus == ReviewStatus.Corrected);
    }
}
