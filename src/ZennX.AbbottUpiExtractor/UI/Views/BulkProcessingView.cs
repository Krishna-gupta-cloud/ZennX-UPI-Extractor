using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class BulkProcessingView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly QrDecoderService _decoder;
        private readonly Action<string> _navigateAction;

        private ProgressBar _progressBar = null!;
        private Label _lblProgressStatus = null!;
        private Label _lblCounters = null!;
        private Button _btnSelectFiles = null!;
        private Button _btnSelectFolder = null!;
        private Button _btnStart = null!;
        private Button _btnCancel = null!;
        private CheckBox _chkRequireZid = null!;
        private DataGridView _grid = null!;

        private readonly List<string> _selectedFiles = new();
        private readonly List<QrRecord> _processedBatch = new();
        private CancellationTokenSource? _cts;
        private bool _isProcessing = false;

        public BulkProcessingView(DatabaseService db, QrDecoderService decoder, Action<string> navigateAction)
        {
            _db = db;
            _decoder = decoder;
            _navigateAction = navigateAction;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Bulk QR Image Processing",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Batch process hundreds of QR code images incrementally without holding large images in RAM.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // Top Control Card
            var controlCard = Theme.CreateCardPanel();
            controlCard.Location = new Point(24, 85);
            controlCard.Size = new Size(1110, 150);

            _btnSelectFiles = Theme.CreateSecondaryButton("📄 Select Files...", 140, 38);
            _btnSelectFiles.Location = new Point(16, 16);
            _btnSelectFiles.Click += (s, e) => SelectFiles();

            _btnSelectFolder = Theme.CreateSecondaryButton("📁 Select Folder...", 140, 38);
            _btnSelectFolder.Location = new Point(165, 16);
            _btnSelectFolder.Click += (s, e) => SelectFolder();

            _chkRequireZid = new CheckBox
            {
                Text = "Require Retailer ZID for this batch",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(325, 22),
                AutoSize = true
            };

            _btnStart = Theme.CreatePrimaryButton("▶ Start Processing", 150, 38);
            _btnStart.Location = new Point(620, 16);
            _btnStart.Click += async (s, e) => await StartProcessingAsync();

            _btnCancel = Theme.CreateSecondaryButton("⏹ Cancel", 100, 38);
            _btnCancel.Location = new Point(780, 16);
            _btnCancel.Enabled = false;
            _btnCancel.Click += (s, e) => CancelProcessing();

            var btnClearList = Theme.CreateSecondaryButton("Clear", 90, 38);
            btnClearList.Location = new Point(890, 16);
            btnClearList.Click += (s, e) => ClearBatch();

            _progressBar = new ProgressBar
            {
                Location = new Point(16, 68),
                Size = new Size(1075, 22),
                Style = ProgressBarStyle.Continuous
            };

            _lblProgressStatus = new Label
            {
                Text = "No files selected. Click 'Select Files' or 'Select Folder' to load QR images.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(16, 102),
                Size = new Size(450, 24)
            };

            _lblCounters = new Label
            {
                Text = "Selected: 0 | Completed: 0 | Success: 0 | Failed: 0 | Duplicates: 0",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                Location = new Point(480, 102),
                Size = new Size(610, 24),
                TextAlign = ContentAlignment.TopRight
            };

            controlCard.Controls.Add(_btnSelectFiles);
            controlCard.Controls.Add(_btnSelectFolder);
            controlCard.Controls.Add(_chkRequireZid);
            controlCard.Controls.Add(_btnStart);
            controlCard.Controls.Add(_btnCancel);
            controlCard.Controls.Add(btnClearList);
            controlCard.Controls.Add(_progressBar);
            controlCard.Controls.Add(_lblProgressStatus);
            controlCard.Controls.Add(_lblCounters);

            // Table Card
            var tableCard = Theme.CreateCardPanel();
            tableCard.Location = new Point(24, 250);
            tableCard.Size = new Size(1110, 420);

            var tableTitle = new Label
            {
                Text = "Batch Processing Results (Editable Retailer ZID)",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 12),
                AutoSize = true
            };

            _grid = new DataGridView
            {
                Location = new Point(16, 45),
                Size = new Size(1075, 355),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Theme.StyleDataGridView(_grid);

            SetupGridColumns();

            tableCard.Controls.Add(tableTitle);
            tableCard.Controls.Add(_grid);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(controlCard);
            Controls.Add(tableCard);
        }

        private void SetupGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Width = 50, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Filename", Width = 230, ReadOnly = true });
            
            // Editable Retailer ZID
            var colZid = new DataGridViewTextBoxColumn
            {
                HeaderText = "Retailer ZID (Edit)",
                Width = 150,
                ReadOnly = false
            };
            _grid.Columns.Add(colZid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Extracted UPI ID", Width = 250, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 130, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duplicate", Width = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Failure / Review Reason", Width = 280, ReadOnly = true });

            _grid.CellValueChanged += Grid_CellValueChanged;
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2 && e.RowIndex < _processedBatch.Count)
            {
                var record = _processedBatch[e.RowIndex];
                string newZid = _grid.Rows[e.RowIndex].Cells[2].Value?.ToString()?.Trim() ?? string.Empty;
                if (record.RetailerZid != newZid)
                {
                    record.RetailerZid = newZid;
                    if (record.Id > 0)
                    {
                        _db.UpdateQrRecord(record, "ZID edited in bulk view");
                    }
                }
            }
        }

        private void SelectFiles()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Retailer QR Images",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (var file in ofd.FileNames)
                {
                    if (!_selectedFiles.Contains(file))
                    {
                        _selectedFiles.Add(file);
                    }
                }
                UpdateCounters(0, 0, 0, 0);
                _lblProgressStatus.Text = $"{_selectedFiles.Count} files selected and ready for processing.";
            }
        }

        private void SelectFolder()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Folder Containing QR Images"
            };

            if (fbd.ShowDialog() == DialogResult.OK && Directory.Exists(fbd.SelectedPath))
            {
                var files = Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.TopDirectoryOnly);
                int added = 0;
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp")
                    {
                        if (!_selectedFiles.Contains(file))
                        {
                            _selectedFiles.Add(file);
                            added++;
                        }
                    }
                }
                UpdateCounters(0, 0, 0, 0);
                _lblProgressStatus.Text = $"Added {added} images from folder. Total selected: {_selectedFiles.Count}";
            }
        }

        private async Task StartProcessingAsync()
        {
            if (_selectedFiles.Count == 0)
            {
                MessageBox.Show("Please select files or a folder first.", "No Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isProcessing = true;
            _btnStart.Enabled = false;
            _btnSelectFiles.Enabled = false;
            _btnSelectFolder.Enabled = false;
            _btnCancel.Enabled = true;

            _progressBar.Value = 0;
            _progressBar.Maximum = _selectedFiles.Count;

            _processedBatch.Clear();
            _grid.Rows.Clear();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            string batchId = "BATCH_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            bool requireZid = _chkRequireZid.Checked;

            _db.SaveBatch(new BatchRecord
            {
                BatchId = batchId,
                BatchName = $"Batch of {_selectedFiles.Count} images",
                CreatedAt = DateTime.UtcNow,
                RequireZid = requireZid
            });

            int completed = 0;
            int success = 0;
            int failed = 0;
            int duplicates = 0;

            var inBatchUpiSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var sw = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                for (int i = 0; i < _selectedFiles.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    string filePath = _selectedFiles[i];
                    string fileName = Path.GetFileName(filePath);

                    // Decode incrementally (memory released after decoding each image)
                    var decodeResult = _decoder.DecodeFile(filePath);

                    var extStatus = decodeResult.Status;
                    string upi = decodeResult.UpiId;
                    var dupStatus = DuplicateStatus.Unique;

                    if (extStatus == ExtractionStatus.Success && !string.IsNullOrWhiteSpace(upi))
                    {
                        // Check duplicate within batch
                        if (inBatchUpiSet.Contains(upi))
                        {
                            dupStatus = DuplicateStatus.DuplicateInBatch;
                            duplicates++;
                        }
                        else
                        {
                            inBatchUpiSet.Add(upi);

                            // Check duplicate historically
                            if (_db.CheckIfUpiExistsHistorically(upi))
                            {
                                dupStatus = DuplicateStatus.DuplicateHistorical;
                                duplicates++;
                            }
                        }
                        success++;
                    }
                    else
                    {
                        failed++;
                    }

                    completed++;

                    var record = new QrRecord
                    {
                        BatchId = batchId,
                        RetailerZid = string.Empty, // Left for manual entry
                        SourceFilename = fileName,
                        SourceType = "File",
                        ExtractedUpiId = upi,
                        ExtractionStatus = extStatus,
                        DuplicateStatus = dupStatus,
                        ReviewStatus = (extStatus == ExtractionStatus.Success && dupStatus == DuplicateStatus.Unique && !requireZid) 
                            ? ReviewStatus.Reviewed 
                            : ReviewStatus.Pending,
                        IsZidRequired = requireZid,
                        FailureReason = decodeResult.ErrorMessage,
                        ProcessedAt = DateTime.UtcNow
                    };

                    _processedBatch.Add(record);

                    // Report progress to UI thread
                    Invoke(new Action(() =>
                    {
                        _progressBar.Value = completed;
                        UpdateCounters(completed, success, failed, duplicates);
                        _lblProgressStatus.Text = $"Processing: {fileName} ({completed}/{_selectedFiles.Count})";

                        int rowIndex = _grid.Rows.Add(
                            completed,
                            fileName,
                            "",
                            upi,
                            extStatus.ToString(),
                            dupStatus != DuplicateStatus.Unique ? "DUPLICATE" : "Unique",
                            decodeResult.ErrorMessage
                        );

                        if (extStatus != ExtractionStatus.Success)
                        {
                            _grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                        }
                        else if (dupStatus != DuplicateStatus.Unique)
                        {
                            _grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                        }
                    }));
                }
            }, token);

            sw.Stop();

            // Persist batch to DB
            if (_processedBatch.Count > 0)
            {
                _db.InsertQrRecordsBulk(_processedBatch);
            }

            _isProcessing = false;
            _btnStart.Enabled = true;
            _btnSelectFiles.Enabled = true;
            _btnSelectFolder.Enabled = true;
            _btnCancel.Enabled = false;

            if (token.IsCancellationRequested)
            {
                _lblProgressStatus.Text = $"Processing cancelled by user. Saved {completed} processed records in {sw.ElapsedMilliseconds} ms.";
            }
            else
            {
                _lblProgressStatus.Text = $"Batch complete! Processed {completed} images in {sw.Elapsed.TotalSeconds:F1}s (Avg {sw.ElapsedMilliseconds / Math.Max(1, completed)} ms/image).";
            }
        }

        private void CancelProcessing()
        {
            if (_isProcessing && _cts != null)
            {
                _cts.Cancel();
                _lblProgressStatus.Text = "Cancelling... please wait for the current image to finish.";
            }
        }

        private void ClearBatch()
        {
            if (_isProcessing) return;
            _selectedFiles.Clear();
            _processedBatch.Clear();
            _grid.Rows.Clear();
            _progressBar.Value = 0;
            UpdateCounters(0, 0, 0, 0);
            _lblProgressStatus.Text = "Batch cleared.";
        }

        private void UpdateCounters(int completed, int success, int failed, int duplicates)
        {
            _lblCounters.Text = $"Selected: {_selectedFiles.Count} | Completed: {completed} | Success: {success} | Failed: {failed} | Duplicates: {duplicates}";
        }
    }
}
