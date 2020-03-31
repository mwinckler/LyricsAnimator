using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LyricAnimator.Configuration
{
    internal sealed class SongConfiguration
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
}