using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class ExcelExportService
    {
        public static void ExportRecordsToExcel(IEnumerable<QrRecord> records, string targetFilePath, string sheetTitle = "Abbott_Cashback_UPI")
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetTitle);

            // Styling header
            var headerColor = XLColor.FromArgb(0, 117, 207); // ZennX Blue
            var headers = new[]
            {
                "Serial No",
                "Batch ID",
                "Retailer ZID",
                "Source Type",
                "Source Filename",
                "Extracted UPI ID",
                "Extraction Status",
                "Duplicate Status",
                "ZID Status",
                "Review Status",
                "Processed Timestamp (UTC)",
                "Review Notes",
                "Failure Reason"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = worksheet.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = headerColor;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            int serial = 1;
            foreach (var rec in records)
            {
                worksheet.Cell(row, 1).SetValue(serial++);
                worksheet.Cell(row, 2).SetValue(SanitizeForFormulaInjection(rec.BatchId));
                
                // CRITICAL: Ensure Retailer ZID is formatted as explicit TEXT to preserve leading zeros
                var zidCell = worksheet.Cell(row, 3);
                zidCell.SetValue(SanitizeForFormulaInjection(rec.RetailerZid));
                zidCell.Style.NumberFormat.Format = "@";

                worksheet.Cell(row, 4).SetValue(rec.SourceType);
                worksheet.Cell(row, 5).SetValue(SanitizeForFormulaInjection(rec.SourceFilename));
                
                var upiCell = worksheet.Cell(row, 6);
                upiCell.SetValue(SanitizeForFormulaInjection(rec.ExtractedUpiId));
                upiCell.Style.NumberFormat.Format = "@";

                worksheet.Cell(row, 7).SetValue(rec.ExtractionStatus.ToString());
                worksheet.Cell(row, 8).SetValue(rec.DuplicateStatus.ToString());
                worksheet.Cell(row, 9).SetValue(rec.IsZidMissing ? "Missing Required ZID" : "Complete");
                worksheet.Cell(row, 10).SetValue(rec.ReviewStatus.ToString());
                worksheet.Cell(row, 11).SetValue(rec.ProcessedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                worksheet.Cell(row, 12).SetValue(SanitizeForFormulaInjection(rec.ReviewNotes));
                worksheet.Cell(row, 13).SetValue(SanitizeForFormulaInjection(rec.FailureReason));

                // Colorize status rows subtly
                if (rec.ExtractionStatus != ExtractionStatus.Success)
                {
                    worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 242, 242); // Light red
                }
                else if (rec.DuplicateStatus != DuplicateStatus.Unique)
                {
                    worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199); // Light amber
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(targetFilePath);
        }

        public static void ExportConsolidatedReportToExcel(IEnumerable<ConsolidatedRecord> records, string targetFilePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Consolidated_Report");

            var headerColor = XLColor.FromArgb(0, 117, 207); // ZennX Blue
            var headers = new[]
            {
                "ID",
                "Source PC Name",
                "Source Import File",
                "Batch ID",
                "Retailer ZID",
                "Source Filename",
                "Extracted UPI ID",
                "Extraction Status",
                "Duplicate Status",
                "Cross-PC Duplicate Flag",
                "Conflict Details",
                "Original Processed Timestamp",
                "Review Notes"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = worksheet.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = headerColor;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            foreach (var rec in records)
            {
                worksheet.Cell(row, 1).SetValue(rec.Id);
                worksheet.Cell(row, 2).SetValue(SanitizeForFormulaInjection(rec.SourcePcName));
                worksheet.Cell(row, 3).SetValue(SanitizeForFormulaInjection(rec.SourceFile));
                worksheet.Cell(row, 4).SetValue(SanitizeForFormulaInjection(rec.BatchId));
                
                var zidCell = worksheet.Cell(row, 5);
                zidCell.SetValue(SanitizeForFormulaInjection(rec.RetailerZid));
                zidCell.Style.NumberFormat.Format = "@";

                worksheet.Cell(row, 6).SetValue(SanitizeForFormulaInjection(rec.SourceFilename));

                var upiCell = worksheet.Cell(row, 7);
                upiCell.SetValue(SanitizeForFormulaInjection(rec.ExtractedUpiId));
                upiCell.Style.NumberFormat.Format = "@";

                worksheet.Cell(row, 8).SetValue(rec.ExtractionStatus);
                worksheet.Cell(row, 9).SetValue(rec.DuplicateStatus);
                worksheet.Cell(row, 10).SetValue(rec.CrossPcDuplicateFlag ? "MATCH FOUND ON OTHER PC" : "No Conflict");
                worksheet.Cell(row, 11).SetValue(SanitizeForFormulaInjection(rec.ConflictDetails));
                worksheet.Cell(row, 12).SetValue(rec.OriginalProcessedAt.HasValue ? rec.OriginalProcessedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "");
                worksheet.Cell(row, 13).SetValue(SanitizeForFormulaInjection(rec.ReviewNotes));

                if (rec.CrossPcDuplicateFlag)
                {
                    worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(targetFilePath);
        }

        public static string SanitizeForFormulaInjection(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // Excel formula injection attack vectors: starts with =, +, -, @, or tab/return
            if (value.StartsWith("=") || value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("@"))
            {
                return "\t" + value;
            }
            return value;
        }
    }
}
