using System;
using System.Drawing;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Services;
using ZennX.AbbottUpiExtractor.UI.Controls;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class DashboardView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly Action<string> _navigateAction;

        private StatCard _statTotal = null!;
        private StatCard _statSuccess = null!;
        private StatCard _statFailed = null!;
        private StatCard _statDuplicates = null!;
        private StatCard _statPendingReview = null!;
        private StatCard _statMissingZid = null!;

        public DashboardView(DatabaseService db, Action<string> navigateAction)
        {
            _db = db;
            _navigateAction = navigateAction;
            InitializeComponent();
            RefreshStats();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Operations Dashboard",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Abbott Cashback UPI QR processing metrics and status overview across local records.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // Stat Cards Layout
            var statsPanel = new FlowLayoutPanel
            {
                Location = new Point(24, 90),
                Size = new Size(1150, 100),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false
            };

            _statTotal = new StatCard("Total Images", "0", Theme.ZennXBlue);
            _statSuccess = new StatCard("Extracted UPIs", "0", Theme.StatusSuccess);
            _statFailed = new StatCard("Failed / Unread", "0", Theme.StatusDanger);
            _statDuplicates = new StatCard("Duplicates Flagged", "0", Theme.StatusWarning);
            _statPendingReview = new StatCard("Pending Review", "0", Theme.StatusInfo);
            _statMissingZid = new StatCard("Missing Required ZID", "0", Theme.StatusDanger);

            statsPanel.Controls.Add(_statTotal);
            statsPanel.Controls.Add(_statSuccess);
            statsPanel.Controls.Add(_statFailed);
            statsPanel.Controls.Add(_statDuplicates);
            statsPanel.Controls.Add(_statPendingReview);
            statsPanel.Controls.Add(_statMissingZid);

            // Quick Action Cards
            var actionsCard = Theme.CreateCardPanel();
            actionsCard.Location = new Point(24, 205);
            actionsCard.Size = new Size(1110, 180);

            var actionsTitle = new Label
            {
                Text = "Quick Actions",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var btnPaste = Theme.CreatePrimaryButton("📋 Paste QR (Ctrl+V)", 180, 42);
            btnPaste.Location = new Point(16, 52);
            btnPaste.Click += (s, e) => _navigateAction("PasteQR");

            var btnBulk = Theme.CreateOrangeButton("📁 Bulk Process QR Files", 200, 42);
            btnBulk.Location = new Point(210, 52);
            btnBulk.Click += (s, e) => _navigateAction("BulkProcess");

            var btnReview = Theme.CreateSecondaryButton("🔍 Review Results", 160, 42);
            btnReview.Location = new Point(425, 52);
            btnReview.Click += (s, e) => _navigateAction("Results");

            var btnExport = Theme.CreateSecondaryButton("📊 Export to Excel", 160, 42);
            btnExport.Location = new Point(600, 52);
            btnExport.Click += (s, e) => _navigateAction("Export");

            var btnConsolidate = Theme.CreateSecondaryButton("🔄 Consolidate PC Reports", 200, 42);
            btnConsolidate.Location = new Point(775, 52);
            btnConsolidate.Click += (s, e) => _navigateAction("Consolidate");

            var tipLabel = new Label
            {
                Text = "💡 Tip: You can copy any retailer QR code from your browser (Right-click -> Copy Image) and press Ctrl+V inside the application anytime.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(16, 116),
                AutoSize = true
            };

            actionsCard.Controls.Add(actionsTitle);
            actionsCard.Controls.Add(btnPaste);
            actionsCard.Controls.Add(btnBulk);
            actionsCard.Controls.Add(btnReview);
            actionsCard.Controls.Add(btnExport);
            actionsCard.Controls.Add(btnConsolidate);
            actionsCard.Controls.Add(tipLabel);

            // Instructions / Info Panel
            var infoCard = Theme.CreateCardPanel();
            infoCard.Location = new Point(24, 405);
            infoCard.Size = new Size(1110, 220);

            var infoTitle = new Label
            {
                Text = "Standard Operating Procedure (SOP) for Abbott Cashback Processing",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var infoBody = new Label
            {
                Text = "1. Single QR Pasting: Copy the retailer's QR image from their ZennX profile, click 'Paste QR', enter or verify the Retailer ZID, and save.\n" +
                       "2. Bulk QR Processing: If images are downloaded in folders, select multiple files or folder to batch decode hundreds of images incrementally.\n" +
                       "3. Verification: Filter by 'Pending Review' or 'Duplicates' to resolve unreadable codes or flag multiple submissions.\n" +
                       "4. Exporting: Export ready records to Excel (.xlsx) with leading zeros preserved on Retailer ZID and formula protection applied.\n" +
                       "5. Report Consolidation: Merge exports from multiple employee workstations to discover cross-PC duplicate claims.\n" +
                       "6. Local Database: Fully offline and stored in your user profile. Perform regular backups via the 'Backup & Restore' screen.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 50),
                Size = new Size(1060, 150)
            };

            infoCard.Controls.Add(infoTitle);
            infoCard.Controls.Add(infoBody);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(statsPanel);
            Controls.Add(actionsCard);
            Controls.Add(infoCard);
        }

        public void RefreshStats()
        {
            var stats = _db.GetDashboardStats();
            _statTotal.SetValue(stats.Total);
            _statSuccess.SetValue(stats.Success);
            _statFailed.SetValue(stats.Failed);
            _statDuplicates.SetValue(stats.Duplicates);
            _statPendingReview.SetValue(stats.PendingReview);
            _statMissingZid.SetValue(stats.MissingZid);
        }
    }
}
