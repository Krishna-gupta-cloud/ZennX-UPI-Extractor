using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string? customDbPath = null)
        {
            string dbPath = customDbPath ?? AppPaths.GetDatabasePath();
            DatabasePath = dbPath;
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string createTablesSql = @"
                CREATE TABLE IF NOT EXISTS batches (
                    batch_id TEXT PRIMARY KEY,
                    batch_name TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    require_zid INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS qr_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    batch_id TEXT NOT NULL,
                    retailer_zid TEXT,
                    source_filename TEXT,
                    source_type TEXT NOT NULL DEFAULT 'File',
                    extracted_upi_id TEXT,
                    extraction_status INTEGER NOT NULL,
                    duplicate_status INTEGER NOT NULL DEFAULT 0,
                    review_status INTEGER NOT NULL DEFAULT 0,
                    is_zid_required INTEGER NOT NULL DEFAULT 0,
                    failure_reason TEXT,
                    review_notes TEXT,
                    original_image_path TEXT,
                    processed_at TEXT NOT NULL,
                    last_modified_at TEXT,
                    FOREIGN KEY(batch_id) REFERENCES batches(batch_id)
                );

                CREATE INDEX IF NOT EXISTS idx_records_upi ON qr_records(extracted_upi_id);
                CREATE INDEX IF NOT EXISTS idx_records_batch ON qr_records(batch_id);
                CREATE INDEX IF NOT EXISTS idx_records_zid ON qr_records(retailer_zid);

                CREATE TABLE IF NOT EXISTS audit_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    qr_record_id INTEGER NOT NULL,
                    field_changed TEXT NOT NULL,
                    old_value TEXT,
                    new_value TEXT,
                    changed_at TEXT NOT NULL,
                    notes TEXT,
                    FOREIGN KEY(qr_record_id) REFERENCES qr_records(id)
                );

                CREATE TABLE IF NOT EXISTS consolidated_imports (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    import_session_id TEXT NOT NULL,
                    source_file TEXT NOT NULL,
                    source_pc_name TEXT,
                    batch_id TEXT,
                    retailer_zid TEXT,
                    source_filename TEXT,
                    extracted_upi_id TEXT,
                    extraction_status TEXT,
                    duplicate_status TEXT,
                    verification_status TEXT,
                    review_notes TEXT,
                    original_processed_at TEXT,
                    imported_at TEXT NOT NULL,
                    cross_pc_duplicate_flag INTEGER NOT NULL DEFAULT 0,
                    conflict_details TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_consol_upi ON consolidated_imports(extracted_upi_id);
                CREATE INDEX IF NOT EXISTS idx_consol_sess ON consolidated_imports(import_session_id);
            ";

            using var cmd = new SqliteCommand(createTablesSql, conn);
            cmd.ExecuteNonQuery();
        }

        public string DatabasePath { get; }

        public void EnsureBatchExists(string batchId)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            string sql = "INSERT OR IGNORE INTO batches (batch_id, batch_name, created_at, require_zid) VALUES (@batchId, @batchName, @createdAt, 0)";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@batchId", batchId);
            cmd.Parameters.AddWithValue("@batchName", "Batch " + batchId);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public void SaveBatch(BatchRecord batch)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                INSERT OR REPLACE INTO batches (batch_id, batch_name, created_at, require_zid)
                VALUES (@batchId, @batchName, @createdAt, @requireZid)";

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@batchId", batch.BatchId);
            cmd.Parameters.AddWithValue("@batchName", batch.BatchName);
            cmd.Parameters.AddWithValue("@createdAt", batch.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@requireZid", batch.RequireZid ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public long InsertQrRecord(QrRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.BatchId))
            {
                record.BatchId = "DEFAULT_BATCH";
            }
            EnsureBatchExists(record.BatchId);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                INSERT INTO qr_records (
                    batch_id, retailer_zid, source_filename, source_type,
                    extracted_upi_id, extraction_status, duplicate_status,
                    review_status, is_zid_required, failure_reason,
                    review_notes, original_image_path, processed_at
                ) VALUES (
                    @batchId, @zid, @filename, @sourceType,
                    @upi, @extStatus, @dupStatus,
                    @revStatus, @isZidReq, @failReason,
                    @notes, @imgPath, @processedAt
                );
                SELECT last_insert_rowid();";

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@batchId", record.BatchId);
            cmd.Parameters.AddWithValue("@zid", (object?)record.RetailerZid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@filename", (object?)record.SourceFilename ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceType", record.SourceType);
            cmd.Parameters.AddWithValue("@upi", (object?)record.ExtractedUpiId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@extStatus", (int)record.ExtractionStatus);
            cmd.Parameters.AddWithValue("@dupStatus", (int)record.DuplicateStatus);
            cmd.Parameters.AddWithValue("@revStatus", (int)record.ReviewStatus);
            cmd.Parameters.AddWithValue("@isZidReq", record.IsZidRequired ? 1 : 0);
            cmd.Parameters.AddWithValue("@failReason", (object?)record.FailureReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object?)record.ReviewNotes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@imgPath", (object?)record.OriginalImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@processedAt", record.ProcessedAt.ToString("o"));

            long newId = (long)(cmd.ExecuteScalar() ?? 0L);
            record.Id = newId;
            return newId;
        }

        public void InsertQrRecordsBulk(IEnumerable<QrRecord> records)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            string sql = @"
                INSERT INTO qr_records (
                    batch_id, retailer_zid, source_filename, source_type,
                    extracted_upi_id, extraction_status, duplicate_status,
                    review_status, is_zid_required, failure_reason,
                    review_notes, original_image_path, processed_at
                ) VALUES (
                    @batchId, @zid, @filename, @sourceType,
                    @upi, @extStatus, @dupStatus,
                    @revStatus, @isZidReq, @failReason,
                    @notes, @imgPath, @processedAt
                );";

            foreach (var record in records)
            {
                using var cmd = new SqliteCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@batchId", record.BatchId);
                cmd.Parameters.AddWithValue("@zid", (object?)record.RetailerZid ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@filename", (object?)record.SourceFilename ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sourceType", record.SourceType);
                cmd.Parameters.AddWithValue("@upi", (object?)record.ExtractedUpiId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@extStatus", (int)record.ExtractionStatus);
                cmd.Parameters.AddWithValue("@dupStatus", (int)record.DuplicateStatus);
                cmd.Parameters.AddWithValue("@revStatus", (int)record.ReviewStatus);
                cmd.Parameters.AddWithValue("@isZidReq", record.IsZidRequired ? 1 : 0);
                cmd.Parameters.AddWithValue("@failReason", (object?)record.FailureReason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@notes", (object?)record.ReviewNotes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@imgPath", (object?)record.OriginalImagePath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@processedAt", record.ProcessedAt.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public void UpdateQrRecord(QrRecord record, string auditNote = "")
        {
            var old = GetRecordById(record.Id);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            string sql = @"
                UPDATE qr_records SET
                    retailer_zid = @zid,
                    extracted_upi_id = @upi,
                    extraction_status = @extStatus,
                    duplicate_status = @dupStatus,
                    review_status = @revStatus,
                    review_notes = @notes,
                    last_modified_at = @modifiedAt
                WHERE id = @id";

            using (var cmd = new SqliteCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", record.Id);
                cmd.Parameters.AddWithValue("@zid", (object?)record.RetailerZid ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@upi", (object?)record.ExtractedUpiId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@extStatus", (int)record.ExtractionStatus);
                cmd.Parameters.AddWithValue("@dupStatus", (int)record.DuplicateStatus);
                cmd.Parameters.AddWithValue("@revStatus", (int)record.ReviewStatus);
                cmd.Parameters.AddWithValue("@notes", (object?)record.ReviewNotes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modifiedAt", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            // Log audits for changed fields
            if (old != null)
            {
                if (old.RetailerZid != record.RetailerZid)
                {
                    InsertAudit(conn, tx, record.Id, "RetailerZid", old.RetailerZid, record.RetailerZid, auditNote);
                }
                if (old.ExtractedUpiId != record.ExtractedUpiId)
                {
                    InsertAudit(conn, tx, record.Id, "ExtractedUpiId", old.ExtractedUpiId, record.ExtractedUpiId, auditNote);
                }
                if (old.ReviewStatus != record.ReviewStatus)
                {
                    InsertAudit(conn, tx, record.Id, "ReviewStatus", old.ReviewStatus.ToString(), record.ReviewStatus.ToString(), auditNote);
                }
            }

            tx.Commit();
        }

        private void InsertAudit(SqliteConnection conn, SqliteTransaction tx, long recordId, string field, string oldVal, string newVal, string note)
        {
            string auditSql = @"
                INSERT INTO audit_history (qr_record_id, field_changed, old_value, new_value, changed_at, notes)
                VALUES (@recordId, @field, @oldVal, @newVal, @changedAt, @notes)";
            using var cmd = new SqliteCommand(auditSql, conn, tx);
            cmd.Parameters.AddWithValue("@recordId", recordId);
            cmd.Parameters.AddWithValue("@field", field);
            cmd.Parameters.AddWithValue("@oldVal", oldVal ?? "");
            cmd.Parameters.AddWithValue("@newVal", newVal ?? "");
            cmd.Parameters.AddWithValue("@changedAt", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@notes", note ?? "");
            cmd.ExecuteNonQuery();
        }

        public QrRecord? GetRecordById(long id)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = "SELECT * FROM qr_records WHERE id = @id";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadRecord(reader);
            }
            return null;
        }

        public List<QrRecord> GetRecords(string? batchId = null, string? filterStatus = null, string? searchQuery = null)
        {
            var list = new List<QrRecord>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = "SELECT * FROM qr_records WHERE 1=1 ";
            if (!string.IsNullOrWhiteSpace(batchId) && batchId != "All")
            {
                sql += " AND batch_id = @batchId ";
            }
            if (!string.IsNullOrWhiteSpace(filterStatus))
            {
                if (filterStatus == "Success") sql += " AND extraction_status = 0 ";
                else if (filterStatus == "Failed") sql += " AND extraction_status != 0 ";
                else if (filterStatus == "Duplicates") sql += " AND duplicate_status != 0 ";
                else if (filterStatus == "PendingReview") sql += " AND review_status = 0 ";
                else if (filterStatus == "MissingZid") sql += " AND is_zid_required = 1 AND (retailer_zid IS NULL OR retailer_zid = '') ";
            }
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                sql += " AND (extracted_upi_id LIKE @query OR retailer_zid LIKE @query OR source_filename LIKE @query) ";
            }
            sql += " ORDER BY id DESC";

            using var cmd = new SqliteCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(batchId) && batchId != "All")
            {
                cmd.Parameters.AddWithValue("@batchId", batchId);
            }
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                cmd.Parameters.AddWithValue("@query", $"%{searchQuery.Trim()}%");
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadRecord(reader));
            }

            return list;
        }

        public List<AuditRecord> GetAuditHistory(long recordId)
        {
            var audits = new List<AuditRecord>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = "SELECT * FROM audit_history WHERE qr_record_id = @id ORDER BY id DESC";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", recordId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                audits.Add(new AuditRecord
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    QrRecordId = reader.GetInt64(reader.GetOrdinal("qr_record_id")),
                    FieldChanged = reader.GetString(reader.GetOrdinal("field_changed")),
                    OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? "" : reader.GetString(reader.GetOrdinal("old_value")),
                    NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? "" : reader.GetString(reader.GetOrdinal("new_value")),
                    ChangedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("changed_at"))),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? "" : reader.GetString(reader.GetOrdinal("notes"))
                });
            }

            return audits;
        }

        public bool CheckIfUpiExistsHistorically(string upiId, string? excludeBatchId = null)
        {
            if (string.IsNullOrWhiteSpace(upiId)) return false;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = "SELECT COUNT(1) FROM qr_records WHERE LOWER(extracted_upi_id) = LOWER(@upi) ";
            if (!string.IsNullOrWhiteSpace(excludeBatchId))
            {
                sql += " AND batch_id != @batchId";
            }

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@upi", upiId.Trim());
            if (!string.IsNullOrWhiteSpace(excludeBatchId))
            {
                cmd.Parameters.AddWithValue("@batchId", excludeBatchId);
            }

            long count = (long)(cmd.ExecuteScalar() ?? 0L);
            return count > 0;
        }

        public List<BatchRecord> GetAllBatches()
        {
            var batches = new List<BatchRecord>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                SELECT b.batch_id, b.batch_name, b.created_at, b.require_zid,
                       COUNT(r.id) as total_count,
                       SUM(CASE WHEN r.extraction_status = 0 THEN 1 ELSE 0 END) as success_count,
                       SUM(CASE WHEN r.extraction_status != 0 THEN 1 ELSE 0 END) as failed_count,
                       SUM(CASE WHEN r.duplicate_status != 0 THEN 1 ELSE 0 END) as duplicate_count,
                       SUM(CASE WHEN r.is_zid_required = 1 AND (r.retailer_zid IS NULL OR r.retailer_zid = '') THEN 1 ELSE 0 END) as missing_zid_count
                FROM batches b
                LEFT JOIN qr_records r ON b.batch_id = r.batch_id
                GROUP BY b.batch_id
                ORDER BY b.created_at DESC";

            using var cmd = new SqliteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                batches.Add(new BatchRecord
                {
                    BatchId = reader.GetString(0),
                    BatchName = reader.GetString(1),
                    CreatedAt = DateTime.Parse(reader.GetString(2)),
                    RequireZid = reader.GetInt32(3) == 1,
                    TotalCount = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetInt64(4)),
                    SuccessCount = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetInt64(5)),
                    FailedCount = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetInt64(6)),
                    DuplicateCount = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetInt64(7)),
                    MissingZidCount = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetInt64(8))
                });
            }

            return batches;
        }

        public (int Total, int Success, int Failed, int Duplicates, int PendingReview, int MissingZid) GetDashboardStats()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                SELECT
                    COUNT(1) as total,
                    SUM(CASE WHEN extraction_status = 0 THEN 1 ELSE 0 END) as success,
                    SUM(CASE WHEN extraction_status != 0 THEN 1 ELSE 0 END) as failed,
                    SUM(CASE WHEN duplicate_status != 0 THEN 1 ELSE 0 END) as duplicates,
                    SUM(CASE WHEN review_status = 0 THEN 1 ELSE 0 END) as pending_review,
                    SUM(CASE WHEN is_zid_required = 1 AND (retailer_zid IS NULL OR retailer_zid = '') THEN 1 ELSE 0 END) as missing_zid
                FROM qr_records";

            using var cmd = new SqliteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int total = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0));
                int success = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1));
                int failed = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2));
                int duplicates = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3));
                int pendingReview = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetInt64(4));
                int missingZid = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetInt64(5));
                return (total, success, failed, duplicates, pendingReview, missingZid);
            }

            return (0, 0, 0, 0, 0, 0);
        }

        private static QrRecord ReadRecord(SqliteDataReader reader)
        {
            return new QrRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                BatchId = reader.GetString(reader.GetOrdinal("batch_id")),
                RetailerZid = reader.IsDBNull(reader.GetOrdinal("retailer_zid")) ? "" : reader.GetString(reader.GetOrdinal("retailer_zid")),
                SourceFilename = reader.IsDBNull(reader.GetOrdinal("source_filename")) ? "" : reader.GetString(reader.GetOrdinal("source_filename")),
                SourceType = reader.GetString(reader.GetOrdinal("source_type")),
                ExtractedUpiId = reader.IsDBNull(reader.GetOrdinal("extracted_upi_id")) ? "" : reader.GetString(reader.GetOrdinal("extracted_upi_id")),
                ExtractionStatus = (ExtractionStatus)reader.GetInt32(reader.GetOrdinal("extraction_status")),
                DuplicateStatus = (DuplicateStatus)reader.GetInt32(reader.GetOrdinal("duplicate_status")),
                ReviewStatus = (ReviewStatus)reader.GetInt32(reader.GetOrdinal("review_status")),
                IsZidRequired = reader.GetInt32(reader.GetOrdinal("is_zid_required")) == 1,
                FailureReason = reader.IsDBNull(reader.GetOrdinal("failure_reason")) ? "" : reader.GetString(reader.GetOrdinal("failure_reason")),
                ReviewNotes = reader.IsDBNull(reader.GetOrdinal("review_notes")) ? "" : reader.GetString(reader.GetOrdinal("review_notes")),
                OriginalImagePath = reader.IsDBNull(reader.GetOrdinal("original_image_path")) ? null : reader.GetString(reader.GetOrdinal("original_image_path")),
                ProcessedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("processed_at"))),
                LastModifiedAt = reader.IsDBNull(reader.GetOrdinal("last_modified_at")) ? null : (DateTime?)DateTime.Parse(reader.GetString(reader.GetOrdinal("last_modified_at")))
            };
        }
    }
}
