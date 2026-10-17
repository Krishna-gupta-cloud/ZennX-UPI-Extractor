using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class SettingsHelpView : UserControl
    {
        public SettingsHelpView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Settings & Employee Help Guide",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Application configuration, keyboard shortcuts, privacy notice, and troubleshooting instructions.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            // App Specs Card
            var specsCard = Theme.CreateCardPanel();
            specsCard.Location = new Point(24, 85);
            specsCard.Size = new Size(1110, 200);

            var specsTitle = new Label
            {
                Text = "ZennX Abbott Cashback UPI Extractor v1.0.0 — Developed by Krishna Gupta",
                Font = Theme.FontSubheading,
                ForeColor = Theme.ZennXBlueDark,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var specsBody = new Label
            {
                Text = "• Project Developer: Krishna Gupta\n" +
                       "• Notice: FOR INTERNAL OPERATIONS USE ONLY (Not for public distribution or cloud deployment)\n" +
                       "• Framework: .NET 8.0 LTS (Self-Contained Windows Desktop x64 Release)\n" +
                       "• Local Database: SQLite (Zero configuration, stored in user LocalAppData)\n" +
                       "• Administrator Privileges: Not required. Operates completely in non-elevated user space.\n" +
                       "• Offline Verification: 100% offline. Zero remote API requests, telemetry, or third-party cloud connections.\n" +
                       "• Memory Footprint: Optimized incremental image stream disposal targeting office PCs with 4GB RAM.\n" +
                       "• Formula Injection Protection: Sanitization enabled for all Excel and CSV output streams.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 48),
                Size = new Size(1060, 140)
            };

            specsCard.Controls.Add(specsTitle);
            specsCard.Controls.Add(specsBody);

            // Shortcuts Card
            var helpCard = Theme.CreateCardPanel();
            helpCard.Location = new Point(24, 300);
            helpCard.Size = new Size(1110, 360);

            var helpTitle = new Label
            {
                Text = "Operations Quick Reference & Troubleshooting",
                Font = Theme.FontSubheading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 16),
                AutoSize = true
            };

            var helpBody = new Label
            {
                Text = "Keyboard Shortcuts:\n" +
                       "  • Ctrl+V               : Instantly paste image from clipboard on the Paste QR view\n" +
                       "  • Enter                : Quick-save record when editing Retailer ZID\n" +
                       "  • F5                   : Refresh current view / statistics\n\n" +
                       "How to Copy QR Codes from ZennX Web Profiles:\n" +
                       "  1. In Chrome, Edge, or Firefox, navigate to the Retailer's Cashback profile page.\n" +
                       "  2. Right-click directly over the QR code image.\n" +
                       "  3. Select 'Copy Image' (do NOT select 'Copy image address' or 'Copy link').\n" +
                       "  4. Switch to this application and click 'Paste QR Image' or press Ctrl+V.\n\n" +
                       "Cross-PC File Exchange Workflow:\n" +
                       "  1. At the end of each shift or batch, click 'Excel & CSV Export' and export your completed records.\n" +
                       "  2. Save your export to your team's approved internal shared folder or USB transfer drive.\n" +
                       "  3. The team lead opens 'Cross-PC Report Consolidation', loads all employee files, and runs the duplicate check.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                Location = new Point(16, 48),
                Size = new Size(1060, 290)
            };

            helpCard.Controls.Add(helpTitle);
            helpCard.Controls.Add(helpBody);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(specsCard);
            Controls.Add(helpCard);
        }
    }
}
