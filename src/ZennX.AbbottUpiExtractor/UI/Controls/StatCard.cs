using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZennX.AbbottUpiExtractor.UI.Controls
{
    public class StatCard : Panel
    {
        private readonly Label _lblTitle;
        private readonly Label _lblValue;
        private readonly Color _accentColor;

        public StatCard(string title, string value, Color accentColor)
        {
            _accentColor = accentColor;
            Size = new Size(180, 85);
            BackColor = Theme.BgCard;
            Padding = new Padding(12);

            _lblTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                Location = new Point(12, 12),
                AutoSize = true
            };

            _lblValue = new Label
            {
                Text = value,
                Font = Theme.FontStat,
                ForeColor = Theme.TextPrimary,
                Location = new Point(10, 32),
                AutoSize = true
            };

            Controls.Add(_lblTitle);
            Controls.Add(_lblValue);
        }

        public void SetValue(int count)
        {
            _lblValue.Text = count.ToString("N0");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var borderPen = new Pen(Theme.BorderLight, 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            // Left accent bar
            using var brush = new SolidBrush(_accentColor);
            e.Graphics.FillRectangle(brush, 0, 0, 4, Height);
        }
    }
}
