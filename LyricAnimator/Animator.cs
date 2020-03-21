using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LyricAnimator
{
    internal sealed class Animator
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int LyricsSideMargin = 200;
        private const int FramesPerSecond = 60;
        private const int TitleBarHeight = 100;
        private const int GradientBarHeight = 150;

        // This is the y-position, in pixels, where the bottom of
        // the lyrics label should end up at the end of verse time.
        private const double EndOfVerseY = 200;

        private SolidColorBrush titleBrush = new SolidColorBrush(Color.FromRgb(93, 93, 93));

        public void Animate(Configuration config)
        {
            var canvas = new Canvas
            {
                Background = new SolidColorBrush(Colors.Black),
                Width = Width,
                Height = Height,
                ClipToBounds = true
            };

            var lyrics = new List<(Lyric lyric, TextBlock textBlock, int startFrame, double startTop, double pixelsPerFrame)>();

            var y0 = Height - GradientBarHeight;
            var y1 = EndOfVerseY;

            foreach (var lyric in config.Lyrics)
            {
                var textBlock = new TextBlock
                {
                    FontSize = 72,
                    FontFamily = new FontFamily(config.LyricsFont),
                    Text = lyric.Text,
                    Width = Width - LyricsSideMargin * 2,
                    Foreground = new SolidColorBrush(Colors.White),
                    TextWrapping = TextWrapping.Wrap
                };

                canvas.Children.Add(textBlock);
                Canvas.SetTop(textBlock, canvas.ActualHeight);
                Canvas.SetLeft(textBlock, LyricsSideMargin);

                textBlock.Measure(new Size(Width - LyricsSideMargin * 2, double.PositiveInfinity));
                textBlock.Arrange(new Rect(0, 0, 200, 200)); // TODO: Does width/height matter here?
                textBlock.UpdateLayout();

                Render(canvas);

                var distanceToMovePixels = y0 + (textBlock.ActualHeight - y1);
                var pixelsPerSecond = distanceToMovePixels / (lyric.EndSeconds - lyric.StartSeconds);
                var pixelsPerFrame = pixelsPerSecond / FramesPerSecond;
                // This is the number of frames "ahead of time" we need to start
                // rolling the lyric label so that at StartSeconds, the top of the
                // label is fully visible
                var preRollFrames = GradientBarHeight / pixelsPerFrame;
                var startFrame = (int) (lyric.StartSeconds * FramesPerSecond - preRollFrames);
                var startTop = canvas.ActualHeight;

                if (startFrame < 0)
                {
                    // Start the textbox higher up than completely off screen
                    startTop = canvas.ActualHeight - pixelsPerFrame * Math.Abs(startFrame);
                    startFrame = 0;
                }

                lyrics.Add((lyric, textBlock, startFrame, startTop, pixelsPerFrame));
            }

            var topGradient = CreateGradient(false);
            var bottomGradient = CreateGradient(true);
            var titleBackground = new Rectangle
            {
                Width = Width,
                Height = Height,
                Fill = new SolidColorBrush(Colors.Black)
            };

            var titleBar = new TextBlock
            {
                Background = new SolidColorBrush(Colors.Black),
                Foreground = titleBrush,
                Width = Width,
                FontFamily = new FontFamily(config.TitleFont),
                FontSize = 48,
                Text = config.SongTitle.ToUpper(),
                Padding = new Thickness(LyricsSideMargin, 0, LyricsSideMargin, 0)
            };

            canvas.Children.Add(topGradient);
            canvas.Children.Add(bottomGradient);
            canvas.Children.Add(titleBackground);
            canvas.Children.Add(titleBar);
            Canvas.SetTop(topGradient, TitleBarHeight);
            Canvas.SetTop(bottomGradient, Height - GradientBarHeight);

            titleBar.Measure(new Size(Width - LyricsSideMargin * 2, TitleBarHeight));
            titleBar.Arrange(new Rect(0, 0, Width, TitleBarHeight));
            titleBar.UpdateLayout();

            Canvas.SetTop(titleBar, (TitleBarHeight - titleBar.ActualHeight) / 2);

            // Calculate total frames required to animate all lyrics
            var totalFramesRequired = lyrics.Max(lyric => lyric.lyric.EndSeconds) * FramesPerSecond;

            for (var frame = 0; frame <= totalFramesRequired; frame++)
            {
                foreach (var lyric in lyrics)
                {
                    if (frame < lyric.startFrame)
                    {
                        continue;
                    }

                    Canvas.SetTop(
                        lyric.textBlock,
                        lyric.startTop - lyric.pixelsPerFrame * (frame - lyric.startFrame)
                    );
                }

                canvas.UpdateLayout();
                SaveAsPng(GetImage(canvas), @$"c:\tmp\animations\output\{frame:D4}.png");
            }
        }

        private static void Render(UIElement element)
        {
            element.Measure(new Size(Width, Height));
            element.Arrange(new Rect(0, 0, Width, Height));
            element.UpdateLayout();
        }

        private static UIElement CreateGradient(bool flip)
        {
            return new Rectangle
            {
                Fill = new LinearGradientBrush(Colors.Black, Color.FromArgb(0, 0, 0, 0), new Point(0, flip ? 1 : 0), new Point(0, flip ? 0 : 1)),
                Width = Width,
                Height = GradientBarHeight
            };
        }

        private static RenderTargetBitmap GetImage(Canvas canvas)
        {
            var size = new Size(canvas.ActualWidth, canvas.ActualHeight);
            if (size.IsEmpty)
                return null;

            var result = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);

            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(canvas), null, new Rect(new Point(), size));
                context.Close();
            }

            result.Render(drawingVisual);
            return result;
        }

        private static void SaveAsPng(RenderTargetBitmap src, string filename)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));

            using var outputStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(outputStream);
        }
    }
}