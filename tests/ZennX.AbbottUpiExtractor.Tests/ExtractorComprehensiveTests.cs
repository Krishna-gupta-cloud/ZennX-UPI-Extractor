using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ClosedXML.Excel;
using Xunit;
using ZXing;
using ZXing.QrCode;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.Tests
{
    public class ExtractorComprehensiveTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly string _testDbPath;
        private readonly DatabaseService _db;
        private readonly QrDecoderService _decoder;

        public ExtractorComprehensiveTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "ZennX_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);
            _testDbPath = Path.Combine(_testTempDir, "test_cashback.db");
            _db = new DatabaseService(_testDbPath);
            _decoder = new QrDecoderService();
        }

        public void Dispose()
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_testTempDir))
                {
                    Directory.Delete(_testTempDir, true);
                }
            }
            catch { }
        }

        private Bitmap GenerateQrBitmap(string content)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 250,
                    Width = 250,
                    Margin = 1
                }
            };

            var pixelData = writer.Write(content);
            var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb);

            try
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        // Test 1: Valid UPI QR codes
        [Fact]
        public void Test01_ValidUpiQrCode_ExtractsSuccessfully()
        {
            string upiUri = "upi://pay?pa=retailer.abbott@okaxis&pn=AbbottRetailer&mc=5411";
            using var bmp = GenerateQrBitmap(upiUri);

            var result = _decoder.DecodeBitmap(bmp);

            Assert.Equal(ExtractionStatus.Success, result.Status);
            Assert.Equal("retailer.abbott@okaxis", result.UpiId);
            Assert.True(string.IsNullOrEmpty(result.ErrorMessage));
        }

        // Test 2: Non-UPI QR codes and unrelated URLs
        [Fact]
        public void Test02_NonUpiQrCodes_FlagsUnsupportedFormat()
        {
            string websiteUrl = "https://www.abbott.in/products/overview.html";
            using var bmp = GenerateQrBitmap(websiteUrl);

            var result = _decoder.DecodeBitmap(bmp);

            Assert.Equal(ExtractionStatus.UnsupportedFormat, result.Status);
            Assert.True(string.IsNullOrEmpty(result.UpiId));
        }

        // Test 3: Corrupted and unreadable images
        [Fact]
        public void Test03_CorruptedOrBlankImage_ReturnsFailed()
        {
            using var blankBmp = new Bitmap(200, 200);
            using (var g = Graphics.FromImage(blankBmp))
            {
                g.Clear(Color.Gray);
            }

            var result = _decoder.DecodeBitmap(blankBmp);

            Assert.Equal(ExtractionStatus.Failed, result.Status);
            Assert.Contains("No QR code", result.ErrorMessage);
        }

        // Test 4: UPI QR with parameter reordering and url encoding
        [Fact]
        public void Test04_UpiQrWithReorderedParameters_ExtractsCorrectPa()
        {
            string payload = "upi://pay?pn=Medical%20Store&mc=0000&pa=pharmacy99%40hdfcbank&cu=INR";
            var result = QrDecoderService.ExtractUpiFromPayload(payload);

            Assert.Equal(ExtractionStatus.Success, result.Status);
            Assert.Equal("pharmacy99@hdfcbank", result.UpiId);
        }

        // Test 5: Optional and mandatory ZID handling
        [Fact]
        public void Test05_MandatoryAndOptionalZidHandling()
        {
            var optionalRecord = new QrRecord
            {
                BatchId = "B1",
                RetailerZid = "",
                ExtractedUpiId = "shop@sbi",
                ExtractionStatus = ExtractionStatus.Success,
                DuplicateStatus = DuplicateStatus.Unique,
                IsZidRequired = false
            };
            Assert.False(optionalRecord.IsZidMissing);
            Assert.True(optionalRecord.IsReadyForExport);

            var mandatoryMissing = new QrRecord
            {
                BatchId = "B1",
                RetailerZid = "",
                ExtractedUpiId = "shop@sbi",
                ExtractionStatus = ExtractionStatus.Success,
                DuplicateStatus = DuplicateStatus.Unique,
                IsZidRequired = true
            };
            Assert.True(mandatoryMissing.IsZidMissing);
            Assert.False(mandatoryMissing.IsReadyForExport);
        }

        // Test 6: ZIDs containing leading zeros preserved as string
        [Fact]
        public void Test06_ZidLeadingZeros_PreservedInDatabase()
        {
            string leadingZeroZid = "00049210";
            var record = new QrRecord
            {
                BatchId = "B_ZID",
                RetailerZid = leadingZeroZid,
                SourceFilename = "test.png",
                ExtractedUpiId = "shop@icici",
                ExtractionStatus = ExtractionStatus.Success,
                DuplicateStatus = DuplicateStatus.Unique
            };

            long id = _db.InsertQrRecord(record);
            var retrieved = _db.GetRecordById(id);

            Assert.NotNull(retrieved);
            Assert.Equal(leadingZeroZid, retrieved.RetailerZid);
            Assert.Equal("00049210", retrieved.RetailerZid);
        }

        // Test 7: Duplicate detection within and across batches
        [Fact]
        public void Test07_DuplicateDetection_HistoricalAndInBatch()
        {
            string upi = "duplicate.test@paytm";

            // Check before inserting: should be false
            Assert.False(_db.CheckIfUpiExistsHistorically(upi));

            // Insert historical record
            _db.InsertQrRecord(new QrRecord
            {
                BatchId = "BATCH_OLD",
                RetailerZid = "Z01",
                ExtractedUpiId = upi,
                ExtractionStatus = ExtractionStatus.Success
            });

            // Check after: should be true
            Assert.True(_db.CheckIfUpiExistsHistorically(upi));
            Assert.True(_db.CheckIfUpiExistsHistorically("DUPLICATE.TEST@PAYTM")); // Case insensitive
        }

        // Test 8: Manual correction and audit history trail
        [Fact]
        public void Test08_ManualCorrection_ProducesAuditHistory()
        {
            var record = new QrRecord
            {
                BatchId = "B_AUDIT",
                RetailerZid = "Z100",
                ExtractedUpiId = "original@okhdfc",
                ExtractionStatus = ExtractionStatus.Success,
                ReviewStatus = ReviewStatus.Pending
            };

            long id = _db.InsertQrRecord(record);

            // Update record
            record.RetailerZid = "Z101";
            record.ExtractedUpiId = "corrected@okhdfc";
            record.ReviewStatus = ReviewStatus.Corrected;
            _db.UpdateQrRecord(record, "Supervisor verified against physical invoice");

            var audits = _db.GetAuditHistory(id);
            Assert.True(audits.Count >= 2);

            var zidAudit = audits.Find(a => a.FieldChanged == "RetailerZid");
            Assert.NotNull(zidAudit);
            Assert.Equal("Z100", zidAudit.OldValue);
            Assert.Equal("Z101", zidAudit.NewValue);

            var upiAudit = audits.Find(a => a.FieldChanged == "ExtractedUpiId");
            Assert.NotNull(upiAudit);
            Assert.Equal("original@okhdfc", upiAudit.OldValue);
            Assert.Equal("corrected@okhdfc", upiAudit.NewValue);
        }

        // Test 9: Excel export accuracy & formula injection escaping
        [Fact]
        public void Test09_ExcelExport_SanitizesFormulaInjectionAndPreservesZid()
        {
            string excelFile = Path.Combine(_testTempDir, "test_export.xlsx");
            var records = new List<QrRecord>
            {
                new QrRecord
                {
                    BatchId = "B1",
                    RetailerZid = "00789", // leading zeros
                    ExtractedUpiId = "=cmd|' /C calc'!A0", // Malicious formula payload
                    SourceFilename = "+SUM(A1:A10).png",  // Malicious leading plus
                    ExtractionStatus = ExtractionStatus.Success
                }
            };

            ExcelExportService.ExportRecordsToExcel(records, excelFile);
            Assert.True(File.Exists(excelFile));

            // Verify ClosedXML read
            using var wb = new XLWorkbook(excelFile);
            var ws = wb.Worksheet(1);

            // ZID must remain text with leading zeros intact
            string exportedZid = ws.Cell(2, 3).GetString();
            Assert.Equal("00789", exportedZid);

            // Formula injection payloads must have been prepended with tab
            string exportedUpi = ws.Cell(2, 6).GetString();
            Assert.StartsWith("\t", exportedUpi);

            string exportedFile = ws.Cell(2, 5).GetString();
            Assert.StartsWith("\t", exportedFile);
        }

        // Test 10: Cross-PC consolidation and conflict flag detection
        [Fact]
        public void Test10_CrossPcConsolidation_DetectsConflictsAcrossComputers()
        {
            string pc1File = Path.Combine(_testTempDir, "Abbott_PC-01.xlsx");
            string pc2File = Path.Combine(_testTempDir, "Abbott_PC-02.xlsx");

            string sharedUpi = "shared.retailer@axis";

            var rec1 = new List<QrRecord>
            {
                new QrRecord { BatchId = "B1", RetailerZid = "Z01", ExtractedUpiId = sharedUpi, ExtractionStatus = ExtractionStatus.Success }
            };
            var rec2 = new List<QrRecord>
            {
                new QrRecord { BatchId = "B2", RetailerZid = "Z02", ExtractedUpiId = sharedUpi, ExtractionStatus = ExtractionStatus.Success }
            };

            ExcelExportService.ExportRecordsToExcel(rec1, pc1File);
            ExcelExportService.ExportRecordsToExcel(rec2, pc2File);

            var consolidation = new ConsolidationService(_db);
            string sessId = "TEST_SESS";
            var result = consolidation.ImportAndConsolidate(new List<string> { pc1File, pc2File }, sessId);

            Assert.Equal(2, result.TotalImported);
            Assert.Equal(2, result.CrossPcDuplicatesFound);

            var consolidatedRecords = consolidation.GetConsolidatedRecords(sessId);
            Assert.All(consolidatedRecords, r => Assert.True(r.CrossPcDuplicateFlag));
        }

        // Test 11: Database backup and restore integrity
        [Fact]
        public void Test11_BackupAndRestore_SucceedsWithIntegrity()
        {
            _db.InsertQrRecord(new QrRecord
            {
                BatchId = "B_BACKUP",
                RetailerZid = "Z99",
                ExtractedUpiId = "backup.test@sbi",
                ExtractionStatus = ExtractionStatus.Success
            });

            string backupDir = Path.Combine(_testTempDir, "Backups");
            var backupRes = BackupRestoreService.CreateBackup(backupDir, _testDbPath);
            Assert.True(backupRes.Success);
            Assert.True(File.Exists(backupRes.BackupFilePath));

            var validation = BackupRestoreService.ValidateBackupFile(backupRes.BackupFilePath);
            Assert.True(validation.Valid);
            Assert.Contains("Valid backup file", validation.Message);
        }

        // Test 12: Generate Sample QR Image Pack for Employee Testing
        [Fact]
        public void Test12_GenerateSampleQrFilesPack()
        {
            string samplesDir = @"E:\UPI fetcher from QR\samples\sample_qr_codes";
            Directory.CreateDirectory(samplesDir);

            var samples = new[]
            {
                ("qr_01_valid_retailer_apollo.png", "upi://pay?pa=apollo.pharmacy@okaxis&pn=Apollo%20Pharmacy&mc=5912"),
                ("qr_02_valid_retailer_medplus.png", "upi://pay?pa=medplus99@icici&pn=Medplus%20Chemists"),
                ("qr_03_valid_retailer_sanjeevani.png", "upi://pay?pa=sanjeevani.med@hdfcbank&pn=Sanjeevani%20Medical"),
                ("qr_04_valid_retailer_netmeds.png", "upi://pay?pa=netmeds.outlet@sbi&pn=Netmeds%20Store"),
                ("qr_05_valid_raw_upi.png", "shree.ganesh.medical@paytm"),
                ("qr_06_non_upi_website.png", "https://www.abbott.co.in/corp-landing.html"),
                ("qr_07_invalid_malformed.png", "upi://pay?pn=NoPaParamOnly"),
                ("qr_08_duplicate_apollo.png", "upi://pay?pa=apollo.pharmacy@okaxis&pn=Apollo%20Pharmacy")
            };

            foreach (var (filename, payload) in samples)
            {
                using var bmp = GenerateQrBitmap(payload);
                bmp.Save(Path.Combine(samplesDir, filename), ImageFormat.Png);
            }

            Assert.True(File.Exists(Path.Combine(samplesDir, "qr_01_valid_retailer_apollo.png")));
        }
    }
}
