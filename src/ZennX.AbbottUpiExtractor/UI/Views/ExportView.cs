using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class ExportView : UserControl
    {
        private readonly DatabaseService _db;
        private ComboBox _cboExportScope = null!;
        private ComboBox _cboBatchSelect = null!;
        private RadioButton _rbExcel = null!;
        private RadioButton _rbCsv = null!;
        private Label _lblSummary = null!;

        public ExportView(DatabaseService db)
        {
            _db = db;
            InitializeComponent();
            LoadBatches();
            UpdateExportSummary();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Excel & CSV Export",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Generate clean, formula-injection protected Excel and CSV exports with Retailer ZID leading zeros preserved.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            var card = Theme.CreateCardPanel();
            card.Location = new Point(24, 85);
            card.Size = new Size(1110, 480);

            // Scope selection
            var lblScope = new Label
            {
                Text = "Select Export Scope:",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            _cboExportScope = new ComboBox
            {
                Location = new Point(24, 48),
                Size = new Size(360, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.FontBody
            };
            _cboExportScope.Items.AddRange(new object[]
            {
                "All Records",
                "Ready for Export Only (Success, Valid ZID, Reviewed)",
                "Specific Batch...",
                "Successful Extractions Only",
                "Failed Extractions Only (Audit & Follow-up)",
                "Duplicate Records Only",
                "Records Pending Review",
                "Records Missing Required ZID"
            });
            _cboExportScope.SelectedIndex = 1;
            _cboExportScope.SelectedIndexChanged += (s, e) =>
            {
                _cboBatchSelect.Visible = (_cboExportScope.SelectedIndex == 2);
                UpdateExportSummary();
            };

            _cboBatchSelect = new ComboBox
            {
                Location = new Point(400, 48),
                Size = new Size(260, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false,
                Font = Theme.FontBody
            };
            _cboBatchSelect.SelectedIndexChanged += (s, e) => UpdateExportSummary();

            // Format Selection
            var lblFormat = new Label
            {
                Text = "Export Format:",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 105),
                AutoSize = true
            };

            _rbExcel = new RadioButton
            {
                Text = "Excel Workbook (*.xlsx) - Recommended for Abbott Operations",
                Location = new Point(28, 135),
                Size = new Size(500, 24),
                Checked = true,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.ZennXBlueDark
            };

            _rbCsv = new RadioButton
            {
                Text = "Standard CSV (*.csv) - RFC 4180 Comma Separated Values",
                Location = new Point(28, 165),
                Size = new Size(500, 24),
                Font = Theme.FontBody
            };

            // Summary Label
            _lblSummary = new Label
            {
                Text = "Calculating matching records...",
                Location = new Point(24, 215),
                Size = new Size(700, 30),
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary
            };

            var featuresNotice = new Label
            {
                Text = "🛡️ Protection & Compliance Guarantees:\n" +
                       "• Retailer ZIDs with leading zeros (e.g., '00418') are saved as explicit Text cells in Excel, preventing data corruption.\n" +
                       "• Formula Injection Protection: Any cell value beginning with '=', '+', '-', or '@' is escaped to ensure safety when opened in Excel.\n" +
                       "• Statuses, audit timestamps, and source file metadata are included for audit compliance.",
                Location = new Point(24, 260),
                Size = new Size(800, 80),
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary
            };

            var btnExportNow = Theme.CreatePrimaryButton("📥 Choose Location & Export", 220, 42);
            btnExportNow.Location = new Point(24, 360);
            btnExportNow.Click += (s, e) => ExecuteExport();

            card.Controls.Add(lblScope);
            card.Controls.Add(_cboExportScope);
            card.Controls.Add(_cboBatchSelect);
            card.Controls.Add(lblFormat);
            card.Controls.Add(_rbExcel);
            card.Controls.Add(_rbCsv);
            card.Controls.Add(_lblSummary);
            card.Controls.Add(featuresNotice);
            card.Controls.Add(btnExportNow);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(card);
        }

        private void LoadBatches()
        {
            _cboBatchSelect.Items.Clear();
            var batches = _db.GetAllBatches();
            foreach (var b in batches)
            {
                _cboBatchSelect.Items.Add(b.BatchId);
            }
            if (_cboBatchSelect.Items.Count > 0)
            {
                _cboBatchSelect.SelectedIndex = 0;
            }
        }

        private List<QrRecord> GetMatchingRecords()
        {
            string scope = _cboExportScope.SelectedItem?.ToString() ?? "";
            string? batch = null;
            string? status = null;

            if (scope.StartsWith("Specific Batch") && _cboBatchSelect.SelectedItem != null)
            {
                batch = _cboBatchSelect.SelectedItem.ToString();
            }
            else if (scope.Contains("Successful"))
            {
                status = "Success";
            }
            else if (scope.Contains("Failed"))
            {
                status = "Failed";
            }
            else if (scope.Contains("Duplicate"))
            {
                status = "Duplicates";
            }
            else if (scope.Contains("Pending Review"))
            {
                status = "PendingReview";
            }
            else if (scope.Contains("Missing Required ZID"))
            {
                status = "MissingZid";
            }

            var all = _db.GetRecords(batch, status);
            if (scope.StartsWith("Ready for Export"))
            {
                return all.FindAll(r => r.IsReadyForExport);
            }

            return all;
        }

        private void UpdateExportSummary()
        {
            try
            {
                var records = GetMatchingRecords();
                _lblSummary.Text = $"Selected criteria matches: {records.Count:N0} records available for export.";
            }
            catch
            {
                _lblSummary.Text = "Unable to calculate count.";
            }
        }

        private void ExecuteExport()
        {
            var records = GetMatchingRecords();
            if (records.Count == 0)
            {
                MessageBox.Show("No records match the selected export criteria.", "Empty Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool isExcel = _rbExcel.Checked;

            using var sfd = new SaveFileDialog
            {
                Title = "Save Export File",
                Filter = isExcel ? "Excel Workbook (*.xlsx)|*.xlsx" : "CSV File (*.csv)|*.csv",
                FileName = isExcel 
                    ? $"Abbott_UPI_Export_{timestamp}.xlsx" 
                    : $"Abbott_UPI_Export_{timestamp}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (isExcel)
                    {
                        ExcelExportService.ExportRecordsToExcel(records, sfd.FileName);
                    }
                    else
                    {
                        CsvExportService.ExportRecordsToCsv(records, sfd.FileName);
                    }

                    var result = MessageBox.Show(
                        $"Successfully exported {records.Count} records to:\n{sfd.FileName}\n\nDo you want to open the destination folder?",
                        "Export Succeeded",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{sfd.FileName}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export file: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
