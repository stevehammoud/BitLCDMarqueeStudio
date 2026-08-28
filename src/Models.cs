using System.Drawing;

namespace BitLCDMarqueeStudio
{
    internal enum MarqueeContentType
    {
        Jukebox,
        Arcade,
        System,
        Collection,
        Custom
    }

    internal enum PanelImageMode
    {
        Fit,
        Fill
    }

    internal enum CanvasEditMode
    {
        JukeboxFixed,
        Freeform
    }

    internal enum TextJustification
    {
        Left,
        Center,
        Right
    }

    internal enum LayerAnimationType
    {
        None,
        FadeIn,
        FadeOut,
        SlideInLeft,
        SlideInRight,
        SlideInUp,
        SlideInDown,
        SlideOutLeft,
        SlideOutRight,
        SlideOutUp,
        SlideOutDown,
        Pulse,
        Bounce,
        Spin,
        ZoomIn,
        ZoomOut
    }

    internal sealed class JukeboxSearchRequest
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public string AlbumOrRelease { get; set; }
        public string FeaturedArtist { get; set; }
        public string ReleaseYear { get; set; }
    }

    internal sealed class ArcadeSearchRequest
    {
        public string GameName { get; set; }
        public string RomName { get; set; }
        public string SystemId { get; set; }
        public string SystemName { get; set; }
        public string SystemSuffix { get; set; }
    }

    internal sealed class FreeformArtLayer
    {
        public bool IsTextLayer { get; set; }
        public string ImagePath { get; set; }
        public string Text { get; set; }
        public Rectangle Bounds { get; set; }
        public PanelImageMode ImageMode { get; set; }
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }
        public float RotationDegrees { get; set; }
        public LayerAnimationType AnimationType { get; set; }
        public float AnimationStartSeconds { get; set; }
        public float AnimationDurationSeconds { get; set; }
        public float VisibleFromSeconds { get; set; }
        public float VisibleToSeconds { get; set; }
        public string FontFamilyName { get; set; }
        public string FontFilePath { get; set; }
        public float FontSize { get; set; }
        public bool FontBold { get; set; }
        public bool FontItalic { get; set; }
        public bool FontUnderline { get; set; }
        public Color TextColor { get; set; }
        public TextJustification TextAlignment { get; set; }
        public bool TextShadow { get; set; }
        public bool TextGlow { get; set; }
    }

    internal sealed class ResourceResult
    {
        public string Source { get; set; }
        public string ResourceType { get; set; }
        public string Label { get; set; }
        public string Detail { get; set; }
        public string ArtworkUrl { get; set; }
        public string CachedImagePath { get; set; }
        public int Score { get; set; }

        public override string ToString()
        {
            string suffix = string.IsNullOrWhiteSpace(ArtworkUrl) ? string.Empty : "  [art]";
            if (Score > 0)
            {
                return string.Format("{0} | {1} | {2} | score {3}{4}", Source, ResourceType, Label, Score, suffix);
            }
            return string.Format("{0} | {1} | {2}{3}", Source, ResourceType, Label, suffix);
        }
    }

    internal sealed class MarqueeLayout
    {
        public const int CanvasWidth = 1920;
        public const int CanvasHeight = 360;

        public Rectangle LeftPanel { get; set; }
        public Rectangle CenterPanel { get; set; }
        public Rectangle RightPanel { get; set; }

        public static MarqueeLayout CreateJukeboxDefault()
        {
            return new MarqueeLayout
            {
                LeftPanel = new Rectangle(0, 0, 360, 360),
                CenterPanel = new Rectangle(360, 0, 1200, 360),
                RightPanel = new Rectangle(1560, 0, 360, 360)
            };
        }
    }
}
