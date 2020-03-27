using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LyricAnimator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
        }
    }

    internal sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string PathToFfmpeg { get; set; }
        public string ConfigDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public ICommand Command { get; }

        public int Progress
        {
            get => progress;
            private set {
                if (progress == value)
                {
                    return;
                }

                progress = value;
                NotifyPropertyChanged();
            }
        }

        public string ProgressDetails
        {
            get => progressDetails;
            private set
            {
                if (progressDetails == value)
                {
                    return;
                }

                progressDetails = value;
                NotifyPropertyChanged();
            }
        }

        public bool RunInParallel { get; set; }
        public bool SaveFrames { get; set; }

        private int progress;
        private string progressDetails;
        private ConcurrentDictionary<string, float> progresses;

        public MainWindowViewModel()
        {
            PathToFfmpeg = @"c:\Users\mattw\bin\ffmpeg.exe";
            ConfigDirectory = @"C:\tmp\animations\config";
            OutputDirectory = @"C:\tmp\animations\output_new";

            Command = new Command(async () =>
            {
                var outputDir = Directory.CreateDirectory(OutputDirectory);
                ResetProgress();

                var configurationFiles = Directory.GetFiles(ConfigDirectory, "*.json");

                if (RunInParallel)
                {
                    await Task.Run(() => Parallel.ForEach(configurationFiles, filePath => ProcessConfigFile(filePath, outputDir))).ConfigureAwait(false);
                    return;
                }

                foreach (var filePath in configurationFiles)
                {
                    await Task.Run(() => ProcessConfigFile(filePath, outputDir));
                }
            });
        }

        private void ProcessConfigFile(string filePath, DirectoryInfo outputDir)
        {
            var config = Configuration.LoadFromFile(filePath);
            var pngOutputDir = SaveFrames
                ? Directory.CreateDirectory(Path.Combine(outputDir.FullName, Path.Combine($"png_{config.OutputFilename}")))
                : null;
            new AnimatorSkiaSharp().Animate(ProgressReporterFactory(config.OutputFilename), config, PathToFfmpeg, outputDir, pngOutputDir?.FullName);
        }

        private Action<float> ProgressReporterFactory(string identifier) =>
            progressPercent => UpdateOverallProgress(identifier, progressPercent);

        private void UpdateOverallProgress(string identifier, float progressPercent)
        {
            progresses[identifier] = progressPercent;
            var progressSnapshot = progresses.ToArray();
            Progress = (int)(progressSnapshot.Average(kvp => kvp.Value) * 100);
            ProgressDetails = string.Join(Environment.NewLine, progressSnapshot.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Value:P}  {kvp.Key}"));
        }

        private void ResetProgress()
        {
            progresses = new ConcurrentDictionary<string, float>();
            ProgressDetails = string.Empty;
            Progress = 0;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
