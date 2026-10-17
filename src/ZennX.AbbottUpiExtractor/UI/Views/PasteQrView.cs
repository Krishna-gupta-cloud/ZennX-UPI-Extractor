using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Models;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class PasteQrView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly QrDecoderService _decoder;
        private readonly Action<string> _navigateAction;

        private PictureBox _picPreview = null!;
        private TextBox _txtZid = null!;
        private TextBox _txtExtractedUpi = null!;
        private Label _lblStatusBadge = null!;
        private Label _lblStatusDetails = null!;
        private CheckBox _chkRequireZid = null!;
        private TextBox _txtNotes = null!;

        private Bitmap? _currentBitmap;
        private ExtractionStatus _currentExtStatus = ExtractionStatus.Failed;
        private DuplicateStatus _currentDupStatus = DuplicateStatus.Unique;
        private string _currentFailureReason = string.Empty;

        public PasteQrView(DatabaseService db, QrDecoderService decoder, Action<string> navigateAction)
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
                Text = "Paste QR & Extract UPI ID",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Copy a QR image from retailer profile or application clipboard (Ctrl+V) to decode UPI instantly.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // Main container card
            var card = Theme.CreateCardPanel();
            card.Location = new Point(24, 85);
            card.Size = new Size(1110, 560);

            // Left Section: Image Preview
            var lblPreviewHeader = new Label
            {
                Text = "Image Preview",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(20, 16),
                AutoSize = true
            };

            _picPreview = new PictureBox
            {
                Location = new Point(20, 48),
                Size = new Size(340, 340),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var btnPasteAction = Theme.CreatePrimaryButton("📋 Paste QR Image (Ctrl+V)", 210, 40);
            btnPasteAction.Location = new Point(20, 400);
            btnPasteAction.Click += (s, e) => PasteFromClipboard();

            var btnBrowseFallback = Theme.CreateSecondaryButton("📂 Choose File...", 120, 40);
            btnBrowseFallback.Location = new Point(240, 400);
            btnBrowseFallback.Click += (s, e) => BrowseImageFallback();

            var btnClear = Theme.CreateSecondaryButton("Clear", 90, 40);
            btnClear.Location = new Point(20, 450);
            btnClear.Click += (s, e) => ResetFields();

            card.Controls.Add(lblPreviewHeader);
            card.Controls.Add(_picPreview);
            card.Controls.Add(btnPasteAction);
            card.Controls.Add(btnBrowseFallback);
            card.Controls.Add(btnClear);

            // Right Section: Retailer ZID and Extracted Details
            int rightX = 400;

            var lblDetailsHeader = new Label
            {
                Text = "Retailer & UPI Record Details",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(rightX, 16),
                AutoSize = true
            };

            // Retailer ZID Field
            var lblZid = new Label
            {
                Text = "Retailer ZID (Preserves leading zeros, e.g. 008124):",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                Location = new Point(rightX, 50),
                AutoSize = true
            };

            _txtZid = new TextBox
            {
                Location = new Point(rightX, 74),
                Size = new Size(320, 28),
                Font = Theme.FontBody
            };

            _chkRequireZid = new CheckBox
            {
                Text = "Require ZID for this record / batch",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(rightX + 340, 74),
                AutoSize = true,
                Checked = false
            };

            // Extracted UPI ID Field
            var lblUpi = new Label
            {
                Text = "Extracted UPI ID (Payee Address):",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                Location = new Point(rightX, 115),
                AutoSize = true
            };

            _txtExtractedUpi = new TextBox
            {
                Location = new Point(rightX, 139),
                Size = new Size(500, 28),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Theme.ZennXBlueDark
            };

            // Status Badge & Details
            _lblStatusBadge = new Label
            {
                Text = "Awaiting Image",
                Location = new Point(rightX, 180),
                Size = new Size(180, 28),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.FontBodyBold
            };

            _lblStatusDetails = new Label
            {
                Text = "No image loaded yet. Paste an image from your clipboard.",
                Location = new Point(rightX + 190, 185),
                Size = new Size(500, 40),
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary
            };

            // Notes field
            var lblNotes = new Label
            {
                Text = "Review Notes / Audit Remarks (Optional):",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(rightX, 230),
                AutoSize = true
            };

            _txtNotes = new TextBox
            {
                Location = new Point(rightX, 254),
                Size = new Size(500, 50),
                Multiline = true,
                Font = Theme.FontBody
            };

            // Save Buttons
            var btnSaveRecord = Theme.CreatePrimaryButton("💾 Save Record", 160, 42);
            btnSaveRecord.Location = new Point(rightX, 325);
            btnSaveRecord.Click += (s, e) => SaveRecord(pasteNext: false);

            var btnSaveAndNext = Theme.CreateOrangeButton("💾 Save & Paste Next", 180, 42);
            btnSaveAndNext.Location = new Point(rightX + 175, 325);
            btnSaveAndNext.Click += (s, e) => SaveRecord(pasteNext: true);

            var infoBanner = new Label
            {
                Text = "ℹ️ Note: Saving another image will NEVER overwrite a previously saved record. Each paste creates a new uniquely tracked database record.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                Location = new Point(rightX, 385),
                Size = new Size(600, 36)
            };

            card.Controls.Add(lblDetailsHeader);
            card.Controls.Add(lblZid);
            card.Controls.Add(_txtZid);
            card.Controls.Add(_chkRequireZid);
            card.Controls.Add(lblUpi);
            card.Controls.Add(_txtExtractedUpi);
            card.Controls.Add(_lblStatusBadge);
            card.Controls.Add(_lblStatusDetails);
            card.Controls.Add(lblNotes);
            card.Controls.Add(_txtNotes);
            card.Controls.Add(btnSaveRecord);
            card.Controls.Add(btnSaveAndNext);
            card.Controls.Add(infoBanner);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(card);
        }

        public void PasteFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        LoadAndDecodeBitmap(new Bitmap(img));
                        return;
                    }
                }

                // Check if file drop list in clipboard (e.g., copied image file from file explorer)
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files != null && files.Count > 0)
                    {
                        string path = files[0]!;
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
                        {
                            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            LoadAndDecodeBitmap(new Bitmap(fs));
                            return;
                        }
                    }
                }

                MessageBox.Show(
                    "Clipboard does not contain image data.\n\n" +
                    "If you are copying from a web browser, make sure to Right-Click the QR code and select 'Copy Image'.\n" +
                    "If your browser only provides a link or HTML, save the image file to disk and use 'Choose File'.",
                    "No Image in Clipboard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to paste image from clipboard: {ex.Message}", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BrowseImageFallback()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Retailer QR Code Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    LoadAndDecodeBitmap(new Bitmap(fs));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to read image file: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadAndDecodeBitmap(Bitmap bmp)
        {
            _currentBitmap?.Dispose();
            _currentBitmap = bmp;
            _picPreview.Image = _currentBitmap;

            // Run decoding locally
            var decodeResult = _decoder.DecodeBitmap(bmp);
            _currentExtStatus = decodeResult.Status;
            _txtExtractedUpi.Text = decodeResult.UpiId;
            _currentFailureReason = decodeResult.ErrorMessage;

            // Check Duplicate Status
            if (decodeResult.Status == ExtractionStatus.Success && !string.IsNullOrWhiteSpace(decodeResult.UpiId))
            {
                bool existsHistorically = _db.CheckIfUpiExistsHistorically(decodeResult.UpiId);
                _currentDupStatus = existsHistorically ? DuplicateStatus.DuplicateHistorical : DuplicateStatus.Unique;
            }
            else
            {
                _currentDupStatus = DuplicateStatus.Unique;
            }

            UpdateStatusUI();
            _txtZid.Focus();
        }

        private void UpdateStatusUI()
        {
            if (_currentExtStatus == ExtractionStatus.Success)
            {
                if (_currentDupStatus != DuplicateStatus.Unique)
                {
                    _lblStatusBadge.Text = "DUPLICATE FOUND";
                    _lblStatusBadge.BackColor = Theme.StatusWarning;
                    _lblStatusBadge.ForeColor = Color.Black;
                    _lblStatusDetails.Text = "Extracted successfully, but UPI ID already exists in the local database!";
                }
                else
                {
                    _lblStatusBadge.Text = "EXTRACTION SUCCESS";
                    _lblStatusBadge.BackColor = Theme.StatusSuccess;
                    _lblStatusBadge.ForeColor = Color.White;
                    _lblStatusDetails.Text = "Valid UPI ID successfully extracted and verified by format rules.";
                }
            }
            else if (_currentExtStatus == ExtractionStatus.UnsupportedFormat)
            {
                _lblStatusBadge.Text = "UNSUPPORTED QR";
                _lblStatusBadge.BackColor = Theme.StatusWarning;
                _lblStatusBadge.ForeColor = Color.Black;
                _lblStatusDetails.Text = _currentFailureReason;
            }
            else
            {
                _lblStatusBadge.Text = "EXTRACTION FAILED";
                _lblStatusBadge.BackColor = Theme.StatusDanger;
                _lblStatusBadge.ForeColor = Color.White;
                _lblStatusDetails.Text = string.IsNullOrEmpty(_currentFailureReason) ? "Could not read or detect QR code." : _currentFailureReason;
            }
        }

        private void SaveRecord(bool pasteNext)
        {
            if (_currentBitmap == null)
            {
                MessageBox.Show("Please paste or select a QR image first.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string zid = _txtZid.Text.Trim();
            bool requireZid = _chkRequireZid.Checked;

            if (requireZid && string.IsNullOrWhiteSpace(zid))
            {
                var promptResult = MessageBox.Show(
                    "Require ZID is enabled for this batch, but Retailer ZID is empty.\n\nDo you want to save this record as 'Pending Review' anyway?",
                    "Missing Retailer ZID",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (promptResult != DialogResult.Yes)
                {
                    _txtZid.Focus();
                    return;
                }
            }

            // Create batch record if default clipboard batch does not exist
            string batchId = "CLIPBOARD_" + DateTime.Now.ToString("yyyyMMdd");
            _db.SaveBatch(new BatchRecord
            {
                BatchId = batchId,
                BatchName = "Clipboard Pasted Records (" + DateTime.Now.ToString("dd MMM yyyy") + ")",
                CreatedAt = DateTime.UtcNow,
                RequireZid = requireZid
            });

            // If manual correction was entered in the UPI box:
            string finalUpi = _txtExtractedUpi.Text.Trim();
            ReviewStatus reviewStatus = ReviewStatus.Pending;

            if (_currentExtStatus == ExtractionStatus.Success && _currentDupStatus == DuplicateStatus.Unique && (!requireZid || !string.IsNullOrWhiteSpace(zid)))
            {
                reviewStatus = ReviewStatus.Reviewed;
            }

            var record = new QrRecord
            {
                BatchId = batchId,
                RetailerZid = zid,
                SourceFilename = "Clipboard_" + DateTime.Now.ToString("HHmmss"),
                SourceType = "Clipboard",
                ExtractedUpiId = finalUpi,
                ExtractionStatus = _currentExtStatus,
                DuplicateStatus = _currentDupStatus,
                ReviewStatus = reviewStatus,
                IsZidRequired = requireZid,
                FailureReason = _currentFailureReason,
                ReviewNotes = _txtNotes.Text.Trim(),
                ProcessedAt = DateTime.UtcNow
            };

            _db.InsertQrRecord(record);

            MessageBox.Show($"Record saved successfully with ID #{record.Id}!", "Record Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

            ResetFields();

            if (pasteNext)
            {
                PasteFromClipboard();
            }
        }

        private void ResetFields()
        {
            _currentBitmap?.Dispose();
            _currentBitmap = null;
            _picPreview.Image = null;
            _txtZid.Text = string.Empty;
            _txtExtractedUpi.Text = string.Empty;
            _txtNotes.Text = string.Empty;
            _currentExtStatus = ExtractionStatus.Failed;
            _currentDupStatus = DuplicateStatus.Unique;
            _currentFailureReason = string.Empty;

            _lblStatusBadge.Text = "Awaiting Image";
            _lblStatusBadge.BackColor = Color.FromArgb(226, 232, 240);
            _lblStatusBadge.ForeColor = Theme.TextSecondary;
            _lblStatusDetails.Text = "No image loaded yet. Paste an image from your clipboard.";
        }
    }
}
