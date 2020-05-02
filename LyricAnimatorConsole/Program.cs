using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine;
using LyricAnimator;
using LyricAnimator.Configuration;
using Newtonsoft.Json;

namespace LyricAnimatorConsole
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => {
                    if (!File.Exists(opts.AppConfig))
                    {
                        Console.Error.WriteLine($"Configuration file not found: '{opts.AppConfig}'");
                        Environment.Exit(1);
                    }

                    var appConfig = JsonConvert.DeserializeObject<AppConfiguration>(File.ReadAllText(opts.AppConfig));
                    var animator = new Animator(appConfig);
                    var outputDir = Directory.CreateDirectory(opts.OutputDir);

                    if (File.Exists(opts.SongConfigFile))
                    {
                        var songConfig = LoadSongConfig(opts.SongConfigFile);
                        Console.WriteLine($"Processing single song config: '{opts.SongConfigFile}'");
                        ProcessSong(ProgressReporterFactory(songConfig.OutputFilename), animator, songConfig, outputDir);
                        Console.WriteLine("Finished.");
                        Environment.Exit(0);
                    }
                    else
                    {
                        if (!Directory.Exists(opts.SongConfigDir))
                        {
                            Console.Error.WriteLine($"Song config directory '{opts.SongConfigDir}' does not exist.");
                            Environment.Exit(1);
                        }

                        var configs = Directory.GetFiles(opts.SongConfigDir, "*.txt");

                        if (!configs.Any())
                        {
                            Console.Error.WriteLine($"No .txt lyric files found in directory '{opts.SongConfigDir}'");
                            Environment.Exit(1);
                        }

                        Console.WriteLine($"Processing {configs.Count()} configuration files from '{opts.SongConfigDir}'...");

                        foreach (var config in configs)
                        {
                            var songConfig = LoadSongConfig(config);
                            ProcessSong(ProgressReporterFactory(songConfig.OutputFilename), animator, songConfig, outputDir);
                            Console.Write("\n");
                        }
                    }
                });

            static Action<float> ProgressReporterFactory(string filename) =>
                progressValue =>
                {
                    var percentLabel = progressValue.ToString("P1");
                    var availableWidth = Console.WindowWidth - (filename.Length + percentLabel.Length + 5);
                    var hashMarks = string.Join("", Enumerable.Range(1, (int)(availableWidth * progressValue)).Select(_ => "#"));
                    Console.Write($"\r{filename}: {percentLabel} {hashMarks}");
                };
        }

        private static void ProcessSong(
            Action<float> progressReporter,
            Animator animator,
            SongConfiguration songConfig,
            DirectoryInfo outputDirectory
        )
        {
            animator.Animate(progressReporter, songConfig, outputDirectory);
        }

        private static SongConfiguration LoadSongConfig(string filePath)
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

        internal sealed class Options
        {
            [Option(
                "appConfig",
                Required = false,
                HelpText = "Path to the application configuration JSON file.",
                Default = "./appConfig.json"
            )]
            public string AppConfig { get; set; }

            [Option(
                "songConfig",
                SetName = "singleSong",
                Required = false,
                HelpText = "Path to a single song configuration file."
            )]
            public string SongConfigFile { get; set; }

            [Option(
                "songConfigDir",
                SetName = "multiSong",
                Required = false,
                HelpText = "Path to the directory containing song configuration files.",
                Default = "./config"
            )]
            public string SongConfigDir { get; set; }

            [Option(
                "outputDir",
                Required = false,
                HelpText = "The directory to which generated videos should be written.",
                Default = "./output"
            )]
            public string OutputDir { get; set; }
        }
    }
}
