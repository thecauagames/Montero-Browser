using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Montero
{
    internal sealed class PhotonTabBar : Control
    {
        private sealed class TabVisual
        {
            public string Id;
            public string Title;
            public Rectangle Bounds;
            public Rectangle CloseBounds;
            public bool IsAddButton;
            public object Tag;
        }

        private readonly List<TabVisual> tabs = new List<TabVisual>();
        private string selectedTabId;
        private bool darkMode = true;

        public event EventHandler AddTabRequested;
        public event EventHandler<string> TabCloseRequested;
        public event EventHandler<string> TabSelected;

        public bool DarkMode
        {
            get { return darkMode; }
            set
            {
                if (darkMode == value)
                {
                    return;
                }

                darkMode = value;
                Invalidate();
            }
        }
        public string SelectedTabId => selectedTabId;

        public PhotonTabBar()
        {
            Height = 36;
            Dock = DockStyle.Top;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        public bool ContainsTab(string id)
        {
            return tabs.Any(t => !t.IsAddButton && t.Id == id);
        }

        public void AddTab(string id, string title)
        {
            tabs.Add(new TabVisual { Id = id, Title = title });
            EnsureAddButton();
            RecalculateLayout();
            Invalidate();
        }

        public void RemoveTab(string id)
        {
            TabVisual existing = tabs.FirstOrDefault(t => !t.IsAddButton && t.Id == id);
            if (existing?.Tag is Image img)
            {
                img.Dispose();
            }

            tabs.RemoveAll(t => !t.IsAddButton && t.Id == id);
            EnsureAddButton();
            if (selectedTabId == id)
            {
                var first = tabs.FirstOrDefault(t => !t.IsAddButton);
                selectedTabId = first?.Id;
            }

            RecalculateLayout();
            Invalidate();
        }

        public void UpdateTabIcon(string id, Image icon)
        {
            TabVisual tab = tabs.FirstOrDefault(t => !t.IsAddButton && t.Id == id);
            if (tab == null)
            {
                return;
            }

            if (tab.Tag is Image existing)
            {
                existing.Dispose();
            }

            tab.Tag = icon == null ? null : new Bitmap(icon);
            Invalidate(tab.Bounds);
        }

        public void UpdateTabTitle(string id, string title)
        {
            TabVisual tab = tabs.FirstOrDefault(t => !t.IsAddButton && t.Id == id);
            if (tab == null)
            {
                return;
            }

            tab.Title = title;
            RecalculateLayout();
            Invalidate();
        }

        public void SelectTab(string id)
        {
            if (!ContainsTab(id))
            {
                return;
            }

            selectedTabId = id;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Keep transparent so DWM frame tint stays visible.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            foreach (TabVisual tab in tabs)
            {
                if (tab.IsAddButton)
                {
                    DrawAddButton(e.Graphics, tab.Bounds);
                }
                else
                {
                    DrawTab(e.Graphics, tab);
                }
            }

            using (var pen = new Pen(DarkMode ? Color.FromArgb(100, 88, 96, 112) : Color.FromArgb(110, 165, 176, 198)))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                TabVisual tab = tabs[i];
                if (!tab.Bounds.Contains(e.Location))
                {
                    continue;
                }

                if (tab.IsAddButton)
                {
                    AddTabRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (Rectangle.Inflate(tab.CloseBounds, 3, 3).Contains(e.Location))
                {
                    TabCloseRequested?.Invoke(this, tab.Id);
                    return;
                }

                if (selectedTabId != tab.Id)
                {
                    selectedTabId = tab.Id;
                    Invalidate();
                    TabSelected?.Invoke(this, tab.Id);
                }

                return;
            }
        }

        private void EnsureAddButton()
        {
            tabs.RemoveAll(t => t.IsAddButton);
            tabs.Add(new TabVisual { Id = "__add__", Title = "+", IsAddButton = true });
        }

        private void RecalculateLayout()
        {
            int x = 10;
            int y = 5;
            int h = 27;

            foreach (TabVisual tab in tabs)
            {
                if (tab.IsAddButton)
                {
                    tab.Bounds = new Rectangle(x + 8, y + 1, 24, 24);
                    tab.CloseBounds = Rectangle.Empty;
                    x += 38;
                    continue;
                }

                int width = 190;
                tab.Bounds = new Rectangle(x, y, width, h);
                tab.CloseBounds = new Rectangle(tab.Bounds.Right - 19, tab.Bounds.Top + 6, 14, 14);
                x += width + 6;
            }
        }

        private void DrawTab(Graphics g, TabVisual tab)
        {
            bool selected = tab.Id == selectedTabId;
            Rectangle rect = tab.Bounds;

            using (GraphicsPath path = CreateRoundedRectPath(rect, 3))
            using (var fill = new SolidBrush(selected ? GetActiveColor() : GetInactiveColor()))
            using (var border = new Pen(GetBorderColor()))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int textLeft = rect.Left + 12;
            if (tab.Tag is Image icon)
            {
                Rectangle iconRect = new Rectangle(rect.Left + 10, rect.Top + 5, 16, 16);
                g.DrawImage(icon, iconRect);
                textLeft = iconRect.Right + 8;
            }

            Rectangle textRect = new Rectangle(textLeft, rect.Top + 5, Math.Max(12, rect.Width - (textLeft - rect.Left) - 24), rect.Height - 10);
            using (var textBrush = new SolidBrush(GetTextColor()))
            using (var format = new StringFormat(StringFormatFlags.NoWrap))
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString(tab.Title, Font, textBrush, textRect, format);
            }

            using (var pen = new Pen(GetTextColor(), 1.6f))
            {
                g.DrawLine(pen, tab.CloseBounds.Left + 3, tab.CloseBounds.Top + 3, tab.CloseBounds.Right - 3, tab.CloseBounds.Bottom - 3);
                g.DrawLine(pen, tab.CloseBounds.Right - 3, tab.CloseBounds.Top + 3, tab.CloseBounds.Left + 3, tab.CloseBounds.Bottom - 3);
            }
        }

        private void DrawAddButton(Graphics g, Rectangle rect)
        {
            using (GraphicsPath path = CreateRoundedRectPath(rect, 3))
            using (var fill = new SolidBrush(GetInactiveColor()))
            using (var border = new Pen(GetBorderColor()))
            using (var pen = new Pen(GetTextColor(), 1.8f))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
                int cx = rect.Left + rect.Width / 2;
                int cy = rect.Top + rect.Height / 2;
                g.DrawLine(pen, cx - 4, cy, cx + 4, cy);
                g.DrawLine(pen, cx, cy - 4, cx, cy + 4);
            }
        }

        private Color GetActiveColor()
        {
            return DarkMode ? Color.FromArgb(205, 58, 63, 74) : Color.FromArgb(242, 255, 255, 255);
        }

        private Color GetInactiveColor()
        {
            return DarkMode ? Color.FromArgb(130, 45, 50, 60) : Color.FromArgb(210, 214, 223, 238);
        }

        private Color GetBorderColor()
        {
            return DarkMode ? Color.FromArgb(120, 94, 100, 112) : Color.FromArgb(140, 168, 179, 198);
        }

        private Color GetTextColor()
        {
            // Light mode must always be solid dark text for readability over translucent backgrounds.
            return DarkMode ? Color.FromArgb(240, 243, 248) : Color.FromArgb(8, 12, 18);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
