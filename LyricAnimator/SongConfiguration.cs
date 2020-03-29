using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace LyricAnimator
{
    internal class AppConfiguration
    {
        public FontConfig TitleFont { get; set; } = new FontConfig(50, "#5d5d5d");
        public FontConfig LyricsFont { get; set; } = new FontConfig();
        public FontConfig VerseFont { get; set; } = new FontConfig(32, "#5d5d5d");
        public string FfmpegPath { get; set; }
        public string SongConfigPath { get; set; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public string OutputPath { get; set; } = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "output");
    }

    internal class SongConfiguration
    {
        public string SongTitle { get; set; }
        public string AudioFilePath { get; set; }
        public string OutputFilename { get; set; }
        public List<Lyric> Lyrics { get; set; }

        public static SongConfiguration LoadFromFile(string filePath)
        {
            return JsonConvert.DeserializeObject<SongConfiguration>(File.ReadAllText(filePath));
        }
    }

    internal class Lyric
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<string> Lines { get; set; }
        public int VerseNumber { get; set; }
    }

    internal class FontConfig
    {
        public string Family { get; set; } = "Open Sans";
        public float Size { get; set; } = 72f;
        public string HexColor { get; set; } = "#ffffff";
        public float LineMargin { get; set; } = 20f;

        public FontConfig(float size, string hexColor)
        {
            Size = size;
            HexColor = hexColor;
        }

        public FontConfig()
        {
        }
    }
}