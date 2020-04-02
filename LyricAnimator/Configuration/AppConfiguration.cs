using System.IO;
using System.Reflection;

namespace LyricAnimator.Configuration
{
    internal sealed class AppConfiguration
    {
        public FontConfig TitleFont { get; set; } = new FontConfig(50, "#5d5d5d");
        public FontConfig LyricsFont { get; set; } = new FontConfig();
        public FontConfig VerseFont { get; set; } = new FontConfig(32, "#5d5d5d");
        public string FfmpegPath { get; set; }
        public string SongConfigPath { get; set; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public string OutputPath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "output");
        public int FramesPerSecond { get; set; } = 60;
        public DimensionsConfig OutputDimensions { get; set; } = new DimensionsConfig();
    }
}