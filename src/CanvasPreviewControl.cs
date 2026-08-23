using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BitLCDMarqueeStudio
{
    internal sealed class CanvasPreviewControl : Control
    {
        private MarqueeLayout _layout;
        private Image _leftImage;
        private Image _middleImage;
        private Image _rightImage;
        private string _artistText;
        private string _titleText;
        private string _featuredArtistText;

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

        public void SetLeftImage(string path)
        {
            ReplaceImage(ref _leftImage, path);
            Invalidate();
        }

        public void ClearLeftImage()
        {
            DisposeImage(ref _leftImage);
            Invalidate();
        }

        public void SetMiddleImage(string path)
        {
            ReplaceImage(ref _middleImage, path);
            Invalidate();
        }

        public void ClearMiddleImage()
        {
            DisposeImage(ref _middleImage);
            Invalidate();
        }

        public void SetRightImage(string path)
        {
            ReplaceImage(ref _rightImage, path);
            Invalidate();
        }

        public void ClearRightImage()
        {
            DisposeImage(ref _rightImage);
            Invalidate();
        }

        public void SetJukeboxText(string artist, string title, string featuredArtist)
        {
            _artistText = artist ?? string.Empty;
            _titleText = title ?? string.Empty;
            _featuredArtistText = featuredArtist ?? string.Empty;
            Invalidate();
        }

        public void SaveJpeg(string path)
        {
            using (var bitmap = new Bitmap(MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight))
            using (var g = Graphics.FromImage(bitmap))
            {
                DrawCanvas(g, new Rectangle(0, 0, MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight), false);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            DrawCanvas(g, GetCanvasRectangle(), true);
        }

        private void DrawCanvas(Graphics g, Rectangle canvas, bool showGuides)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using (var bg = new LinearGradientBrush(canvas, Color.FromArgb(16, 18, 38), Color.FromArgb(38, 12, 52), 0f))
            {
                g.FillRectangle(bg, canvas);
            }

            DrawGeneratedBackground(g, canvas);

            using (var border = new Pen(Color.FromArgb(190, 120, 240, 255), 2))
            {
                g.DrawRectangle(border, canvas);
            }

            DrawPanelImage(g, canvas, _layout.LeftPanel, _leftImage);
            DrawPanelImage(g, canvas, _layout.RightPanel, _rightImage);
            if (_middleImage != null)
            {
                Rectangle padded = new Rectangle(
                    _layout.CenterPanel.X + 15,
                    _layout.CenterPanel.Y + 15,
                    Math.Max(1, _layout.CenterPanel.Width - 30),
                    Math.Max(1, _layout.CenterPanel.Height - 30));
                DrawImageContain(g, _middleImage, ScaleRect(canvas, padded));
            }
            else
            {
                DrawJukeboxText(g, canvas);
            }

            if (showGuides)
            {
                DrawPanel(g, canvas, _layout.LeftPanel, Color.FromArgb(220, 255, 190, 70), "LEFT");
                DrawPanel(g, canvas, _layout.CenterPanel, Color.FromArgb(220, 80, 220, 255), "MIDDLE");
                DrawPanel(g, canvas, _layout.RightPanel, Color.FromArgb(220, 255, 90, 190), "RIGHT");

                using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(210, 240, 245, 255)))
                {
                    g.DrawString("Canvas locked: 1920 x 360", font, brush, canvas.Left + 10, canvas.Bottom + 8);
                }
            }
        }

        private void DrawGeneratedBackground(Graphics g, Rectangle canvas)
        {
            int seed = Math.Abs(((_artistText ?? string.Empty) + "|" + (_titleText ?? string.Empty)).GetHashCode());
            Color[] colors =
            {
                Color.FromArgb(120, 255, 40, 180),
                Color.FromArgb(120, 30, 220, 255),
                Color.FromArgb(115, 255, 180, 45),
                Color.FromArgb(100, 120, 255, 110)
            };

            using (var pen = new Pen(colors[seed % colors.Length], Math.Max(2, canvas.Height / 120f)))
            {
                for (int i = -canvas.Width; i < canvas.Width * 2; i += Math.Max(20, canvas.Width / 24))
                {
                    g.DrawLine(pen, canvas.Left + i, canvas.Top, canvas.Left + i + (canvas.Width / 5), canvas.Bottom);
                }
            }

            for (int i = 0; i < 18; i++)
            {
                int x = canvas.Left + ((seed + (i * 173)) % Math.Max(1, canvas.Width));
                int y = canvas.Top + ((seed / 3 + (i * 61)) % Math.Max(1, canvas.Height));
                int size = 30 + ((seed + i * 13) % 80);
                using (var brush = new SolidBrush(Color.FromArgb(26, colors[(seed + i) % colors.Length])))
                using (var pen = new Pen(Color.FromArgb(70, colors[(seed + i) % colors.Length]), 2))
                {
                    g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
                    g.DrawEllipse(pen, x - size / 2, y - size / 2, size, size);
                }
            }
        }

        private void DrawJukeboxText(Graphics g, Rectangle canvas)
        {
            string artist = string.IsNullOrWhiteSpace(_featuredArtistText)
                ? (_artistText ?? string.Empty)
                : (_artistText + " FT. " + _featuredArtistText);
            string title = _titleText ?? string.Empty;
            RectangleF center = ScaleRect(canvas, _layout.CenterPanel);
            RectangleF titleRect = new RectangleF(center.X + center.Width * 0.04f, center.Y + center.Height * 0.12f, center.Width * 0.92f, center.Height * 0.36f);
            RectangleF artistRect = new RectangleF(center.X + center.Width * 0.08f, center.Y + center.Height * 0.55f, center.Width * 0.84f, center.Height * 0.30f);

            using (Font titleFont = FitFont(g, title, titleRect, 92f))
            using (Font artistFont = FitFont(g, artist, artistRect, 64f))
            {
                DrawOutlinedString(g, title.ToUpperInvariant(), titleFont, titleRect, Color.FromArgb(255, 250, 218, 145), Color.FromArgb(255, 28, 20, 54));
                DrawOutlinedString(g, artist.ToUpperInvariant(), artistFont, artistRect, Color.FromArgb(255, 150, 242, 255), Color.FromArgb(255, 22, 36, 62));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeImage(ref _leftImage);
                DisposeImage(ref _middleImage);
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

        private static void DrawImageContain(Graphics g, Image image, RectangleF dest)
        {
            float scale = Math.Min(dest.Width / image.Width, dest.Height / image.Height);
            float w = image.Width * scale;
            float h = image.Height * scale;
            var rect = new RectangleF(dest.X + ((dest.Width - w) / 2f), dest.Y + ((dest.Height - h) / 2f), w, h);
            g.DrawImage(image, rect);
        }

        private static Font FitFont(Graphics g, string text, RectangleF rect, float maxSize)
        {
            if (string.IsNullOrWhiteSpace(text)) text = " ";
            for (float size = maxSize; size >= 16f; size -= 2f)
            {
                var font = new Font("Arial", size, FontStyle.Bold, GraphicsUnit.Pixel);
                SizeF measured = g.MeasureString(text, font);
                if (measured.Width <= rect.Width && measured.Height <= rect.Height) return font;
                font.Dispose();
            }
            return new Font("Arial", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        private static void DrawOutlinedString(Graphics g, string text, Font font, RectangleF rect, Color fill, Color outline)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var path = new GraphicsPath())
            using (var fillBrush = new SolidBrush(fill))
            using (var glowPen = new Pen(Color.FromArgb(130, fill), Math.Max(10f, font.Size * 0.18f)))
            using (var outlinePen = new Pen(outline, Math.Max(4f, font.Size * 0.07f)))
            {
                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, rect, format);
                g.DrawPath(glowPen, path);
                g.FillPath(fillBrush, path);
                g.DrawPath(outlinePen, path);
            }
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
