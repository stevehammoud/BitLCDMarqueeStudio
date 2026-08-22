using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BitLCDMarqueeStudio
{
    internal sealed class CanvasPreviewControl : Control
    {
        private MarqueeLayout _layout;
        private Image _backgroundImage;
        private Image _leftImage;
        private Image _rightImage;

        public CanvasPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 12, 24);
            ForeColor = Color.White;
            _layout = MarqueeLayout.CreateJukeboxDefault();
        }

        public MarqueeLayout LayoutModel
        {
            get { return _layout; }
            set
            {
                _layout = value ?? MarqueeLayout.CreateJukeboxDefault();
                Invalidate();
            }
        }

        public void SetBackgroundImage(string path)
        {
            ReplaceImage(ref _backgroundImage, path);
            Invalidate();
        }

        public void SetLeftImage(string path)
        {
            ReplaceImage(ref _leftImage, path);
            Invalidate();
        }

        public void SetRightImage(string path)
        {
            ReplaceImage(ref _rightImage, path);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Rectangle canvas = GetCanvasRectangle();
            using (var bg = new LinearGradientBrush(canvas, Color.FromArgb(16, 18, 38), Color.FromArgb(38, 12, 52), 0f))
            {
                g.FillRectangle(bg, canvas);
            }
            if (_backgroundImage != null)
            {
                DrawImageCover(g, _backgroundImage, canvas);
                using (var dim = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
                {
                    g.FillRectangle(dim, canvas);
                }
            }

            using (var border = new Pen(Color.FromArgb(190, 120, 240, 255), 2))
            {
                g.DrawRectangle(border, canvas);
            }

            DrawPanelImage(g, canvas, _layout.LeftPanel, _leftImage);
            DrawPanelImage(g, canvas, _layout.RightPanel, _rightImage);
            DrawPanel(g, canvas, _layout.LeftPanel, Color.FromArgb(220, 255, 190, 70), "LEFT PANEL");
            DrawPanel(g, canvas, _layout.CenterPanel, Color.FromArgb(220, 80, 220, 255), "CENTER PANEL");
            DrawPanel(g, canvas, _layout.RightPanel, Color.FromArgb(220, 255, 90, 190), "RIGHT PANEL");

            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(210, 240, 245, 255)))
            {
                g.DrawString("Canvas locked: 1920 x 360", font, brush, canvas.Left + 10, canvas.Bottom + 8);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeImage(ref _backgroundImage);
                DisposeImage(ref _leftImage);
                DisposeImage(ref _rightImage);
            }
            base.Dispose(disposing);
        }

        private Rectangle GetCanvasRectangle()
        {
            int padding = 14;
            int availableWidth = Math.Max(1, Width - (padding * 2));
            int availableHeight = Math.Max(1, Height - 40);
            double scale = Math.Min(availableWidth / (double)MarqueeLayout.CanvasWidth, availableHeight / (double)MarqueeLayout.CanvasHeight);
            int w = Math.Max(1, (int)Math.Round(MarqueeLayout.CanvasWidth * scale));
            int h = Math.Max(1, (int)Math.Round(MarqueeLayout.CanvasHeight * scale));
            int x = (Width - w) / 2;
            int y = padding;
            return new Rectangle(x, y, w, h);
        }

        private static RectangleF ScaleRect(Rectangle canvas, Rectangle source)
        {
            float sx = canvas.Width / (float)MarqueeLayout.CanvasWidth;
            float sy = canvas.Height / (float)MarqueeLayout.CanvasHeight;
            return new RectangleF(
                canvas.Left + (source.X * sx),
                canvas.Top + (source.Y * sy),
                source.Width * sx,
                source.Height * sy);
        }

        private static void DrawPanel(Graphics g, Rectangle canvas, Rectangle source, Color color, string label)
        {
            RectangleF rect = ScaleRect(canvas, source);
            using (var fill = new SolidBrush(Color.FromArgb(40, color)))
            using (var pen = new Pen(color, 2))
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(235, color)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(fill, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(label, font, textBrush, rect, format);
            }
        }

        private static void DrawPanelImage(Graphics g, Rectangle canvas, Rectangle source, Image image)
        {
            if (image == null) return;
            RectangleF rect = ScaleRect(canvas, source);
            DrawImageCover(g, image, rect);
        }

        private static void DrawImageCover(Graphics g, Image image, RectangleF dest)
        {
            float srcAspect = image.Width / (float)image.Height;
            float destAspect = dest.Width / dest.Height;
            RectangleF src;
            if (srcAspect > destAspect)
            {
                float srcW = image.Height * destAspect;
                src = new RectangleF((image.Width - srcW) / 2f, 0, srcW, image.Height);
            }
            else
            {
                float srcH = image.Width / destAspect;
                src = new RectangleF(0, (image.Height - srcH) / 2f, image.Width, srcH);
            }
            g.DrawImage(image, dest, src, GraphicsUnit.Pixel);
        }

        private static void ReplaceImage(ref Image target, string path)
        {
            DisposeImage(ref target);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            using (var temp = Image.FromFile(path))
            {
                target = new Bitmap(temp);
            }
        }

        private static void DisposeImage(ref Image image)
        {
            if (image == null) return;
            image.Dispose();
            image = null;
        }
    }
}
