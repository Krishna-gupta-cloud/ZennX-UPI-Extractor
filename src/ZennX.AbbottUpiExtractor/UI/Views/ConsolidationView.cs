using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class ConsolidationView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly ConsolidationService _consolidationService;

        private ListBox _lstFiles = null!;
        private Label _lblSummary = null!;
        private DataGridView _grid = null!;
        private readonly List<string> _fileList = new();
        private List<ConsolidatedRecord> _currentConsolidatedList = new();

        public ConsolidationView(DatabaseService db)
        {
            _db = db;
            _consolidationService = new ConsolidationService(db);
            InitializeComponent();
            RefreshConsolidatedGrid();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Cross-PC Report Consolidation",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Merge exported Excel or CSV files from multiple employee workstations into a unified master report to detect cross-PC duplicate claims.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // File selection card
            var fileCard = Theme.CreateCardPanel();
            fileCard.Location = new Point(24, 85);
            fileCard.Size = new Size(1110, 200);

            var fileTitle = new Label
            {
                Text = "Select Export Files from Other PCs:",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 14),
                AutoSize = true
            };

            _lstFiles = new ListBox
            {
                Location = new Point(16, 42),
                Size = new Size(800, 100),
                Font = Theme.FontSmall
            };

            var btnAddFiles = Theme.CreateSecondaryButton("➕ Add Files...", 120, 36);
            btnAddFiles.Location = new Point(830, 42);
            btnAddFiles.Click += (s, e) => AddFiles();

            var btnClearFiles = Theme.CreateSecondaryButton("Clear List", 120, 36);
            btnClearFiles.Location = new Point(830, 85);
            btnClearFiles.Click += (s, e) => { _fileList.Clear(); _lstFiles.Items.Clear(); };

            var btnImport = Theme.CreateOrangeButton("🔄 Merge & Check Duplicates", 230, 38);
            btnImport.Location = new Point(16, 150);
            btnImport.Click += (s, e) => RunConsolidation();

            _lblSummary = new Label
            {
                Text = "Ready to merge. Add files exported from colleagues' computers.",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextSecondary,
                Location = new Point(260, 160),
                Size = new Size(800, 26)
            };

            fileCard.Controls.Add(fileTitle);
            fileCard.Controls.Add(_lstFiles);
            fileCard.Controls.Add(btnAddFiles);
            fileCard.Controls.Add(btnClearFiles);
            fileCard.Controls.Add(btnImport);
            fileCard.Controls.Add(_lblSummary);

            // Consolidated Results Card
            var gridCard = Theme.CreateCardPanel();
            gridCard.Location = new Point(24, 300);
            gridCard.Size = new Size(1110, 390);

            var gridTitle = new Label
            {
                Text = "Consolidated Master Records & Cross-Workstation Duplicate Alerts",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 14),
                AutoSize = true
            };

            var btnExportMaster = Theme.CreatePrimaryButton("📊 Export Consolidated Report to Excel", 270, 34);
            btnExportMaster.Location = new Point(810, 10);
            btnExportMaster.Click += (s, e) => ExportConsolidatedToExcel();

            _grid = new DataGridView
            {
                Location = new Point(16, 52),
                Size = new Size(1075, 320),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Theme.StyleDataGridView(_grid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source PC", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Imported File", Width = 190 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Retailer ZID", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Extracted UPI ID", Width = 230 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cross-PC Alert", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Conflict Details", Width = 230 });

            gridCard.Controls.Add(gridTitle);
            gridCard.Controls.Add(btnExportMaster);
            gridCard.Controls.Add(_grid);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(fileCard);
            Controls.Add(gridCard);
        }

        private void AddFiles()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Export Files (*.xlsx, *.csv)",
                Filter = "Export Files (*.xlsx;*.csv)|*.xlsx;*.csv|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (var f in ofd.FileNames)
                {
                    if (!_fileList.Contains(f))
                    {
                        _fileList.Add(f);
                        _lstFiles.Items.Add(Path.GetFileName(f) + " (" + f + ")");
                    }
                }
            }
        }

        private void RunConsolidation()
        {
            if (_fileList.Count == 0)
            {
                MessageBox.Show("Please add at least one export file to consolidate.", "No Files Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string sessionId = "SESS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var result = _consolidationService.ImportAndConsolidate(_fileList, sessionId);
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                MessageBox.Show(result.ErrorMessage, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _lblSummary.Text = $"Consolidation finished: {result.TotalImported} records imported. Found {result.CrossPcDuplicatesFound} records with duplicate UPI IDs across multiple PCs!";
            RefreshConsolidatedGrid();
        }

        private void RefreshConsolidatedGrid()
        {
            _currentConsolidatedList = _consolidationService.GetConsolidatedRecords();
            _grid.Rows.Clear();

            int idx = 1;
            foreach (var r in _currentConsolidatedList)
            {
                int row = _grid.Rows.Add(
                    idx++,
                    r.SourcePcName,
                    r.SourceFile,
                    r.RetailerZid,
                    r.ExtractedUpiId,
                    r.CrossPcDuplicateFlag ? "⚠️ DUPLICATE ON OTHER PC" : "Unique",
                    r.ConflictDetails
                );

                if (r.CrossPcDuplicateFlag)
                {
                    _grid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                }
            }
        }

        private void ExportConsolidatedToExcel()
        {
            if (_currentConsolidatedList.Count == 0)
            {
                MessageBox.Show("No consolidated records available to export.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            using var sfd = new SaveFileDialog
            {
                Title = "Save Consolidated Master Report",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"Abbott_Consolidated_Master_{timestamp}.xlsx"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExcelExportService.ExportConsolidatedReportToExcel(_currentConsolidatedList, sfd.FileName);
                    MessageBox.Show($"Master consolidation report successfully saved to:\n{sfd.FileName}", "Export Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
