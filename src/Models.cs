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

    internal sealed class JukeboxSearchRequest
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public string AlbumOrRelease { get; set; }
        public string FeaturedArtist { get; set; }
        public string ReleaseYear { get; set; }
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
