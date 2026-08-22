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
