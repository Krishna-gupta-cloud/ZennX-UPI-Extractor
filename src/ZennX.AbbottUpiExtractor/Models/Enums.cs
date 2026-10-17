using System;

namespace ZennX.AbbottUpiExtractor.Models
{
    public enum ExtractionStatus
    {
        Success,
        Failed,
        UnsupportedFormat
    }

    public enum DuplicateStatus
    {
        Unique,
        DuplicateInBatch,
        DuplicateHistorical
    }

    public enum ReviewStatus
    {
        Pending,
        Reviewed,
        Corrected
    }
}
