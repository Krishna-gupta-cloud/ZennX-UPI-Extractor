using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Services;
using ZennX.AbbottUpiExtractor.UI.Views;

namespace ZennX.AbbottUpiExtractor.UI
{
    public class MainForm : Form
    {
        private readonly DatabaseService _db;
        private readonly QrDecoderService _decoder;

        private Panel _sidebarPanel = null!;
        private Panel _contentPanel = null!;
        private readonly Dictionary<string, UserControl> _views = new();
        private readonly Dictionary<string, Button> _navButtons = new();
        private string _activeViewKey = "Dashboard";

        public MainForm()
        {
            _db = new DatabaseService();
            _decoder = new QrDecoderService();

            InitializeLayout();
            InitializeViews();
            NavigateTo("Dashboard");
        }

        private void InitializeLayout()
        {
            Text = "ZennX Abbott Cashback UPI Extractor";
            Size = new Size(1360, 860);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BgApp;
            KeyPreview = true;

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
            }
            catch { }

            KeyDown += MainForm_KeyDown;

            // 1. Sidebar Panel (Left docked, fixed width 250px)
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Theme.BgDarkSidebar
            };

            // 1.A Brand Header
            var brandPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(15, 20, 31)
            };

            var brandTitle = new Label
            {
                Text = "ZennX",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Theme.ZennXOrange,
                Location = new Point(18, 12),
                AutoSize = true
            };

            var brandSubtitle = new Label
            {
                Text = "Abbott Cashback Extractor",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(20, 42),
                AutoSize = true
            };

            brandPanel.Controls.Add(brandTitle);
            brandPanel.Controls.Add(brandSubtitle);

            // 1.B Sidebar Footer (Docked Bottom inside sidebar)
            var sidebarFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 75,
                BackColor = Color.FromArgb(15, 20, 31),
                Padding = new Padding(10, 8, 10, 8)
            };

            var lblDeveloper = new Label
            {
                Text = "Developed by Krishna Gupta",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Theme.ZennXOrange,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20
            };

            var lblInternalUse = new Label
            {
                Text = "🔒 Internal Operations Use Only",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 20
            };

            var lblOfflineNotice = new Label
            {
                Text = "🟢 100% Offline (Local SQLite)",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 18
            };

            sidebarFooter.Controls.Add(lblOfflineNotice);
            sidebarFooter.Controls.Add(lblInternalUse);
            sidebarFooter.Controls.Add(lblDeveloper);

            // 1.C Nav Buttons Panel (Docked Fill in sidebar)
            var navContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8, 10, 8, 10)
            };

            AddNavButton(navContainer, "Dashboard", "📊  Dashboard");
            AddNavButton(navContainer, "PasteQR", "📋  Paste QR (Ctrl+V)");
            AddNavButton(navContainer, "BulkProcess", "📁  Bulk Processing");
            AddNavButton(navContainer, "Results", "🔍  Results & Review");
            AddNavButton(navContainer, "BatchHistory", "🗂️  Batch History");
            AddNavButton(navContainer, "Export", "📤  Excel & CSV Export");
            AddNavButton(navContainer, "Consolidate", "🔄  Cross-PC Reports");
            AddNavButton(navContainer, "BackupRestore", "💾  Backup & Restore");
            AddNavButton(navContainer, "SettingsHelp", "⚙️  Settings & Help");

            _sidebarPanel.Controls.Add(navContainer);
            _sidebarPanel.Controls.Add(brandPanel);
            _sidebarPanel.Controls.Add(sidebarFooter);

            // 2. Main Content Panel (Docked Fill)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgApp
            };

            // CRITICAL WinForms Docking Order:
            // Controls added last to Form.Controls get highest priority.
            // Adding _contentPanel FIRST, then _sidebarPanel ensures _contentPanel fills remaining space properly.
            Controls.Add(_contentPanel);
            Controls.Add(_sidebarPanel);
        }

        private void AddNavButton(Panel parent, string key, string label)
        {
            int top = _navButtons.Count * 46 + 10;

            var btn = new Button
            {
                Text = label,
                Tag = key,
                Location = new Point(8, top),
                Size = new Size(218, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgDarkSidebar,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = Theme.FontBody,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => NavigateTo(key);

            _navButtons[key] = btn;
            parent.Controls.Add(btn);
        }

        private void InitializeViews()
        {
            _views["Dashboard"] = new DashboardView(_db, NavigateTo);
            _views["PasteQR"] = new PasteQrView(_db, _decoder, NavigateTo);
            _views["BulkProcess"] = new BulkProcessingView(_db, _decoder, NavigateTo);
            _views["Results"] = new ResultsReviewView(_db, NavigateTo);
            _views["BatchHistory"] = new BatchHistoryView(_db, NavigateTo);
            _views["Export"] = new ExportView(_db);
            _views["Consolidate"] = new ConsolidationView(_db);
            _views["BackupRestore"] = new BackupRestoreView();
            _views["SettingsHelp"] = new SettingsHelpView();
        }

        public void NavigateTo(string key)
        {
            if (!_views.TryGetValue(key, out var targetView)) return;

            _activeViewKey = key;

            // Highlight nav button
            foreach (var kvp in _navButtons)
            {
                if (kvp.Key == key)
                {
                    kvp.Value.BackColor = Theme.BgSidebarActive;
                    kvp.Value.ForeColor = Color.White;
                    kvp.Value.Font = Theme.FontBodyBold;
                }
                else
                {
                    kvp.Value.BackColor = Theme.BgDarkSidebar;
                    kvp.Value.ForeColor = Color.FromArgb(203, 213, 225);
                    kvp.Value.Font = Theme.FontBody;
                }
            }

            // Refresh view data
            if (targetView is DashboardView dv) dv.RefreshStats();
            else if (targetView is ResultsReviewView rv) rv.RefreshGrid();
            else if (targetView is BatchHistoryView bv) bv.RefreshBatches();

            _contentPanel.SuspendLayout();
            _contentPanel.Controls.Clear();
            targetView.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(targetView);
            _contentPanel.ResumeLayout(true);
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                if (_activeViewKey != "PasteQR")
                {
                    NavigateTo("PasteQR");
                }

                if (_views["PasteQR"] is PasteQrView pqv)
                {
                    pqv.PasteFromClipboard();
                }
                e.Handled = true;
            }
        }
    }
}
