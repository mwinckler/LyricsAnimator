using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LyricAnimatorWpf.Configuration;
using SkiaSharp;

namespace LyricAnimatorWpf
{
    internal sealed class Animator
    {
        private const int DissolveAnimationDurationFrames = 60;

        private readonly TimeSpan endTransitionDuration = TimeSpan.FromSeconds(2);
        private readonly TimeSpan verseLabelHideBeforeVerseEnd = TimeSpan.FromSeconds(5);

        private readonly AppConfiguration appConfig;
        private readonly object typefaceLock;
        private readonly int width;
        private readonly int height;
        private readonly int sideMargin;
        private readonly int headerHeight;
        private readonly int footerHeight;
        private readonly int gradientHeight;

        public Animator(AppConfiguration appConfig, object typefaceLock)
        {
            this.appConfig = appConfig;
            this.typefaceLock = typefaceLock;

            width = appConfig.OutputDimensions.Width;
            height = appConfig.OutputDimensions.Height;
            sideMargin = appConfig.OutputDimensions.SideMargin;
            headerHeight = appConfig.OutputDimensions.HeaderHeight;
            footerHeight = appConfig.OutputDimensions.FooterHeight;
            gradientHeight = appConfig.OutputDimensions.GradientHeight;
        }

        public void Animate(Action<float> reportProgress, SongConfiguration config, string ffmpegExePath, DirectoryInfo outputDirectory, string pngOutputPath = null)
        {
            var desiredReadingY = height * 3 / 4;

            using var titleTypeface = SKTypeface.FromFamilyName(appConfig.TitleFont.Family, SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var lyricTypeface = SKTypeface.FromFamilyName(appConfig.LyricsFont.Family, SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var verseTypeface = SKTypeface.FromFamilyName(appConfig.VerseFont.Family, SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            var timingRegex = new Regex(@"^(?:\[(\d{1,2}:\d{1,2}:\d{1,2})\])?\s*(?:\{([^}]+)\})?\s*(.*)$");
            var unprocessedLines = File.ReadAllLines(config.LyricsFilePath);

            var speedChangeEasingFrames = appConfig.FramesPerSecond * 4;

            var processedLines = new List<(TimeSpan arrivalTime, TimeSpan nextArrivalTime, IEnumerable<string> lines)>();
            var verseLabels = new List<VerseLabel>();

            var segmentLines = new List<string>();
            TimeSpan? segmentArrival = null;

            foreach (var line in unprocessedLines)
            {
                var match = timingRegex.Match(line);
                var nextArrivalGroup = match.Groups[1];

                if (nextArrivalGroup.Success)
                {
                    var nextArrivalTime = TimeSpan.Parse(nextArrivalGroup.Value);

                    if (segmentLines.Any())
                    {
                        processedLines.Add((segmentArrival.Value, nextArrivalTime, segmentLines));
                        segmentLines = new List<string>();
                    }

                    segmentArrival = nextArrivalTime;

                    // TODO: Make it clearer/explicit that verse text requires a paired segment timing
                    if (match.Groups[2].Success)
                    {
                        var arrivalFrame = (int)(segmentArrival.Value.TotalSeconds * appConfig.FramesPerSecond);

                        if (verseLabels.Any())
                        {
                            verseLabels.Last().HiddenFrame = arrivalFrame - (int)(verseLabelHideBeforeVerseEnd.TotalSeconds * appConfig.FramesPerSecond);
                        }

                        verseLabels.Add(new VerseLabel
                        {
                            ArrivalFrame = arrivalFrame,
                            Text = match.Groups[2].Value
                        });
                    }
                }

                var lyricText = match.Groups[3].Value;

                segmentLines.Add(lyricText);
            }

            if (verseLabels.Any())
            {
                verseLabels.Last().HiddenFrame = (int)(processedLines.Last().nextArrivalTime.TotalSeconds * appConfig.FramesPerSecond);
            }

            float? previousPixelsPerFrame = null;

            var speedChanges = new List<(int arrivalFrame, float fromPixelsPerFrame, float toPixelsPerFrame)>();

            var lyrics = new List<Lyric>();

            foreach (var timingSegment in processedLines)
            {
                // Calculate the speed of this segment, defined by (distance / duration)
                var segmentHeight = CalculateTextHeight(
                    lyricTypeface,
                    appConfig.LyricsFont.Size,
                    timingSegment.lines,
                    appConfig.LyricsFont.Size + appConfig.LyricsFont.LineMargin,
                    width - sideMargin * 2
                );

                var segmentDuration = timingSegment.nextArrivalTime - timingSegment.arrivalTime;
                var pixelsPerFrame = (float)(segmentHeight / segmentDuration.TotalSeconds / appConfig.FramesPerSecond);
                var arrivalFrame = (int)(timingSegment.arrivalTime.TotalSeconds * appConfig.FramesPerSecond);

                speedChanges.Add((
                    arrivalFrame,
                    previousPixelsPerFrame.GetValueOrDefault(pixelsPerFrame),
                    pixelsPerFrame
                ));

                var segmentDurationFrames = segmentDuration.TotalSeconds * appConfig.FramesPerSecond;

                previousPixelsPerFrame = pixelsPerFrame;

                lyrics.Add(new Lyric {
                    VisibleFrame = (int)(arrivalFrame - (appConfig.OutputDimensions.Height - desiredReadingY) / pixelsPerFrame - 100),
                    HiddenFrame = (int)(arrivalFrame + segmentDurationFrames + appConfig.OutputDimensions.Height / pixelsPerFrame),
                    ArrivalFrame = arrivalFrame,
                    Lines = timingSegment.lines,
                    Height = segmentHeight
                });
            }

            var currentPixelsPerFrame = speedChanges.First().fromPixelsPerFrame;

            var currentSpeedChangeIndex = 1;
            var speedChangeStartFrame = speedChanges.Count > 1 ? speedChanges[1].arrivalFrame - speedChangeEasingFrames / 2 : new int?();
            float? accelerationPerFrame = null;

            var endTransitionDissolveDurationFrames = (int)(endTransitionDuration.TotalSeconds * appConfig.FramesPerSecond);

            // TODO: Can we calculate total frames required from the song end time automatically?
            // Or do we need an explicit "end of audio" timestamp in the lyrics file?
            var duration = TimeSpan.Parse(config.Duration);

            var totalFramesRequired = duration.TotalSeconds * appConfig.FramesPerSecond;
            var outputFilePath = Path.Combine(outputDirectory.FullName, config.OutputFilename);

            File.Delete(outputFilePath);

            var ffmpegProcess = StartFfmpeg(appConfig, config.AudioFilePath, outputFilePath);

            var info = new SKImageInfo(width, height);
            using (var surface = SKSurface.Create(info))
            {
                var canvas = surface.Canvas;

                for (var frame = 0; frame <= totalFramesRequired; frame++)
                {
                    reportProgress(frame / (float)totalFramesRequired);

                    canvas.Clear(SKColors.Black);

                    if (speedChangeStartFrame.HasValue)
                    {
                        if (frame == speedChangeStartFrame.Value + speedChangeEasingFrames)
                        {
                            currentSpeedChangeIndex++;
                            speedChangeStartFrame = speedChanges.Count > currentSpeedChangeIndex
                                ? speedChanges[currentSpeedChangeIndex].arrivalFrame - speedChangeEasingFrames / 2
                                : new int?();
                            accelerationPerFrame = null;
                        }
                        else if (frame >= speedChangeStartFrame)
                        {
                            accelerationPerFrame ??= (speedChanges[currentSpeedChangeIndex].toPixelsPerFrame - currentPixelsPerFrame) / speedChangeEasingFrames;
                            currentPixelsPerFrame += accelerationPerFrame.Value;
                        }
                    }

                    for (var i = 0; i < lyrics.Count; i++)
                    {
                        var lyric = lyrics[i];

                        if (frame < lyric.VisibleFrame)
                        {
                            continue;
                        }

                        if (frame == lyric.VisibleFrame)
                        {
                            lyric.Y = i > 0
                                ? lyrics[i - 1].Y + lyrics[i - 1].Height
                                : desiredReadingY + (lyric.ArrivalFrame - lyric.VisibleFrame) * currentPixelsPerFrame;
                        }
                        else
                        {
                            lyric.Y -= currentPixelsPerFrame;
                        }

                        DrawLyric(
                            canvas,
                            lyricTypeface,
                            appConfig.LyricsFont.Size,
                            appConfig.LyricsFont.Size + appConfig.LyricsFont.LineMargin,
                            lyric.Lines,
                            x: sideMargin,
                            y: lyric.Y
                        );
                    }

                    DrawGradientOverlays(canvas);

                    var framesToArrivalPointAtCurrentSpeed = (int)((appConfig.OutputDimensions.Height - desiredReadingY) / currentPixelsPerFrame);

                    for (var i = 0; i < verseLabels.Count; i++)
                    {
                        var verseLabel = verseLabels[i];
                        var opacity = 1f;

                        if (frame < verseLabel.ArrivalFrame - framesToArrivalPointAtCurrentSpeed)
                        {
                            continue;
                        }

                        if (frame == verseLabel.ArrivalFrame - framesToArrivalPointAtCurrentSpeed)
                        {
                            verseLabel.Y = appConfig.OutputDimensions.Height;
                        }
                        else if (frame > verseLabel.ArrivalFrame - framesToArrivalPointAtCurrentSpeed &&
                                 verseLabel.Y > appConfig.OutputDimensions.HeaderHeight + appConfig.OutputDimensions.GradientHeight)
                        {
                            verseLabel.Y -= currentPixelsPerFrame;
                        }
                        else if (frame >= verseLabel.HiddenFrame)
                        {
                            opacity = Math.Max(0, 1 - (frame - verseLabel.HiddenFrame) / (float)DissolveAnimationDurationFrames);
                        }

                        DrawVerseLabel(
                            canvas,
                            verseTypeface,
                            appConfig.VerseFont.Size,
                            SKColor.Parse(appConfig.VerseFont.HexColor),
                            verseLabel.Text,
                            width - sideMargin,
                            verseLabel.Y,
                            opacity
                        );
                    }

                    DrawTitleAndFooterBars(canvas, titleTypeface, appConfig.TitleFont.Size, SKColor.Parse(appConfig.TitleFont.HexColor), config.SongTitle);

                    if (totalFramesRequired - frame <= endTransitionDissolveDurationFrames)
                    {
                        var alpha = (1 - (totalFramesRequired - frame) / endTransitionDissolveDurationFrames) * 255;
                        using var paint = new SKPaint {
                            Color = SKColors.Black.WithAlpha((byte)alpha)
                        };
                        canvas.DrawRect(new SKRect(0, 0, width, height), paint);
                    }

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
            canvas.DrawRect(new SKRect(0, 0, width, headerHeight), paint);
            canvas.DrawRect(new SKRect(0, height - footerHeight, width, height), paint);
            paint.Color = textColor;
            SafeDrawText(canvas, songTitle.ToUpper(), sideMargin, fontSize + (headerHeight - fontSize) / 2, paint);
            paint.StrokeWidth = 3;
            canvas.DrawLine(sideMargin, headerHeight, width - sideMargin, headerHeight, paint);
        }

        private void DrawGradientOverlays(SKCanvas canvas)
        {
            using var paint = new SKPaint();
            using var topGradient = SKShader.CreateLinearGradient(new SKPoint(0, headerHeight), new SKPoint(0, headerHeight + gradientHeight), new[] { SKColors.Black, SKColors.Black.WithAlpha(0) }, SKShaderTileMode.Clamp);
            using var bottomGradient = SKShader.CreateLinearGradient(new SKPoint(0, height - footerHeight), new SKPoint(0, height - footerHeight - gradientHeight), new[] { SKColors.Black, SKColors.Black.WithAlpha(0) }, SKShaderTileMode.Clamp);
            paint.Shader = topGradient;
            canvas.DrawRect(0, headerHeight, width, gradientHeight, paint);
            paint.Shader = bottomGradient;
            canvas.DrawRect(0, height - footerHeight - gradientHeight, width, height - footerHeight, paint);
        }

        private static float CalculateTextHeight(SKTypeface typeface, float fontSize, IEnumerable<string> lines, float lineHeight, int maxWidth)
        {
            using var paint = CreatePaint(typeface, fontSize);
            // Note drawing text effectively adds a blank line between each line of text,
            // so we need to add lines.Count() * lineHeight to the overall height.
            var lineCount = lines.SelectMany(line => WrapText(paint, line, maxWidth)).Count();
            return lineCount * lineHeight + lines.Count() * lineHeight;
        }

        private void DrawLyric(SKCanvas canvas, SKTypeface typeface, float fontSize, float lineHeight, IEnumerable<string> lines, float x, float y)
        {
            using var paint = CreatePaint(typeface, fontSize);

            var i = 0;

            foreach (var lyricLine in lines)
            {
                foreach (var wrappedLine in WrapText(paint, lyricLine, width - sideMargin * 2))
                {
                    SafeDrawText(canvas, wrappedLine, x, y + i++ * lineHeight, paint);
                }

                i++;
            }
        }

        private void DrawVerseLabel(SKCanvas canvas, SKTypeface typeface, float fontSize, SKColor color, string text, float x, float y, float verseOpacity)
        {
            using var paint = CreatePaint(typeface, fontSize, color.WithAlpha((byte)(verseOpacity * 255)));
            SafeDrawText(canvas, text, x, y, paint);
        }

        private void SafeDrawText(SKCanvas canvas, string text, float x, float y, SKPaint paint)
        {
            lock (typefaceLock)
            {
                canvas.DrawText(text, x, y, paint);
                canvas.Flush();
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

        private static Process StartFfmpeg(AppConfiguration appConfig, string audioFilePath, string outputFilePath)
        {
            var proc = new Process
            {
                StartInfo =
            {
                FileName = appConfig.FfmpegPath,
                Arguments = $"-framerate {appConfig.FramesPerSecond} -f image2pipe -i - -i {audioFilePath} {outputFilePath}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            }
            };

            proc.Start();
            return proc;
        }

        private sealed class Lyric
        {
            public int VisibleFrame { get; set; }
            public int HiddenFrame { get; set; }
            public int ArrivalFrame { get; set; }
            public IEnumerable<string> Lines { get; set; }
            public float Height { get; set; }
            public float Y { get; set; }
        }

        private sealed class VerseLabel
        {
            public int ArrivalFrame { get; set; }
            public int HiddenFrame { get; set; }
            public string Text { get; set; }
            public float Y { get; set; }
        }
    }
}
