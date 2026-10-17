using System;

namespace ZennX.AbbottUpiExtractor.Models
{
    public class BatchRecord
    {
        public string BatchId { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int MissingZidCount { get; set; }
        public bool RequireZid { get; set; }
    }

    public class AuditRecord
    {
        public long Id { get; set; }
        public long QrRecordId { get; set; }
        public string FieldChanged { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; } = string.Empty;
    }

    public class ConsolidatedRecord
    {
        public long Id { get; set; }
        public string ImportSessionId { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SourcePcName { get; set; } = string.Empty;
        public string BatchId { get; set; } = string.Empty;
        public string RetailerZid { get; set; } = string.Empty;
        public string SourceFilename { get; set; } = string.Empty;
        public string ExtractedUpiId { get; set; } = string.Empty;
        public string ExtractionStatus { get; set; } = string.Empty;
        public string DuplicateStatus { get; set; } = string.Empty;
        public string VerificationStatus { get; set; } = string.Empty;
        public string ReviewNotes { get; set; } = string.Empty;
        public DateTime? OriginalProcessedAt { get; set; }
        public bool CrossPcDuplicateFlag { get; set; }
        public string ConflictDetails { get; set; } = string.Empty;
    }
}
