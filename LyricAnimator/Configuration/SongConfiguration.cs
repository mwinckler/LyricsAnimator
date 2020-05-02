using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

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
            return JsonConvert.DeserializeObject<SongConfiguration>(File.ReadAllText(filePath));
        }
    }
}