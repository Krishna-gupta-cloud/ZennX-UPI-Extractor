using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class ConsolidationService
    {
        private readonly DatabaseService _db;

        public ConsolidationService(DatabaseService db)
        {
            _db = db;
        }

        public (int TotalImported, int CrossPcDuplicatesFound, string ErrorMessage) ImportAndConsolidate(
            List<string> filePaths,
            string sessionId,
            Action<int, int>? progressCallback = null)
        {
            int totalImported = 0;
            int crossPcDuplicates = 0;

            try
            {
                var importedRecords = new List<ConsolidatedRecord>();

                for (int i = 0; i < filePaths.Count; i++)
                {
                    string filePath = filePaths[i];
                    string extension = Path.GetExtension(filePath).ToLowerInvariant();
                    string fileName = Path.GetFileName(filePath);

                    // Infer source PC from filename if pattern matches (e.g., Abbott_Cashback_PC1_20261017.xlsx) or default
                    string sourcePc = InferPcName(fileName);

                    List<ConsolidatedRecord> recordsInFile = extension switch
                    {
                        ".xlsx" => ReadFromExcel(filePath, fileName, sourcePc, sessionId),
                        ".csv" => ReadFromCsv(filePath, fileName, sourcePc, sessionId),
                        _ => new List<ConsolidatedRecord>()
                    };

                    importedRecords.AddRange(recordsInFile);
                    progressCallback?.Invoke(i + 1, filePaths.Count);
                }

                // Check for cross-PC duplicates and repeated imports
                // Group by UPI ID to identify cross-PC matches
                var upiCounts = new Dictionary<string, List<ConsolidatedRecord>>(StringComparer.OrdinalIgnoreCase);
                foreach (var rec in importedRecords)
                {
                    if (!string.IsNullOrWhiteSpace(rec.ExtractedUpiId))
                    {
                        if (!upiCounts.TryGetValue(rec.ExtractedUpiId, out var list))
                        {
                            list = new List<ConsolidatedRecord>();
                            upiCounts[rec.ExtractedUpiId] = list;
                        }
                        list.Add(rec);
                    }
                }

                foreach (var kvp in upiCounts)
                {
                    var group = kvp.Value;
                    if (group.Count > 1)
                    {
                        // Check if from different PCs or different source files
                        var distinctPcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var item in group) distinctPcs.Add(item.SourcePcName);

                        if (distinctPcs.Count > 1)
                        {
                            foreach (var item in group)
                            {
                                item.CrossPcDuplicateFlag = true;
                                item.ConflictDetails = $"Matching UPI detected across {distinctPcs.Count} PC files: {string.Join(", ", distinctPcs)}";
                            }
                            crossPcDuplicates += group.Count;
                        }
                    }
                }

                // Persist into consolidated table in DB
                SaveConsolidatedRecords(importedRecords);
                totalImported = importedRecords.Count;

                return (totalImported, crossPcDuplicates, string.Empty);
            }
            catch (Exception ex)
            {
                return (0, 0, $"Consolidation failed: {ex.Message}");
            }
        }

        private static string InferPcName(string fileName)
        {
            // If filename has PC-1 or Desk-2 or similar, extract it
            if (fileName.Contains("_PC-", StringComparison.OrdinalIgnoreCase) || fileName.Contains("_PC", StringComparison.OrdinalIgnoreCase))
            {
                var parts = fileName.Split('_');
                foreach (var p in parts)
                {
                    if (p.StartsWith("PC", StringComparison.OrdinalIgnoreCase))
                    {
                        return p;
                    }
                }
            }
            return "PC-" + Math.Abs(fileName.GetHashCode() % 100).ToString("D2");
        }

        private static List<ConsolidatedRecord> ReadFromExcel(string filePath, string fileName, string sourcePc, string sessionId)
        {
            var list = new List<ConsolidatedRecord>();
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed().RowsUsed();

            bool isHeader = true;
            foreach (var row in rows)
            {
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                string batchId = CleanFormulaPrefix(row.Cell(2).GetString());
                string zid = CleanFormulaPrefix(row.Cell(3).GetString());
                string sourceFile = CleanFormulaPrefix(row.Cell(5).GetString());
                string upi = CleanFormulaPrefix(row.Cell(6).GetString());
                string extStatus = row.Cell(7).GetString();
                string dupStatus = row.Cell(8).GetString();
                string revStatus = row.Cell(10).GetString();
                string dateStr = row.Cell(11).GetString();
                string notes = CleanFormulaPrefix(row.Cell(12).GetString());

                DateTime.TryParse(dateStr, out DateTime dt);

                list.Add(new ConsolidatedRecord
                {
                    ImportSessionId = sessionId,
                    SourceFile = fileName,
                    SourcePcName = sourcePc,
                    BatchId = batchId,
                    RetailerZid = zid,
                    SourceFilename = sourceFile,
                    ExtractedUpiId = upi,
                    ExtractionStatus = extStatus,
                    DuplicateStatus = dupStatus,
                    VerificationStatus = revStatus,
                    ReviewNotes = notes,
                    OriginalProcessedAt = dt != default ? dt : null
                });
            }

            return list;
        }

        private static List<ConsolidatedRecord> ReadFromCsv(string filePath, string fileName, string sourcePc, string sessionId)
        {
            var list = new List<ConsolidatedRecord>();
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                string batchId = CleanFormulaPrefix(csv.GetField("BatchId") ?? "");
                string zid = CleanFormulaPrefix(csv.GetField("RetailerZid") ?? "");
                string sourceFile = CleanFormulaPrefix(csv.GetField("SourceFilename") ?? "");
                string upi = CleanFormulaPrefix(csv.GetField("ExtractedUpiId") ?? "");
                string extStatus = csv.GetField("ExtractionStatus") ?? "";
                string dupStatus = csv.GetField("DuplicateStatus") ?? "";
                string revStatus = csv.GetField("ReviewStatus") ?? "";
                string dateStr = csv.GetField("ProcessedTimestampUtc") ?? "";
                string notes = CleanFormulaPrefix(csv.GetField("ReviewNotes") ?? "");

                DateTime.TryParse(dateStr, out DateTime dt);

                list.Add(new ConsolidatedRecord
                {
                    ImportSessionId = sessionId,
                    SourceFile = fileName,
                    SourcePcName = sourcePc,
                    BatchId = batchId,
                    RetailerZid = zid,
                    SourceFilename = sourceFile,
                    ExtractedUpiId = upi,
                    ExtractionStatus = extStatus,
                    DuplicateStatus = dupStatus,
                    VerificationStatus = revStatus,
                    ReviewNotes = notes,
                    OriginalProcessedAt = dt != default ? dt : null
                });
            }

            return list;
        }

        private static string CleanFormulaPrefix(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            if (val.StartsWith("'") || val.StartsWith("\t")) return val.Substring(1);
            return val;
        }

        private void SaveConsolidatedRecords(List<ConsolidatedRecord> records)
        {
            string dbPath = _db.DatabasePath;
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var tx = conn.BeginTransaction();

            string sql = @"
                INSERT INTO consolidated_imports (
                    import_session_id, source_file, source_pc_name, batch_id,
                    retailer_zid, source_filename, extracted_upi_id, extraction_status,
                    duplicate_status, verification_status, review_notes,
                    original_processed_at, imported_at, cross_pc_duplicate_flag, conflict_details
                ) VALUES (
                    @sessId, @sourceFile, @sourcePc, @batchId,
                    @zid, @srcFileName, @upi, @extStatus,
                    @dupStatus, @verStatus, @notes,
                    @origDate, @importedAt, @dupFlag, @conflict
                )";

            foreach (var r in records)
            {
                using var cmd = new SqliteCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@sessId", r.ImportSessionId);
                cmd.Parameters.AddWithValue("@sourceFile", r.SourceFile);
                cmd.Parameters.AddWithValue("@sourcePc", r.SourcePcName);
                cmd.Parameters.AddWithValue("@batchId", r.BatchId);
                cmd.Parameters.AddWithValue("@zid", r.RetailerZid);
                cmd.Parameters.AddWithValue("@srcFileName", r.SourceFilename);
                cmd.Parameters.AddWithValue("@upi", r.ExtractedUpiId);
                cmd.Parameters.AddWithValue("@extStatus", r.ExtractionStatus);
                cmd.Parameters.AddWithValue("@dupStatus", r.DuplicateStatus);
                cmd.Parameters.AddWithValue("@verStatus", r.VerificationStatus);
                cmd.Parameters.AddWithValue("@notes", r.ReviewNotes);
                cmd.Parameters.AddWithValue("@origDate", r.OriginalProcessedAt.HasValue ? r.OriginalProcessedAt.Value.ToString("o") : DBNull.Value);
                cmd.Parameters.AddWithValue("@importedAt", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@dupFlag", r.CrossPcDuplicateFlag ? 1 : 0);
                cmd.Parameters.AddWithValue("@conflict", r.ConflictDetails);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public List<ConsolidatedRecord> GetConsolidatedRecords(string? sessionId = null)
        {
            var list = new List<ConsolidatedRecord>();
            string dbPath = _db.DatabasePath;
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();

            string sql = "SELECT * FROM consolidated_imports ";
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                sql += " WHERE import_session_id = @sessId";
            }
            sql += " ORDER BY id DESC";

            using var cmd = new SqliteCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                cmd.Parameters.AddWithValue("@sessId", sessionId);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ConsolidatedRecord
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    ImportSessionId = reader.GetString(reader.GetOrdinal("import_session_id")),
                    SourceFile = reader.GetString(reader.GetOrdinal("source_file")),
                    SourcePcName = reader.GetString(reader.GetOrdinal("source_pc_name")),
                    BatchId = reader.GetString(reader.GetOrdinal("batch_id")),
                    RetailerZid = reader.GetString(reader.GetOrdinal("retailer_zid")),
                    SourceFilename = reader.GetString(reader.GetOrdinal("source_filename")),
                    ExtractedUpiId = reader.GetString(reader.GetOrdinal("extracted_upi_id")),
                    ExtractionStatus = reader.GetString(reader.GetOrdinal("extraction_status")),
                    DuplicateStatus = reader.GetString(reader.GetOrdinal("duplicate_status")),
                    VerificationStatus = reader.GetString(reader.GetOrdinal("verification_status")),
                    ReviewNotes = reader.GetString(reader.GetOrdinal("review_notes")),
                    CrossPcDuplicateFlag = reader.GetInt32(reader.GetOrdinal("cross_pc_duplicate_flag")) == 1,
                    ConflictDetails = reader.GetString(reader.GetOrdinal("conflict_details")),
                    OriginalProcessedAt = reader.IsDBNull(reader.GetOrdinal("original_processed_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("original_processed_at")))
                });
            }

            return list;
        }
    }
}
