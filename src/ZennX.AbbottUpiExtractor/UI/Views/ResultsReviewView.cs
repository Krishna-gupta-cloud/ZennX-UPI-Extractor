using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class ResultsReviewView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly Action<string> _navigateAction;

        private ComboBox _cboBatchFilter = null!;
        private ComboBox _cboStatusFilter = null!;
        private TextBox _txtSearch = null!;
        private DataGridView _grid = null!;

        // Detail panel controls
        private TextBox _txtDetailId = null!;
        private TextBox _txtDetailZid = null!;
        private TextBox _txtDetailUpi = null!;
        private ComboBox _cboReviewStatus = null!;
        private TextBox _txtReviewNotes = null!;
        private ListBox _lstAudit = null!;
        private Button _btnSaveDetail = null!;

        private List<QrRecord> _currentList = new();
        private QrRecord? _selectedRecord;

        public ResultsReviewView(DatabaseService db, Action<string> navigateAction)
        {
            _db = db;
            _navigateAction = navigateAction;
            InitializeComponent();
            LoadBatches();
            RefreshGrid();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Results & Manual Review",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Review extracted UPIs, edit Retailer ZIDs, resolve duplicate flags, and view audit history.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // Filter Bar Card
            var filterCard = Theme.CreateCardPanel();
            filterCard.Location = new Point(24, 85);
            filterCard.Size = new Size(1110, 75);

            var lblBatch = new Label { Text = "Batch:", Location = new Point(16, 26), AutoSize = true, Font = Theme.FontBodyBold };
            _cboBatchFilter = new ComboBox { Location = new Point(65, 22), Size = new Size(180, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboBatchFilter.SelectedIndexChanged += (s, e) => RefreshGrid();

            var lblStatus = new Label { Text = "Status Filter:", Location = new Point(265, 26), AutoSize = true, Font = Theme.FontBodyBold };
            _cboStatusFilter = new ComboBox { Location = new Point(355, 22), Size = new Size(160, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboStatusFilter.Items.AddRange(new object[] { "All Records", "Success", "Failed", "Duplicates", "PendingReview", "MissingZid" });
            _cboStatusFilter.SelectedIndex = 0;
            _cboStatusFilter.SelectedIndexChanged += (s, e) => RefreshGrid();

            var lblSearch = new Label { Text = "Search:", Location = new Point(535, 26), AutoSize = true, Font = Theme.FontBodyBold };
            _txtSearch = new TextBox { Location = new Point(590, 22), Size = new Size(200, 26) };
            _txtSearch.TextChanged += (s, e) => RefreshGrid();

            var btnRefresh = Theme.CreateSecondaryButton("🔄 Refresh", 100, 32);
            btnRefresh.Location = new Point(810, 20);
            btnRefresh.Click += (s, e) => { LoadBatches(); RefreshGrid(); };

            var btnGoExport = Theme.CreatePrimaryButton("Export Ready Records", 170, 32);
            btnGoExport.Location = new Point(925, 20);
            btnGoExport.Click += (s, e) => _navigateAction("Export");

            filterCard.Controls.Add(lblBatch);
            filterCard.Controls.Add(_cboBatchFilter);
            filterCard.Controls.Add(lblStatus);
            filterCard.Controls.Add(_cboStatusFilter);
            filterCard.Controls.Add(lblSearch);
            filterCard.Controls.Add(_txtSearch);
            filterCard.Controls.Add(btnRefresh);
            filterCard.Controls.Add(btnGoExport);

            // Left: Grid View Card
            var gridCard = Theme.CreateCardPanel();
            gridCard.Location = new Point(24, 175);
            gridCard.Size = new Size(740, 520);

            _grid = new DataGridView
            {
                Location = new Point(16, 16),
                Size = new Size(708, 488),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Theme.StyleDataGridView(_grid);
            SetupGridColumns();
            _grid.SelectionChanged += Grid_SelectionChanged;

            gridCard.Controls.Add(_grid);

            // Right: Detail & Audit Card
            var detailCard = Theme.CreateCardPanel();
            detailCard.Location = new Point(780, 175);
            detailCard.Size = new Size(354, 520);

            var detailTitle = new Label
            {
                Text = "Record Details & Review",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var lblId = new Label { Text = "Internal Record ID:", Location = new Point(16, 48), AutoSize = true, Font = Theme.FontSmall };
            _txtDetailId = new TextBox { Location = new Point(16, 68), Size = new Size(320, 24), ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249) };

            var lblZid = new Label { Text = "Retailer ZID (Preserves leading zeros):", Location = new Point(16, 100), AutoSize = true, Font = Theme.FontBodyBold };
            _txtDetailZid = new TextBox { Location = new Point(16, 122), Size = new Size(320, 26) };

            var lblUpi = new Label { Text = "Extracted UPI ID:", Location = new Point(16, 155), AutoSize = true, Font = Theme.FontBodyBold };
            _txtDetailUpi = new TextBox { Location = new Point(16, 177), Size = new Size(320, 26) };

            var lblRevStatus = new Label { Text = "Manual Verification Status:", Location = new Point(16, 210), AutoSize = true, Font = Theme.FontSmall };
            _cboReviewStatus = new ComboBox { Location = new Point(16, 230), Size = new Size(320, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboReviewStatus.Items.AddRange(Enum.GetNames(typeof(ReviewStatus)));

            var lblNotes = new Label { Text = "Review Remarks / Notes:", Location = new Point(16, 265), AutoSize = true, Font = Theme.FontSmall };
            _txtReviewNotes = new TextBox { Location = new Point(16, 285), Size = new Size(320, 50), Multiline = true };

            _btnSaveDetail = Theme.CreatePrimaryButton("💾 Save Corrections", 160, 36);
            _btnSaveDetail.Location = new Point(16, 345);
            _btnSaveDetail.Click += (s, e) => SaveDetailCorrections();

            var lblAudit = new Label { Text = "Audit Trail (Change History):", Location = new Point(16, 390), AutoSize = true, Font = Theme.FontSmall };
            _lstAudit = new ListBox { Location = new Point(16, 410), Size = new Size(320, 95), Font = Theme.FontSmall };

            detailCard.Controls.Add(detailTitle);
            detailCard.Controls.Add(lblId);
            detailCard.Controls.Add(_txtDetailId);
            detailCard.Controls.Add(lblZid);
            detailCard.Controls.Add(_txtDetailZid);
            detailCard.Controls.Add(lblUpi);
            detailCard.Controls.Add(_txtDetailUpi);
            detailCard.Controls.Add(lblRevStatus);
            detailCard.Controls.Add(_cboReviewStatus);
            detailCard.Controls.Add(lblNotes);
            detailCard.Controls.Add(_txtReviewNotes);
            detailCard.Controls.Add(_btnSaveDetail);
            detailCard.Controls.Add(lblAudit);
            detailCard.Controls.Add(_lstAudit);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(filterCard);
            Controls.Add(gridCard);
            Controls.Add(detailCard);
        }

        private void SetupGridColumns()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", Width = 55, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ZID", Width = 95, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Extracted UPI ID", Width = 220, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 95, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duplicate", Width = 90, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Review", Width = 85, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source File", Width = 160, ReadOnly = true });
        }

        private void LoadBatches()
        {
            _cboBatchFilter.Items.Clear();
            _cboBatchFilter.Items.Add("All Batches");
            var batches = _db.GetAllBatches();
            foreach (var b in batches)
            {
                _cboBatchFilter.Items.Add(b.BatchId);
            }
            _cboBatchFilter.SelectedIndex = 0;
        }

        public void RefreshGrid()
        {
            string selectedBatch = _cboBatchFilter.SelectedItem?.ToString() ?? "All Batches";
            string batchFilter = (selectedBatch == "All Batches") ? "" : selectedBatch;

            string statusFilter = _cboStatusFilter.SelectedItem?.ToString() ?? "All Records";
            if (statusFilter == "All Records") statusFilter = "";

            string search = _txtSearch.Text.Trim();

            _currentList = _db.GetRecords(batchFilter, statusFilter, search);

            _grid.Rows.Clear();
            foreach (var r in _currentList)
            {
                int rowIdx = _grid.Rows.Add(
                    r.Id,
                    r.RetailerZid,
                    r.ExtractedUpiId,
                    r.ExtractionStatus.ToString(),
                    r.DuplicateStatus != DuplicateStatus.Unique ? "DUP" : "OK",
                    r.ReviewStatus.ToString(),
                    r.SourceFilename
                );

                if (r.ExtractionStatus != ExtractionStatus.Success)
                {
                    _grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                }
                else if (r.DuplicateStatus != DuplicateStatus.Unique)
                {
                    _grid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                }
            }

            if (_currentList.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                LoadRecordDetail(_currentList[0]);
            }
            else
            {
                ClearDetail();
            }
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count > 0)
            {
                int index = _grid.SelectedRows[0].Index;
                if (index >= 0 && index < _currentList.Count)
                {
                    LoadRecordDetail(_currentList[index]);
                }
            }
        }

        private void LoadRecordDetail(QrRecord rec)
        {
            _selectedRecord = rec;
            _txtDetailId.Text = $"Record #{rec.Id} ({rec.BatchId})";
            _txtDetailZid.Text = rec.RetailerZid;
            _txtDetailUpi.Text = rec.ExtractedUpiId;
            _cboReviewStatus.SelectedItem = rec.ReviewStatus.ToString();
            _txtReviewNotes.Text = rec.ReviewNotes;

            // Load audits
            _lstAudit.Items.Clear();
            var audits = _db.GetAuditHistory(rec.Id);
            if (audits.Count == 0)
            {
                _lstAudit.Items.Add("No prior modifications.");
            }
            else
            {
                foreach (var a in audits)
                {
                    _lstAudit.Items.Add($"{a.ChangedAt:MM-dd HH:mm} [{a.FieldChanged}]: '{a.OldValue}' -> '{a.NewValue}'");
                }
            }
        }

        private void ClearDetail()
        {
            _selectedRecord = null;
            _txtDetailId.Text = string.Empty;
            _txtDetailZid.Text = string.Empty;
            _txtDetailUpi.Text = string.Empty;
            _txtReviewNotes.Text = string.Empty;
            _lstAudit.Items.Clear();
        }

        private void SaveDetailCorrections()
        {
            if (_selectedRecord == null) return;

            string newZid = _txtDetailZid.Text.Trim();
            string newUpi = _txtDetailUpi.Text.Trim();
            string newNotes = _txtReviewNotes.Text.Trim();
            var newReviewStatus = Enum.Parse<ReviewStatus>(_cboReviewStatus.SelectedItem?.ToString() ?? "Pending");

            _selectedRecord.RetailerZid = newZid;
            _selectedRecord.ExtractedUpiId = newUpi;
            _selectedRecord.ReviewNotes = newNotes;
            _selectedRecord.ReviewStatus = newReviewStatus;

            // Re-validate UPI syntax if edited
            if (!string.IsNullOrWhiteSpace(newUpi))
            {
                _selectedRecord.ExtractionStatus = QrDecoderService.IsValidUpiSyntax(newUpi) 
                    ? ExtractionStatus.Success 
                    : ExtractionStatus.Failed;
            }

            _db.UpdateQrRecord(_selectedRecord, "Manual review edit");

            MessageBox.Show("Record updated and audit trail recorded.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshGrid();
        }
    }
}
