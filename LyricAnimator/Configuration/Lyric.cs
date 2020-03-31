using System;
using System.Collections.Generic;

namespace LyricAnimator.Configuration
{
    internal sealed class Lyric
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<string> Lines { get; set; }
        public int VerseNumber { get; set; }
    }
}