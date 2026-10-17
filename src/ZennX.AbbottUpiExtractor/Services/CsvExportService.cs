using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class CsvExportService
    {
        public static void ExportRecordsToCsv(IEnumerable<QrRecord> records, string targetFilePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Encoding = Encoding.UTF8,
                ShouldQuote = args => true
            };

            using var writer = new StreamWriter(targetFilePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, config);

            // Write header
            csv.WriteField("SerialNo");
            csv.WriteField("BatchId");
            csv.WriteField("RetailerZid");
            csv.WriteField("SourceType");
            csv.WriteField("SourceFilename");
            csv.WriteField("ExtractedUpiId");
            csv.WriteField("ExtractionStatus");
            csv.WriteField("DuplicateStatus");
            csv.WriteField("ZidStatus");
            csv.WriteField("ReviewStatus");
            csv.WriteField("ProcessedTimestampUtc");
            csv.WriteField("ReviewNotes");
            csv.WriteField("FailureReason");
            csv.NextRecord();

            int serial = 1;
            foreach (var rec in records)
            {
                csv.WriteField(serial++);
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.BatchId));
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.RetailerZid));
                csv.WriteField(rec.SourceType);
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.SourceFilename));
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.ExtractedUpiId));
                csv.WriteField(rec.ExtractionStatus.ToString());
                csv.WriteField(rec.DuplicateStatus.ToString());
                csv.WriteField(rec.IsZidMissing ? "Missing Required ZID" : "Complete");
                csv.WriteField(rec.ReviewStatus.ToString());
                csv.WriteField(rec.ProcessedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.ReviewNotes));
                csv.WriteField(ExcelExportService.SanitizeForFormulaInjection(rec.FailureReason));
                csv.NextRecord();
            }
        }
    }
}
