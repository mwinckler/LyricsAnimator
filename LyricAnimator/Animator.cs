using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace LyricAnimator
{
    internal sealed class Animator
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int LyricsSideMargin = 200;
        private const int FramesPerSecond = 60;
        private const int TitleBarHeight = 100;
        private const int BottomBarHeight = 50;
        private const int GradientBarHeight = 150;
        private const int VerseLabelMargin = 100;
        private const int DissolveAnimationDurationFrames = 60;
        private const int EndTransitionDissolveDurationFrames = 120;

        // This is the y-position, in pixels, where the bottom of
        // the lyrics label should end up at the end of verse time.
        private const float EndOfVerseY = Height / 2f;

        private readonly AppConfiguration appConfig;
        private readonly object typefaceLock;

        public Animator(AppConfiguration appConfig, object typefaceLock)
        {
            this.appConfig = appConfig;
            this.typefaceLock = typefaceLock;
        }

        public void Animate(Action<float> reportProgress, SongConfiguration config, string ffmpegExePath, DirectoryInfo outputDirectory, string pngOutputPath = null)
        {
            var lyrics = new List<(
                Lyric lyric,
                int startFrame,
                int endFrame,
                int preRollFrames,
                float startTop,
                float pixelsPerFrame
            )>();

            var desiredReadingY = Height * 2 / 3;

            using var titleTypeface = SKTypeface.FromFamilyName(appConfig.TitleFont.Family);
            using var lyricTypeface = SKTypeface.FromFamilyName(appConfig.LyricsFont.Family);
            using var verseTypeface = SKTypeface.FromFamilyName(appConfig.VerseFont.Family);

            var pixelsPerFrames = new List<float>();

            foreach (var lyric in config.Lyrics)
            {
                var textHeight = CalculateTextHeight(
                    lyricTypeface,
                    appConfig.LyricsFont.Size,
                    lyric.Lines,
                    appConfig.LyricsFont.Size + appConfig.LyricsFont.LineMargin,
                    Width - LyricsSideMargin * 2
                );

                // Assumes starting position at Height (offscreen)
                var distanceToMovePixels = textHeight + (Height - EndOfVerseY);

                // All lyrics need to move at the same speed, else it looks goofy.
                // Find the maximum speed any verse needs, then apply to all verses.
                // In general this should be close enough for similarly-sized verses.
                var pixelsPerSecond = (float)(distanceToMovePixels / (lyric.EndTime.TotalSeconds - lyric.StartTime.TotalSeconds));
                pixelsPerFrames.Add(pixelsPerSecond / FramesPerSecond);
            }

            var pixelsPerFrame = pixelsPerFrames.Average();

            foreach (var lyric in config.Lyrics)
            {
                // This is the number of frames "ahead of time" we need to start
                // rolling the lyric label so that at StartSeconds, the top of the
                // label is fully visible
                var preRollFrames = (int)((Height - desiredReadingY) / pixelsPerFrame);
                var startFrame = (int)(lyric.StartTime.TotalSeconds * FramesPerSecond - preRollFrames);
                var startTop = Height;

                if (startFrame < 0)
                {
                    // Start the textbox higher up than completely off screen
                    startTop = (int)(Height - pixelsPerFrame * Math.Abs(startFrame));
                    startFrame = 0;
                }

                var endFrame = (int)(startFrame + (lyric.EndTime.TotalSeconds - lyric.StartTime.TotalSeconds) * FramesPerSecond) + preRollFrames;

                lyrics.Add((lyric, startFrame, endFrame, preRollFrames, startTop, pixelsPerFrame));
            }

            // Calculate total frames required to animate all lyrics
            var lastEndFrame = lyrics.Max(lyric => lyric.endFrame);
            var lastLyric = lyrics.First(lyric => lyric.endFrame == lastEndFrame);
            var postRollFrames = Math.Min(EndTransitionDissolveDurationFrames, EndOfVerseY / lastLyric.pixelsPerFrame);
            var totalFramesRequired = lastEndFrame + postRollFrames;

            var ffmpegProcess = StartFfmpeg(ffmpegExePath, config.AudioFilePath, Path.Combine(outputDirectory.FullName, config.OutputFilename));

            var info = new SKImageInfo(Width, Height);
            using (var surface = SKSurface.Create(info))
            {
                var canvas = surface.Canvas;

                for (var frame = 0; frame <= totalFramesRequired; frame++)
                {
                    reportProgress(frame / totalFramesRequired);

                    var verseLabel = string.Empty;
                    var verseOpacity = 1f;

                    canvas.Clear(SKColors.Black);

                    foreach (var lyric in lyrics)
                    {
                        if (frame < lyric.startFrame)
                        {
                            continue;
                        }

                        var y = lyric.startTop - lyric.pixelsPerFrame * (frame - lyric.startFrame);
                        DrawLyric(canvas, lyricTypeface, appConfig.LyricsFont.Size, appConfig.LyricsFont.Size + appConfig.LyricsFont.LineMargin, lyric.lyric.Lines, x: LyricsSideMargin, y);

                        if (frame > lyric.startFrame + lyric.preRollFrames && lyric.lyric.VerseNumber > 0)
                        {
                            if (frame >= lyric.startFrame)
                            {
                                verseLabel = $"verse {lyric.lyric.VerseNumber}";

                                verseOpacity = frame < lyric.endFrame - DissolveAnimationDurationFrames
                                    ? Math.Min(1f, (float) (frame - lyric.startFrame) / DissolveAnimationDurationFrames)
                                    : Math.Max(0f, (float) (lyric.endFrame - frame) / DissolveAnimationDurationFrames);
                            }
                        }
                    }

                    DrawGradientOverlays(canvas);

                    if (!string.IsNullOrEmpty(verseLabel))
                    {
                        DrawVerseLabel(canvas, verseTypeface, appConfig.VerseFont.Size, SKColor.Parse(appConfig.VerseFont.HexColor), verseLabel, verseOpacity);
                    }

                    DrawTitleAndFooterBars(canvas, titleTypeface, appConfig.TitleFont.Size, SKColor.Parse(appConfig.TitleFont.HexColor), config.SongTitle);

                    if (totalFramesRequired - frame <= EndTransitionDissolveDurationFrames)
                    {
                        using var paint = new SKPaint {
                            Color = SKColors.Black.WithAlpha((byte)((1 - (totalFramesRequired - frame) / EndTransitionDissolveDurationFrames) * 255))
                        };
                        canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
                    }

                    // DEBUG: Attempt to fix text flicker
                    canvas.Flush();

                    using var image = surface.Snapshot();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var ms = new MemoryStream(data.ToArray()) { Position = 0 };

                    ms.WriteTo(ffmpegProcess.StandardInput.BaseStream);
                    ms.Flush();
                    ffmpegProcess.StandardInput.BaseStream.Flush();

                    if (pngOutputPath != null)
                    {
                        using var fs = new FileStream(Path.Combine(pngOutputPath, $"{frame:D5}.png"), FileMode.Create, FileAccess.Write);

                        ms.Position = 0;
                        ms.WriteTo(fs);
                        ms.Flush();

                        fs.Close();
                    }

                    ms.Close();
                }
            }

            ffmpegProcess.StandardInput.BaseStream.Close();
            ffmpegProcess.WaitForExit();
        }

        private void DrawTitleAndFooterBars(SKCanvas canvas, SKTypeface typeface, float fontSize, SKColor textColor, string songTitle)
        {
            using var paint = CreatePaint(typeface, fontSize, SKColors.Black);
            canvas.DrawRect(new SKRect(0, 0, Width, TitleBarHeight), paint);
            canvas.DrawRect(new SKRect(0, Height - BottomBarHeight, Width, Height), paint);
            paint.Color = textColor;
            SafeDrawText(canvas, songTitle.ToUpper(), LyricsSideMargin, fontSize + (TitleBarHeight - fontSize) / 2, paint);
            paint.StrokeWidth = 3;
            canvas.DrawLine(LyricsSideMargin, TitleBarHeight, Width - LyricsSideMargin, TitleBarHeight, paint);
        }

        private static void DrawGradientOverlays(SKCanvas canvas)
        {
            using var paint = new SKPaint();
            using var topGradient = SKShader.CreateLinearGradient(new SKPoint(0, TitleBarHeight), new SKPoint(0, TitleBarHeight + GradientBarHeight), new[] { SKColors.Black, SKColors.Black.WithAlpha(0) }, SKShaderTileMode.Clamp);
            using var bottomGradient = SKShader.CreateLinearGradient(new SKPoint(0, Height - BottomBarHeight), new SKPoint(0, Height - BottomBarHeight - GradientBarHeight), new[] { SKColors.Black, SKColors.Black.WithAlpha(0) }, SKShaderTileMode.Clamp);
            paint.Shader = topGradient;
            canvas.DrawRect(0, TitleBarHeight, Width, GradientBarHeight, paint);
            paint.Shader = bottomGradient;
            canvas.DrawRect(0, Height - BottomBarHeight - GradientBarHeight, Width, Height - BottomBarHeight, paint);
        }

        private static float CalculateTextHeight(SKTypeface typeface, float fontSize, IEnumerable<string> lines, float lineHeight, int maxWidth)
        {
            using var paint = CreatePaint(typeface, fontSize);
            return lines.SelectMany(line => WrapText(paint, line, maxWidth)).Count() * lineHeight;
        }

        private void DrawLyric(SKCanvas canvas, SKTypeface typeface, float fontSize, float lineHeight, IEnumerable<string> lines, float x, float y)
        {
            using var paint = CreatePaint(typeface, fontSize);

            var i = 0;

            foreach (var lyricLine in lines)
            {
                foreach (var wrappedLine in WrapText(paint, lyricLine, Width - LyricsSideMargin * 2))
                {
                    SafeDrawText(canvas, wrappedLine, x, y + i++ * lineHeight, paint);
                }

                i++;
            }
        }

        private void DrawVerseLabel(SKCanvas canvas, SKTypeface typeface, float fontSize, SKColor color, string text, float verseOpacity)
        {
            using var paint = CreatePaint(typeface, fontSize, color.WithAlpha((byte)(verseOpacity * 255)));
            var labelWidth = paint.MeasureText(text);
            SafeDrawText(canvas, text, Width - VerseLabelMargin - labelWidth, Height - GradientBarHeight / 2, paint);
        }

        private void SafeDrawText(SKCanvas canvas, string text, float x, float y, SKPaint paint)
        {
            lock (typefaceLock)
            {
                canvas.DrawText(text, x, y, paint);
            }
        }

        private static SKPaint CreatePaint(SKTypeface typeface, float fontSize) => CreatePaint(typeface, fontSize, SKColors.White);

        private static SKPaint CreatePaint(SKTypeface typeface, float fontSize, SKColor color)
        {
            return new SKPaint
            {
                Typeface = typeface,
                TextSize = fontSize,
                IsAntialias = true,
                Color = color,
                IsStroke = false
            };
        }

        private static IEnumerable<string> WrapText(SKPaint paint, string text, double maxWidth)
        {
            var words = text.Split(new[] {" "}, StringSplitOptions.None);
            var wrappedLines = new List<string>();

            var line = new System.Text.StringBuilder();
            double width = 0;

            foreach (var word in words)
            {
                var wordWidth = paint.MeasureText($" {word}");
                width += wordWidth;

                if (width < maxWidth)
                {
                    line.Append($"{word} ");
                    continue;
                }

                wrappedLines.Add(line.ToString());
                line.Clear();
                line.Append($"{word} ");
                width = wordWidth;
            }

            if (!string.IsNullOrWhiteSpace(line.ToString()))
            {
                wrappedLines.Add(line.ToString());
            }

            return wrappedLines;
        }

        private static Process StartFfmpeg(string ffmpegExePath, string audioFilePath, string outputFilePath)
        {
            var proc = new Process
            {
                StartInfo =
            {
                FileName = ffmpegExePath,
                Arguments = $"-framerate {FramesPerSecond} -f image2pipe -i - -i {audioFilePath} {outputFilePath}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            }
            };

            proc.Start();
            return proc;
        }
    }
}
