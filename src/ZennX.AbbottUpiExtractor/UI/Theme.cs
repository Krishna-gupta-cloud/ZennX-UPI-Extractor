using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZennX.AbbottUpiExtractor.UI
{
    public static class Theme
    {
        // Core ZennX Brand Palette
        public static readonly Color ZennXBlue = Color.FromArgb(0, 117, 207);       // #0075CF
        public static readonly Color ZennXBlueDark = Color.FromArgb(0, 90, 160);
        public static readonly Color ZennXOrange = Color.FromArgb(255, 152, 41);    // #FF9829
        public static readonly Color ZennXOrangeDark = Color.FromArgb(220, 125, 20);

        // Neutral & Surface Palette
        public static readonly Color BgDarkSidebar = Color.FromArgb(17, 24, 39);    // Slate 900
        public static readonly Color BgSidebarHover = Color.FromArgb(31, 41, 55);   // Slate 800
        public static readonly Color BgSidebarActive = Color.FromArgb(0, 117, 207);  // Brand Blue
        public static readonly Color BgApp = Color.FromArgb(248, 250, 252);         // Slate 50
        public static readonly Color BgCard = Color.White;
        public static readonly Color BorderLight = Color.FromArgb(226, 232, 240);   // Slate 200

        // Typography Colors
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);      // Slate 900
        public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139); // Slate 500
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // Slate 400

        // Status Colors
        public static readonly Color StatusSuccess = Color.FromArgb(16, 185, 129);  // Emerald 500
        public static readonly Color StatusWarning = Color.FromArgb(245, 158, 11);  // Amber 500
        public static readonly Color StatusDanger = Color.FromArgb(239, 68, 68);    // Red 500
        public static readonly Color StatusInfo = Color.FromArgb(59, 130, 246);     // Blue 500

        // Standard Fonts
        public static readonly Font FontHeading = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font FontSubheading = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontStat = new Font("Segoe UI", 18f, FontStyle.Bold);

        public static Button CreatePrimaryButton(string text, int width = 140, int height = 36)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = ZennXBlue,
                ForeColor = Color.White,
                Font = FontBodyBold,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ZennXBlueDark;
            btn.MouseLeave += (s, e) => btn.BackColor = ZennXBlue;
            return btn;
        }

        public static Button CreateOrangeButton(string text, int width = 140, int height = 36)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = ZennXOrange,
                ForeColor = Color.White,
                Font = FontBodyBold,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ZennXOrangeDark;
            btn.MouseLeave += (s, e) => btn.BackColor = ZennXOrange;
            return btn;
        }

        public static Button CreateSecondaryButton(string text, int width = 120, int height = 36)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextPrimary,
                Font = FontBody,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = BorderLight;
            btn.FlatAppearance.BorderSize = 1;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(241, 245, 249);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.White;
            return btn;
        }

        public static Panel CreateCardPanel()
        {
            var pnl = new Panel
            {
                BackColor = BgCard,
                Padding = new Padding(16)
            };
            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderLight, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };
            return pnl;
        }

        public static void StyleDataGridView(DataGridView grid)
        {
            grid.BackgroundColor = BgCard;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = BorderLight;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowTemplate.Height = 36;
            grid.EnableHeadersVisualStyles = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = FontBodyBold;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            grid.ColumnHeadersHeight = 38;

            grid.DefaultCellStyle.Font = FontBody;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 242, 254);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(12, 74, 110);
            grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        }
    }
}
