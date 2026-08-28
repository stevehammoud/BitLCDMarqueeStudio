using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace BitLCDMarqueeStudio
{
    internal sealed class CanvasPreviewControl : Control
    {
        private MarqueeLayout _layout;
        private Image _leftImage;
        private Image _middleImage;
        private Image _rightImage;
        private string _leftImagePath;
        private string _middleImagePath;
        private string _rightImagePath;
        private Image _backgroundImage;
        private string _backgroundImagePath;
        private PanelImageMode _leftImageMode;
        private PanelImageMode _middleImageMode;
        private PanelImageMode _rightImageMode;
        private CanvasEditMode _editMode;
        private readonly List<FreeformArtLayer> _freeformLayers;
        private readonly Dictionary<string, Image> _freeformImageCache;
        private readonly Dictionary<string, Rectangle> _freeformVisibleBoundsCache;
        private int _selectedLayerIndex;
        private bool _draggingLayer;
        private ResizeHandle _activeResizeHandle;
        private Point _lastMousePoint;
        private string _artistText;
        private string _titleText;
        private string _featuredArtistText;
        private bool _animationPreviewEnabled;
        private float _animationPreviewSeconds;

        public event EventHandler SelectedLayerChanged;

        public CanvasPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 12, 24);
            ForeColor = Color.White;
            _layout = MarqueeLayout.CreateJukeboxDefault();
            _leftImageMode = PanelImageMode.Fit;
            _middleImageMode = PanelImageMode.Fit;
            _rightImageMode = PanelImageMode.Fit;
            _editMode = CanvasEditMode.JukeboxFixed;
            _freeformLayers = new List<FreeformArtLayer>();
            _freeformImageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
            _freeformVisibleBoundsCache = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
            _selectedLayerIndex = -1;
            _activeResizeHandle = ResizeHandle.None;
            _animationPreviewEnabled = false;
            _animationPreviewSeconds = 0f;
            TabStop = true;
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

        public CanvasEditMode EditMode
        {
            get { return _editMode; }
            set
            {
                _editMode = value;
                Invalidate();
            }
        }

        public bool HasSelectedFreeformLayer
        {
            get { return _selectedLayerIndex >= 0 && _selectedLayerIndex < _freeformLayers.Count; }
        }

        public void SetLeftImage(string path)
        {
            SetLeftImage(path, PanelImageMode.Fit);
        }

        public void SetLeftImage(string path, PanelImageMode mode)
        {
            ReplaceImage(ref _leftImage, path);
            _leftImagePath = NormalizeImagePath(path);
            _leftImageMode = mode;
            Invalidate();
        }

        public void ClearLeftImage()
        {
            DisposeImage(ref _leftImage);
            _leftImagePath = string.Empty;
            _leftImageMode = PanelImageMode.Fit;
            Invalidate();
        }

        public void SetMiddleImage(string path)
        {
            SetMiddleImage(path, PanelImageMode.Fit);
        }

        public void SetMiddleImage(string path, PanelImageMode mode)
        {
            ReplaceImage(ref _middleImage, path);
            _middleImagePath = NormalizeImagePath(path);
            _middleImageMode = mode;
            Invalidate();
        }

        public void ClearMiddleImage()
        {
            DisposeImage(ref _middleImage);
            _middleImagePath = string.Empty;
            _middleImageMode = PanelImageMode.Fit;
            Invalidate();
        }

        public void SetRightImage(string path)
        {
            SetRightImage(path, PanelImageMode.Fit);
        }

        public void SetRightImage(string path, PanelImageMode mode)
        {
            ReplaceImage(ref _rightImage, path);
            _rightImagePath = NormalizeImagePath(path);
            _rightImageMode = mode;
            Invalidate();
        }

        public void ClearRightImage()
        {
            DisposeImage(ref _rightImage);
            _rightImagePath = string.Empty;
            _rightImageMode = PanelImageMode.Fit;
            Invalidate();
        }

        public void SetBackgroundImage(string path)
        {
            ReplaceImage(ref _backgroundImage, path);
            _backgroundImagePath = NormalizeImagePath(path);
            Invalidate();
        }

        public void ClearBackgroundImage()
        {
            DisposeImage(ref _backgroundImage);
            _backgroundImagePath = string.Empty;
            Invalidate();
        }

        public void SetJukeboxText(string artist, string title, string featuredArtist)
        {
            _artistText = artist ?? string.Empty;
            _titleText = title ?? string.Empty;
            _featuredArtistText = featuredArtist ?? string.Empty;
            Invalidate();
        }

        public void AddFreeformImage(string path, PanelImageMode mode)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            using (var temp = Image.FromFile(path))
            {
                Size size = GetInitialFreeformSize(temp);
                var layer = new FreeformArtLayer
                {
                    IsTextLayer = false,
                    ImagePath = path,
                    Text = string.Empty,
                    ImageMode = mode,
                    FlipHorizontal = false,
                    FlipVertical = false,
                    RotationDegrees = 0f,
                    AnimationType = LayerAnimationType.None,
                    AnimationStartSeconds = 0f,
                    AnimationDurationSeconds = 1f,
                    VisibleFromSeconds = 0f,
                    VisibleToSeconds = 20f,
                    Bounds = new Rectangle(
                        (MarqueeLayout.CanvasWidth - size.Width) / 2,
                        (MarqueeLayout.CanvasHeight - size.Height) / 2,
                        size.Width,
                        size.Height)
                };
                _freeformLayers.Add(layer);
                _selectedLayerIndex = _freeformLayers.Count - 1;
            }
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void AddFreeformText(
            string text,
            string fontFamilyName,
            string fontFilePath,
            float fontSize,
            bool bold,
            bool italic,
            bool underline,
            Color color,
            TextJustification alignment,
            bool shadow,
            bool glow)
        {
            var layer = new FreeformArtLayer
            {
                IsTextLayer = true,
                ImagePath = string.Empty,
                Text = text ?? string.Empty,
                Bounds = new Rectangle(460, 72, 1000, 220),
                ImageMode = PanelImageMode.Fit,
                FlipHorizontal = false,
                FlipVertical = false,
                RotationDegrees = 0f,
                AnimationType = LayerAnimationType.None,
                AnimationStartSeconds = 0f,
                AnimationDurationSeconds = 1f,
                VisibleFromSeconds = 0f,
                VisibleToSeconds = 20f,
                FontFamilyName = string.IsNullOrWhiteSpace(fontFamilyName) ? "Arial" : fontFamilyName,
                FontFilePath = fontFilePath ?? string.Empty,
                FontSize = Math.Max(8f, fontSize),
                FontBold = bold,
                FontItalic = italic,
                FontUnderline = underline,
                TextColor = color,
                TextAlignment = alignment,
                TextShadow = shadow,
                TextGlow = glow
            };
            _freeformLayers.Add(layer);
            _selectedLayerIndex = _freeformLayers.Count - 1;
            OnSelectedLayerChanged();
            Invalidate();
        }

        public FreeformArtLayer GetSelectedLayerSnapshot()
        {
            if (!HasSelectedFreeformLayer) return null;
            return CloneFreeformLayer(_freeformLayers[_selectedLayerIndex]);
        }

        public IList<FreeformArtLayer> GetLayerSnapshots()
        {
            return CloneFreeformLayers(_freeformLayers);
        }

        public int SelectedLayerIndex
        {
            get { return _selectedLayerIndex; }
        }

        public void SelectLayer(int index)
        {
            if (index < 0 || index >= _freeformLayers.Count) return;
            _selectedLayerIndex = index;
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void UpdateSelectedTextLayer(
            string text,
            string fontFamilyName,
            string fontFilePath,
            float fontSize,
            bool bold,
            bool italic,
            bool underline,
            Color color,
            TextJustification alignment,
            bool shadow,
            bool glow)
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            if (!layer.IsTextLayer) return;
            layer.Text = text ?? string.Empty;
            layer.FontFamilyName = string.IsNullOrWhiteSpace(fontFamilyName) ? "Arial" : fontFamilyName;
            layer.FontFilePath = fontFilePath ?? string.Empty;
            layer.FontSize = Math.Max(8f, fontSize);
            layer.FontBold = bold;
            layer.FontItalic = italic;
            layer.FontUnderline = underline;
            layer.TextColor = color;
            layer.TextAlignment = alignment;
            layer.TextShadow = shadow;
            layer.TextGlow = glow;
            Invalidate();
        }

        public void ClearFreeformLayers()
        {
            _freeformLayers.Clear();
            _selectedLayerIndex = -1;
            _activeResizeHandle = ResizeHandle.None;
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void DeleteSelectedLayer()
        {
            if (!HasSelectedFreeformLayer) return;
            _freeformLayers.RemoveAt(_selectedLayerIndex);
            if (_freeformLayers.Count == 0)
            {
                _selectedLayerIndex = -1;
            }
            else if (_selectedLayerIndex >= _freeformLayers.Count)
            {
                _selectedLayerIndex = _freeformLayers.Count - 1;
            }
            _activeResizeHandle = ResizeHandle.None;
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void BringSelectedLayerForward()
        {
            if (!HasSelectedFreeformLayer || _selectedLayerIndex >= _freeformLayers.Count - 1) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            _freeformLayers.RemoveAt(_selectedLayerIndex);
            _selectedLayerIndex++;
            _freeformLayers.Insert(_selectedLayerIndex, layer);
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void SendSelectedLayerBackward()
        {
            if (!HasSelectedFreeformLayer || _selectedLayerIndex <= 0) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            _freeformLayers.RemoveAt(_selectedLayerIndex);
            _selectedLayerIndex--;
            _freeformLayers.Insert(_selectedLayerIndex, layer);
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void SetSelectedLayerImageMode(PanelImageMode mode)
        {
            if (!HasSelectedFreeformLayer) return;
            _freeformLayers[_selectedLayerIndex].ImageMode = mode;
            Invalidate();
        }

        public void FlipSelectedLayerHorizontal()
        {
            if (!HasSelectedFreeformLayer) return;
            _freeformLayers[_selectedLayerIndex].FlipHorizontal = !_freeformLayers[_selectedLayerIndex].FlipHorizontal;
            Invalidate();
        }

        public void FlipSelectedLayerVertical()
        {
            if (!HasSelectedFreeformLayer) return;
            _freeformLayers[_selectedLayerIndex].FlipVertical = !_freeformLayers[_selectedLayerIndex].FlipVertical;
            Invalidate();
        }

        public void RotateSelectedLayer(float degrees)
        {
            if (!HasSelectedFreeformLayer) return;
            _freeformLayers[_selectedLayerIndex].RotationDegrees = NormalizeRotation(degrees);
            Invalidate();
        }

        public void UpdateSelectedLayerAnimation(LayerAnimationType animationType, float startSeconds, float durationSeconds, float visibleFromSeconds, float visibleToSeconds)
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            layer.AnimationType = animationType;
            layer.AnimationStartSeconds = Math.Max(0f, startSeconds);
            layer.AnimationDurationSeconds = Math.Max(0.1f, durationSeconds);
            layer.VisibleFromSeconds = Math.Max(0f, visibleFromSeconds);
            layer.VisibleToSeconds = Math.Max(layer.VisibleFromSeconds, visibleToSeconds);
            Invalidate();
        }

        public void DuplicateSelectedLayer()
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer clone = CloneFreeformLayer(_freeformLayers[_selectedLayerIndex]);
            _freeformLayers.Insert(_selectedLayerIndex + 1, clone);
            _selectedLayerIndex++;
            OnSelectedLayerChanged();
            Invalidate();
        }

        public void SetAnimationPreview(float seconds, bool enabled)
        {
            _animationPreviewSeconds = Math.Max(0f, seconds);
            _animationPreviewEnabled = enabled;
            Invalidate();
        }

        public void MoveSelectedLayer(int dx, int dy)
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            layer.Bounds = new Rectangle(layer.Bounds.X + dx, layer.Bounds.Y + dy, layer.Bounds.Width, layer.Bounds.Height);
            Invalidate();
        }

        public void ScaleSelectedLayer(float factor)
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            int width = ClampLayerDimension((int)Math.Round(layer.Bounds.Width * factor), MarqueeLayout.CanvasWidth * 4);
            int height = ClampLayerDimension((int)Math.Round(layer.Bounds.Height * factor), MarqueeLayout.CanvasHeight * 4);
            int centerX = layer.Bounds.X + (layer.Bounds.Width / 2);
            int centerY = layer.Bounds.Y + (layer.Bounds.Height / 2);
            layer.Bounds = new Rectangle(centerX - (width / 2), centerY - (height / 2), width, height);
            Invalidate();
        }

        public CanvasState CaptureState()
        {
            return new CanvasState
            {
                LeftImagePath = _leftImagePath ?? string.Empty,
                MiddleImagePath = _middleImagePath ?? string.Empty,
                RightImagePath = _rightImagePath ?? string.Empty,
                BackgroundImagePath = _backgroundImagePath ?? string.Empty,
                LeftImageMode = _leftImageMode,
                MiddleImageMode = _middleImageMode,
                RightImageMode = _rightImageMode,
                EditMode = _editMode,
                FreeformLayers = CloneFreeformLayers(_freeformLayers),
                SelectedLayerIndex = _selectedLayerIndex,
                ArtistText = _artistText ?? string.Empty,
                TitleText = _titleText ?? string.Empty,
                FeaturedArtistText = _featuredArtistText ?? string.Empty
            };
        }

        public void RestoreState(CanvasState state)
        {
            if (state == null) return;
            SetLeftImage(state.LeftImagePath, state.LeftImageMode);
            SetMiddleImage(state.MiddleImagePath, state.MiddleImageMode);
            SetRightImage(state.RightImagePath, state.RightImageMode);
            SetBackgroundImage(state.BackgroundImagePath);
            _editMode = state.EditMode;
            _freeformLayers.Clear();
            _freeformLayers.AddRange(CloneFreeformLayers(state.FreeformLayers ?? new List<FreeformArtLayer>()));
            _selectedLayerIndex = state.SelectedLayerIndex >= 0 && state.SelectedLayerIndex < _freeformLayers.Count ? state.SelectedLayerIndex : -1;
            _activeResizeHandle = ResizeHandle.None;
            SetJukeboxText(state.ArtistText, state.TitleText, state.FeaturedArtistText);
            OnSelectedLayerChanged();
        }

        public void SaveJpeg(string path)
        {
            using (var bitmap = new Bitmap(MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight))
            using (var g = Graphics.FromImage(bitmap))
            {
                DrawCanvas(g, new Rectangle(0, 0, MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight), false, false);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        public void SaveAnimationFramePng(string path, float seconds)
        {
            bool previousEnabled = _animationPreviewEnabled;
            float previousSeconds = _animationPreviewSeconds;
            try
            {
                _animationPreviewEnabled = true;
                _animationPreviewSeconds = Math.Max(0f, seconds);
                using (var bitmap = new Bitmap(MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight))
                using (var g = Graphics.FromImage(bitmap))
                {
                    DrawCanvas(g, new Rectangle(0, 0, MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight), false, true);
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            finally
            {
                _animationPreviewEnabled = previousEnabled;
                _animationPreviewSeconds = previousSeconds;
            }
        }

        public float GetAnimationDurationSeconds()
        {
            float duration = 0f;
            foreach (FreeformArtLayer layer in _freeformLayers)
            {
                if (layer.AnimationType == LayerAnimationType.None) continue;
                duration = Math.Max(duration, Math.Max(layer.VisibleToSeconds, Math.Max(0f, layer.AnimationStartSeconds) + Math.Max(0.1f, layer.AnimationDurationSeconds)));
            }
            return Math.Max(3f, duration);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Rectangle canvas = GetCanvasRectangle();
            GraphicsState state = g.Save();
            g.TranslateTransform(canvas.Left, canvas.Top);
            g.ScaleTransform(
                canvas.Width / (float)MarqueeLayout.CanvasWidth,
                canvas.Height / (float)MarqueeLayout.CanvasHeight);
            DrawCanvas(g, new Rectangle(0, 0, MarqueeLayout.CanvasWidth, MarqueeLayout.CanvasHeight), true, _animationPreviewEnabled);
            g.Restore(state);
        }

        private void DrawCanvas(Graphics g, Rectangle canvas, bool showGuides, bool applyAnimations)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using (var bg = new SolidBrush(Color.Black))
            {
                g.FillRectangle(bg, canvas);
            }

            if (_backgroundImage != null)
            {
                DrawImageCover(g, _backgroundImage, canvas);
            }

            GraphicsState canvasClip = g.Save();
            g.SetClip(canvas, CombineMode.Intersect);
            if (_editMode == CanvasEditMode.Freeform)
            {
                DrawFreeformLayers(g, canvas, showGuides, applyAnimations);
            }
            else
            {
                DrawPanelImage(g, canvas, _layout.LeftPanel, _leftImage, _leftImageMode);
                DrawPanelImage(g, canvas, _layout.RightPanel, _rightImage, _rightImageMode);
                if (_middleImage != null)
                {
                    DrawPanelImage(g, canvas, _layout.CenterPanel, _middleImage, _middleImageMode);
                }
                else
                {
                    DrawJukeboxText(g, canvas);
                }
            }
            g.Restore(canvasClip);

            if (showGuides)
            {
                using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(210, 240, 245, 255)))
                {
                    g.DrawString("Canvas locked: 1920 x 360", font, brush, canvas.Left + 10, canvas.Bottom + 8);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_editMode != CanvasEditMode.Freeform) return;
            Focus();

            Rectangle canvas = GetCanvasRectangle();
            if (HasSelectedFreeformLayer)
            {
                _activeResizeHandle = HitTestResizeHandle(e.Location);
                if (_activeResizeHandle != ResizeHandle.None)
                {
                    _draggingLayer = true;
                    _lastMousePoint = e.Location;
                    Cursor = CursorForResizeHandle(_activeResizeHandle);
                    return;
                }
            }

            int selected = -1;
            for (int i = _freeformLayers.Count - 1; i >= 0; i--)
            {
                RectangleF rect = ScaleRect(canvas, _freeformLayers[i].Bounds);
                if (rect.Contains(e.Location))
                {
                    selected = i;
                    break;
                }
            }

            if (_selectedLayerIndex != selected)
            {
                _selectedLayerIndex = selected;
                OnSelectedLayerChanged();
                Invalidate();
            }

            _draggingLayer = HasSelectedFreeformLayer && e.Button == MouseButtons.Left;
            _lastMousePoint = e.Location;
            Cursor = _draggingLayer ? Cursors.SizeAll : Cursors.Default;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_editMode != CanvasEditMode.Freeform) return;

            if (_draggingLayer && HasSelectedFreeformLayer)
            {
                Rectangle canvas = GetCanvasRectangle();
                float sx = MarqueeLayout.CanvasWidth / (float)Math.Max(1, canvas.Width);
                float sy = MarqueeLayout.CanvasHeight / (float)Math.Max(1, canvas.Height);
                int dx = (int)Math.Round((e.X - _lastMousePoint.X) * sx);
                int dy = (int)Math.Round((e.Y - _lastMousePoint.Y) * sy);
                if (dx != 0 || dy != 0)
                {
                    if (_activeResizeHandle == ResizeHandle.None)
                    {
                        MoveSelectedLayer(dx, dy);
                    }
                    else
                    {
                        ResizeSelectedLayer(_activeResizeHandle, dx, dy);
                    }
                    _lastMousePoint = e.Location;
                }
                return;
            }

            ResizeHandle handle = HasSelectedFreeformLayer ? HitTestResizeHandle(e.Location) : ResizeHandle.None;
            Cursor = handle != ResizeHandle.None ? CursorForResizeHandle(handle) :
                (HitTestFreeformLayer(e.Location) >= 0 ? Cursors.SizeAll : Cursors.Default);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingLayer = false;
            _activeResizeHandle = ResizeHandle.None;
            Cursor = Cursors.Default;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (_editMode == CanvasEditMode.Freeform &&
                (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down))
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_editMode != CanvasEditMode.Freeform) return;

            int step = e.Shift ? 20 : 4;
            if (e.KeyCode == Keys.Left)
            {
                MoveSelectedLayer(-step, 0);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                MoveSelectedLayer(step, 0);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                MoveSelectedLayer(0, -step);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                MoveSelectedLayer(0, step);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)
            {
                ScaleSelectedLayer(1.06f);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
            {
                ScaleSelectedLayer(0.94f);
                e.Handled = true;
            }
        }

        private void DrawFreeformLayers(Graphics g, Rectangle canvas, bool showGuides, bool applyAnimations)
        {
            for (int i = 0; i < _freeformLayers.Count; i++)
            {
                FreeformArtLayer layer = _freeformLayers[i];
                if (applyAnimations && !IsLayerVisibleAt(layer, _animationPreviewSeconds)) continue;
                RectangleF rect = ScaleRect(canvas, layer.Bounds);
                AnimationFrame frame = applyAnimations ? GetAnimationFrame(layer, rect, canvas) : new AnimationFrame(rect, 1f, 0f);
                rect = frame.Bounds;
                GraphicsState state = null;
                float totalRotation = layer.RotationDegrees + frame.ExtraRotationDegrees;
                if (layer.FlipHorizontal || layer.FlipVertical || Math.Abs(totalRotation) > 0.01f)
                {
                    state = g.Save();
                    g.TranslateTransform(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
                    if (Math.Abs(totalRotation) > 0.01f) g.RotateTransform(totalRotation);
                    g.ScaleTransform(layer.FlipHorizontal ? -1 : 1, layer.FlipVertical ? -1 : 1);
                    g.TranslateTransform(-(rect.X + (rect.Width / 2f)), -(rect.Y + (rect.Height / 2f)));
                }

                if (layer.IsTextLayer)
                {
                    DrawFreeformTextLayer(g, layer, rect, frame.Alpha);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(layer.ImagePath) || !System.IO.File.Exists(layer.ImagePath))
                    {
                        if (state != null) g.Restore(state);
                        continue;
                    }

                    Image image = GetFreeformCachedImage(layer.ImagePath);
                    if (image == null)
                    {
                        if (state != null) g.Restore(state);
                        continue;
                    }

                    if (layer.ImageMode == PanelImageMode.Fill)
                    {
                        DrawImageStretch(g, image, rect, frame.Alpha);
                    }
                    else
                    {
                        DrawImageContainTrimmed(g, image, rect, GetFreeformVisibleBounds(layer.ImagePath, image), frame.Alpha);
                    }
                }
                if (state != null) g.Restore(state);

                if (showGuides && i == _selectedLayerIndex)
                {
                    RectangleF guideRect = ScaleRect(canvas, layer.Bounds);
                    using (var pen = new Pen(Color.FromArgb(240, 255, 235, 70), 3))
                    using (var fill = new SolidBrush(Color.FromArgb(35, 255, 235, 70)))
                    using (var handleBrush = new SolidBrush(Color.FromArgb(245, 255, 235, 70)))
                    {
                        g.FillRectangle(fill, guideRect);
                        g.DrawRectangle(pen, guideRect.X, guideRect.Y, guideRect.Width, guideRect.Height);
                        foreach (RectangleF handle in GetResizeHandles(guideRect).Values)
                        {
                            g.FillRectangle(handleBrush, handle);
                            g.DrawRectangle(Pens.Black, handle.X, handle.Y, handle.Width, handle.Height);
                        }
                    }
                }
            }
        }

        private AnimationFrame GetAnimationFrame(FreeformArtLayer layer, RectangleF baseRect, Rectangle canvas)
        {
            if (layer.AnimationType == LayerAnimationType.None)
            {
                return new AnimationFrame(baseRect, 1f, 0f);
            }

            float duration = Math.Max(0.1f, layer.AnimationDurationSeconds <= 0 ? 1f : layer.AnimationDurationSeconds);
            float raw = (_animationPreviewSeconds - Math.Max(0f, layer.AnimationStartSeconds)) / duration;
            float progress = Math.Max(0f, Math.Min(1f, raw));
            float eased = EaseInOut(progress);
            RectangleF rect = baseRect;
            float alpha = 1f;
            float extraRotation = 0f;

            if (layer.AnimationType == LayerAnimationType.FadeIn)
            {
                alpha = raw <= 0f ? 0f : eased;
            }
            else if (layer.AnimationType == LayerAnimationType.FadeOut)
            {
                alpha = raw >= 1f ? 0f : 1f - eased;
            }
            else if (layer.AnimationType == LayerAnimationType.SlideInLeft)
            {
                rect = new RectangleF(baseRect.X - (canvas.Width * (1f - eased)), baseRect.Y, baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideInRight)
            {
                rect = new RectangleF(baseRect.X + (canvas.Width * (1f - eased)), baseRect.Y, baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideInUp)
            {
                rect = new RectangleF(baseRect.X, baseRect.Y + (canvas.Height * (1f - eased)), baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideInDown)
            {
                rect = new RectangleF(baseRect.X, baseRect.Y - (canvas.Height * (1f - eased)), baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideOutLeft)
            {
                rect = new RectangleF(baseRect.X - (canvas.Width * eased), baseRect.Y, baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideOutRight)
            {
                rect = new RectangleF(baseRect.X + (canvas.Width * eased), baseRect.Y, baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideOutUp)
            {
                rect = new RectangleF(baseRect.X, baseRect.Y - (canvas.Height * eased), baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.SlideOutDown)
            {
                rect = new RectangleF(baseRect.X, baseRect.Y + (canvas.Height * eased), baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.Pulse)
            {
                float cycle = raw < 0f ? 0f : raw - (float)Math.Floor(raw);
                float pulse = (float)Math.Sin(cycle * Math.PI * 2d);
                rect = ScaleFromCenter(baseRect, 1f + (0.18f * pulse));
            }
            else if (layer.AnimationType == LayerAnimationType.Bounce)
            {
                float cycle = raw < 0f ? 0f : raw - (float)Math.Floor(raw);
                float bounce = Math.Abs((float)Math.Sin(cycle * Math.PI * 2d));
                float travel = Math.Max(10f, baseRect.Height * 0.18f);
                rect = new RectangleF(baseRect.X, baseRect.Y - (travel * bounce), baseRect.Width, baseRect.Height);
            }
            else if (layer.AnimationType == LayerAnimationType.Spin)
            {
                extraRotation = 360f * eased;
            }
            else if (layer.AnimationType == LayerAnimationType.ZoomIn)
            {
                rect = ScaleFromCenter(baseRect, 0.2f + (0.8f * eased));
            }
            else if (layer.AnimationType == LayerAnimationType.ZoomOut)
            {
                rect = ScaleFromCenter(baseRect, 1f - (0.8f * eased));
            }

            return new AnimationFrame(rect, Math.Max(0f, Math.Min(1f, alpha)), extraRotation);
        }

        private static bool IsLayerVisibleAt(FreeformArtLayer layer, float seconds)
        {
            float from = Math.Max(0f, layer.VisibleFromSeconds);
            float to = layer.VisibleToSeconds <= 0f ? 20f : layer.VisibleToSeconds;
            return seconds >= from && seconds <= to;
        }

        private void ResizeSelectedLayer(ResizeHandle handle, int dx, int dy)
        {
            if (!HasSelectedFreeformLayer) return;
            FreeformArtLayer layer = _freeformLayers[_selectedLayerIndex];
            Rectangle bounds = layer.Bounds;
            int left = bounds.Left;
            int top = bounds.Top;
            int right = bounds.Right;
            int bottom = bounds.Bottom;

            if (handle == ResizeHandle.Left || handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomLeft) left += dx;
            if (handle == ResizeHandle.Right || handle == ResizeHandle.TopRight || handle == ResizeHandle.BottomRight) right += dx;
            if (handle == ResizeHandle.Top || handle == ResizeHandle.TopLeft || handle == ResizeHandle.TopRight) top += dy;
            if (handle == ResizeHandle.Bottom || handle == ResizeHandle.BottomLeft || handle == ResizeHandle.BottomRight) bottom += dy;

            const int minSize = 12;
            if (right - left < minSize)
            {
                if (handle == ResizeHandle.Left || handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomLeft) left = right - minSize;
                else right = left + minSize;
            }
            if (bottom - top < minSize)
            {
                if (handle == ResizeHandle.Top || handle == ResizeHandle.TopLeft || handle == ResizeHandle.TopRight) top = bottom - minSize;
                else bottom = top + minSize;
            }

            layer.Bounds = Rectangle.FromLTRB(left, top, right, bottom);
            Invalidate();
        }

        private int HitTestFreeformLayer(Point point)
        {
            Rectangle canvas = GetCanvasRectangle();
            for (int i = _freeformLayers.Count - 1; i >= 0; i--)
            {
                RectangleF rect = ScaleRect(canvas, _freeformLayers[i].Bounds);
                if (rect.Contains(point)) return i;
            }
            return -1;
        }

        private ResizeHandle HitTestResizeHandle(Point point)
        {
            Rectangle canvas = GetCanvasRectangle();
            if (!HasSelectedFreeformLayer) return ResizeHandle.None;
            RectangleF rect = ScaleRect(canvas, _freeformLayers[_selectedLayerIndex].Bounds);
            foreach (KeyValuePair<ResizeHandle, RectangleF> handle in GetResizeHandles(rect))
            {
                if (handle.Value.Contains(point)) return handle.Key;
            }
            return ResizeHandle.None;
        }

        private static Dictionary<ResizeHandle, RectangleF> GetResizeHandles(RectangleF rect)
        {
            const float size = 10f;
            float half = size / 2f;
            float midX = rect.Left + rect.Width / 2f;
            float midY = rect.Top + rect.Height / 2f;
            return new Dictionary<ResizeHandle, RectangleF>
            {
                { ResizeHandle.TopLeft, new RectangleF(rect.Left - half, rect.Top - half, size, size) },
                { ResizeHandle.Top, new RectangleF(midX - half, rect.Top - half, size, size) },
                { ResizeHandle.TopRight, new RectangleF(rect.Right - half, rect.Top - half, size, size) },
                { ResizeHandle.Right, new RectangleF(rect.Right - half, midY - half, size, size) },
                { ResizeHandle.BottomRight, new RectangleF(rect.Right - half, rect.Bottom - half, size, size) },
                { ResizeHandle.Bottom, new RectangleF(midX - half, rect.Bottom - half, size, size) },
                { ResizeHandle.BottomLeft, new RectangleF(rect.Left - half, rect.Bottom - half, size, size) },
                { ResizeHandle.Left, new RectangleF(rect.Left - half, midY - half, size, size) }
            };
        }

        private static Cursor CursorForResizeHandle(ResizeHandle handle)
        {
            if (handle == ResizeHandle.Left || handle == ResizeHandle.Right) return Cursors.SizeWE;
            if (handle == ResizeHandle.Top || handle == ResizeHandle.Bottom) return Cursors.SizeNS;
            if (handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomRight) return Cursors.SizeNWSE;
            if (handle == ResizeHandle.TopRight || handle == ResizeHandle.BottomLeft) return Cursors.SizeNESW;
            return Cursors.Default;
        }

        private static void DrawFreeformTextLayer(Graphics g, FreeformArtLayer layer, RectangleF rect, float alpha)
        {
            string text = layer.Text ?? string.Empty;
            if (text.Trim().Length == 0) return;
            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f) return;

            FontStyle style = FontStyle.Regular;
            if (layer.FontBold) style |= FontStyle.Bold;
            if (layer.FontItalic) style |= FontStyle.Italic;
            if (layer.FontUnderline) style |= FontStyle.Underline;

            using (Font font = CreateLayerFont(layer, style))
            using (var format = new StringFormat { LineAlignment = StringAlignment.Center })
            using (var path = new GraphicsPath())
            using (var fill = new SolidBrush(ApplyAlpha(layer.TextColor.IsEmpty ? Color.White : layer.TextColor, alpha)))
            {
                if (layer.TextAlignment == TextJustification.Left) format.Alignment = StringAlignment.Near;
                else if (layer.TextAlignment == TextJustification.Right) format.Alignment = StringAlignment.Far;
                else format.Alignment = StringAlignment.Center;

                path.AddString(text, font.FontFamily, (int)font.Style, font.Size, rect, format);

                if (layer.TextShadow)
                {
                    using (var shadowPath = new GraphicsPath())
                    using (var shadowBrush = new SolidBrush(Color.FromArgb((int)(160 * alpha), Color.Black)))
                    {
                        var shadowRect = new RectangleF(rect.X + 7, rect.Y + 7, rect.Width, rect.Height);
                        shadowPath.AddString(text, font.FontFamily, (int)font.Style, font.Size, shadowRect, format);
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }

                if (layer.TextGlow)
                {
                    Color glow = layer.TextColor.IsEmpty ? Color.White : layer.TextColor;
                    using (var penWide = new Pen(Color.FromArgb((int)(85 * alpha), glow), Math.Max(8f, font.Size * 0.18f)))
                    using (var penTight = new Pen(Color.FromArgb((int)(150 * alpha), glow), Math.Max(3f, font.Size * 0.06f)))
                    {
                        penWide.LineJoin = LineJoin.Round;
                        penTight.LineJoin = LineJoin.Round;
                        g.DrawPath(penWide, path);
                        g.DrawPath(penTight, path);
                    }
                }

                g.FillPath(fill, path);
            }
        }

        private static Font CreateLayerFont(FreeformArtLayer layer, FontStyle style)
        {
            float size = Math.Max(8f, layer.FontSize <= 0 ? 92f : layer.FontSize);
            if (!string.IsNullOrWhiteSpace(layer.FontFilePath) && System.IO.File.Exists(layer.FontFilePath))
            {
                try
                {
                    var fonts = new PrivateFontCollection();
                    fonts.AddFontFile(layer.FontFilePath);
                    if (fonts.Families.Length > 0)
                    {
                        return new Font(fonts.Families[0], size, style, GraphicsUnit.Pixel);
                    }
                }
                catch
                {
                }
            }

            string family = string.IsNullOrWhiteSpace(layer.FontFamilyName) ? "Arial" : layer.FontFamilyName;
            try
            {
                return new Font(family, size, style, GraphicsUnit.Pixel);
            }
            catch
            {
                return new Font("Arial", size, style, GraphicsUnit.Pixel);
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
                DisposeImage(ref _backgroundImage);
                foreach (Image image in _freeformImageCache.Values)
                {
                    image.Dispose();
                }
                _freeformImageCache.Clear();
                _freeformVisibleBoundsCache.Clear();
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

        private static Size GetInitialFreeformSize(Image image)
        {
            const int maxWidth = 720;
            const int maxHeight = 300;
            float scale = Math.Min(maxWidth / (float)image.Width, maxHeight / (float)image.Height);
            scale = Math.Min(1f, scale);
            return new Size(Math.Max(12, (int)Math.Round(image.Width * scale)), Math.Max(12, (int)Math.Round(image.Height * scale)));
        }

        private static int ClampLayerDimension(int value, int max)
        {
            return Math.Max(12, Math.Min(max, value));
        }

        private static List<FreeformArtLayer> CloneFreeformLayers(IEnumerable<FreeformArtLayer> layers)
        {
            return layers.Select(CloneFreeformLayer).ToList();
        }

        private static FreeformArtLayer CloneFreeformLayer(FreeformArtLayer layer)
        {
            if (layer == null) return null;
            return new FreeformArtLayer
            {
                IsTextLayer = layer.IsTextLayer,
                ImagePath = layer.ImagePath,
                Text = layer.Text,
                Bounds = layer.Bounds,
                ImageMode = layer.ImageMode,
                FlipHorizontal = layer.FlipHorizontal,
                FlipVertical = layer.FlipVertical,
                RotationDegrees = layer.RotationDegrees,
                AnimationType = layer.AnimationType,
                AnimationStartSeconds = layer.AnimationStartSeconds,
                AnimationDurationSeconds = layer.AnimationDurationSeconds,
                VisibleFromSeconds = layer.VisibleFromSeconds,
                VisibleToSeconds = layer.VisibleToSeconds,
                FontFamilyName = layer.FontFamilyName,
                FontFilePath = layer.FontFilePath,
                FontSize = layer.FontSize,
                FontBold = layer.FontBold,
                FontItalic = layer.FontItalic,
                FontUnderline = layer.FontUnderline,
                TextColor = layer.TextColor,
                TextAlignment = layer.TextAlignment,
                TextShadow = layer.TextShadow,
                TextGlow = layer.TextGlow
            };
        }

        private Image GetFreeformCachedImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
            Image image;
            if (_freeformImageCache.TryGetValue(path, out image)) return image;
            using (var temp = Image.FromFile(path))
            {
                image = new Bitmap(temp);
            }
            _freeformImageCache[path] = image;
            return image;
        }

        private Rectangle GetFreeformVisibleBounds(string path, Image image)
        {
            if (string.IsNullOrWhiteSpace(path)) return new Rectangle(0, 0, image.Width, image.Height);
            Rectangle bounds;
            if (_freeformVisibleBoundsCache.TryGetValue(path, out bounds)) return bounds;
            bounds = GetVisibleImageBounds(image);
            _freeformVisibleBoundsCache[path] = bounds;
            return bounds;
        }

        private void OnSelectedLayerChanged()
        {
            EventHandler handler = SelectedLayerChanged;
            if (handler != null) handler(this, EventArgs.Empty);
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

        private static void DrawPanelImage(Graphics g, Rectangle canvas, Rectangle source, Image image, PanelImageMode mode)
        {
            if (image == null) return;
            RectangleF rect = ScaleRect(canvas, source);
            if (mode == PanelImageMode.Fill)
            {
                DrawImageStretch(g, image, rect);
            }
            else
            {
                DrawImageContainTrimmed(g, image, rect);
            }
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

        private static void DrawImageStretch(Graphics g, Image image, RectangleF dest)
        {
            g.DrawImage(image, dest);
        }

        private static void DrawImageStretch(Graphics g, Image image, RectangleF dest, float alpha)
        {
            DrawImageAlpha(g, image, dest, new RectangleF(0, 0, image.Width, image.Height), alpha);
        }

        private static void DrawImageContain(Graphics g, Image image, RectangleF dest)
        {
            float scale = Math.Min(dest.Width / image.Width, dest.Height / image.Height);
            float w = image.Width * scale;
            float h = image.Height * scale;
            var rect = new RectangleF(dest.X + ((dest.Width - w) / 2f), dest.Y + ((dest.Height - h) / 2f), w, h);
            g.DrawImage(image, rect);
        }

        private static void DrawImageContainTrimmed(Graphics g, Image image, RectangleF dest)
        {
            DrawImageContainTrimmed(g, image, dest, GetVisibleImageBounds(image));
        }

        private static void DrawImageContainTrimmed(Graphics g, Image image, RectangleF dest, Rectangle source)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                DrawImageContain(g, image, dest);
                return;
            }

            float scale = Math.Min(dest.Width / source.Width, dest.Height / source.Height);
            float w = source.Width * scale;
            float h = source.Height * scale;
            var rect = new RectangleF(dest.X + ((dest.Width - w) / 2f), dest.Y + ((dest.Height - h) / 2f), w, h);
            g.DrawImage(image, rect, source, GraphicsUnit.Pixel);
        }

        private static void DrawImageContainTrimmed(Graphics g, Image image, RectangleF dest, Rectangle source, float alpha)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                DrawImageAlpha(g, image, dest, new RectangleF(0, 0, image.Width, image.Height), alpha);
                return;
            }

            float scale = Math.Min(dest.Width / source.Width, dest.Height / source.Height);
            float w = source.Width * scale;
            float h = source.Height * scale;
            var rect = new RectangleF(dest.X + ((dest.Width - w) / 2f), dest.Y + ((dest.Height - h) / 2f), w, h);
            DrawImageAlpha(g, image, rect, source, alpha);
        }

        private static void DrawImageAlpha(Graphics g, Image image, RectangleF dest, RectangleF source, float alpha)
        {
            alpha = Math.Max(0f, Math.Min(1f, alpha));
            if (alpha <= 0f) return;
            if (alpha >= 0.999f)
            {
                g.DrawImage(image, dest, source, GraphicsUnit.Pixel);
                return;
            }

            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix();
                matrix.Matrix33 = alpha;
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(
                    image,
                    Rectangle.Round(dest),
                    source.X,
                    source.Y,
                    source.Width,
                    source.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        private static Rectangle GetVisibleImageBounds(Image image)
        {
            using (var bitmap = new Bitmap(image))
            {
                int minX = bitmap.Width;
                int minY = bitmap.Height;
                int maxX = -1;
                int maxY = -1;
                bool hasTransparentPixel = false;

                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.A < 250) hasTransparentPixel = true;
                        if (pixel.A <= 12) continue;

                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                if (!hasTransparentPixel || maxX < minX || maxY < minY)
                {
                    return new Rectangle(0, 0, image.Width, image.Height);
                }

                return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
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

        private static string NormalizeImagePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path) ? string.Empty : path;
        }

        private static float NormalizeRotation(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;
            degrees = degrees % 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }

        private static float EaseInOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - (2f * value));
        }

        private static RectangleF ScaleFromCenter(RectangleF rect, float scale)
        {
            float w = rect.Width * scale;
            float h = rect.Height * scale;
            return new RectangleF(rect.X + ((rect.Width - w) / 2f), rect.Y + ((rect.Height - h) / 2f), w, h);
        }

        private static Color ApplyAlpha(Color color, float alpha)
        {
            return Color.FromArgb((int)(Math.Max(0f, Math.Min(1f, alpha)) * color.A), color);
        }
    }

    internal struct AnimationFrame
    {
        public AnimationFrame(RectangleF bounds, float alpha, float extraRotationDegrees)
            : this()
        {
            Bounds = bounds;
            Alpha = alpha;
            ExtraRotationDegrees = extraRotationDegrees;
        }

        public RectangleF Bounds { get; private set; }
        public float Alpha { get; private set; }
        public float ExtraRotationDegrees { get; private set; }
    }

    internal enum ResizeHandle
    {
        None,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left
    }

    internal sealed class CanvasState
    {
        public string LeftImagePath { get; set; }
        public string MiddleImagePath { get; set; }
        public string RightImagePath { get; set; }
        public string BackgroundImagePath { get; set; }
        public PanelImageMode LeftImageMode { get; set; }
        public PanelImageMode MiddleImageMode { get; set; }
        public PanelImageMode RightImageMode { get; set; }
        public CanvasEditMode EditMode { get; set; }
        public List<FreeformArtLayer> FreeformLayers { get; set; }
        public int SelectedLayerIndex { get; set; }
        public string ArtistText { get; set; }
        public string TitleText { get; set; }
        public string FeaturedArtistText { get; set; }
    }
}
