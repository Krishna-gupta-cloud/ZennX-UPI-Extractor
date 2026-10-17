using System;
using System.Drawing;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.Services;

namespace ZennX.AbbottUpiExtractor.UI.Views
{
    public class BatchHistoryView : UserControl
    {
        private readonly DatabaseService _db;
        private readonly Action<string> _navigateAction;
        private DataGridView _grid = null!;

        public BatchHistoryView(DatabaseService db, Action<string> navigateAction)
        {
            _db = db;
            _navigateAction = navigateAction;
            InitializeComponent();
            RefreshBatches();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.BgApp;
            AutoScroll = true;

            var titleLabel = new Label
            {
                Text = "Batch History",
                Font = Theme.FontHeading,
                ForeColor = Theme.TextPrimary,
                Location = new Point(24, 20),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "Historical overview of all QR processing batches recorded on this local workstation.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                Location = new Point(24, 52),
                AutoSize = true
            };

            var card = Theme.CreateCardPanel();
            card.Location = new Point(24, 85);
            card.Size = new Size(1110, 580);

            var btnRefresh = Theme.CreateSecondaryButton("🔄 Refresh", 100, 34);
            btnRefresh.Location = new Point(16, 16);
            btnRefresh.Click += (s, e) => RefreshBatches();

            _grid = new DataGridView
            {
                Location = new Point(16, 60),
                Size = new Size(1075, 500),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Theme.StyleDataGridView(_grid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch ID", Width = 200 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch Name", Width = 230 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Created Date (UTC)", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total Items", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Success", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Failed", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duplicates", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Missing ZID", Width = 90 });

            card.Controls.Add(btnRefresh);
            card.Controls.Add(_grid);

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(card);
        }

        public void RefreshBatches()
        {
            _grid.Rows.Clear();
            var batches = _db.GetAllBatches();
            foreach (var b in batches)
            {
                _grid.Rows.Add(
                    b.BatchId,
                    b.BatchName,
                    b.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    b.TotalCount,
                    b.SuccessCount,
                    b.FailedCount,
                    b.DuplicateCount,
                    b.MissingZidCount
                );
            }
        }
    }
}
