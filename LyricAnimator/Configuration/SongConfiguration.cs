using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LyricAnimator.Configuration
{
    public sealed class SongConfiguration
    {
        public string SongTitle { get; set; }
        public string AudioFilePath { get; set; }
        public string OutputFilename { get; set; }
        public IEnumerable<string> Lyrics { get; set; }
        public string Duration { get; set; }

        public static SongConfiguration LoadFromFile(string filePath)
        {
            // TODO: Make this more robust
            var lines = File.ReadAllLines(filePath);
            var durationLine = lines.Last(line => !string.IsNullOrEmpty(line));
            return new SongConfiguration
            {
                SongTitle = lines.First(),
                Lyrics = lines.Skip(2),
                AudioFilePath = Regex.Replace(filePath, @"\.txt$", ".mp3"),
                OutputFilename = Regex.Replace(Path.GetFileName(filePath), @"\.txt$", ".mp4"),
                Duration = Regex.Replace(durationLine, @"[[\]]", "")
            };
        }
    }
}