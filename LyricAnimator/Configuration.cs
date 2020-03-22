using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LyricAnimator
{
    internal class Configuration
    {
        public string SongTitle { get; set; }
        public string TitleFont { get; set; }
        public string LyricsFont { get; set; }
        public List<Lyric> Lyrics { get; set; }

        public static Configuration LoadFromFile(string filePath)
        {
            return JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(filePath));
        }
    }

    internal class Lyric
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Text { get; set; }
        public int Verse { get; set; }
    }
}