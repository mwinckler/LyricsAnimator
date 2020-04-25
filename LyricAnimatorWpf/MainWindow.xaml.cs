using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using LyricAnimator;
using LyricAnimator.Configuration;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace LyricAnimatorWpf
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

        private const string configFilePath = "./config.json";

        public string PathToFfmpeg
        {
            get => pathToFfmpeg;
            set
            {
                if (pathToFfmpeg == value)
                {
                    return;
                }

                pathToFfmpeg = value;
                NotifyPropertyChanged();
            }
        }

        public string ConfigDirectory
        {
            get => configDirectory;
            set
            {
                if (configDirectory == value)
                {
                    return;
                }

                configDirectory = value;
                NotifyPropertyChanged();
            }
        }

        public string OutputDirectory
        {
            get => outputDirectory;
            set
            {
                if (outputDirectory == value)
                {
                    return;
                }

                outputDirectory = value;
                NotifyPropertyChanged();
            }
        }

        public int Progress
        {
            get => progress;
            private set
            {
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

        public Visibility ConfigurationPageVisibility
        {
            get => configurationPageVisibility;
            set
            {
                if (configurationPageVisibility == value)
                {
                    return;
                }

                configurationPageVisibility = value;
                NotifyPropertyChanged();
            }
        }

        public AppConfiguration Configuration
        {
            get => appConfig;
            set
            {
                if (appConfig == value)
                {
                    return;
                }

                appConfig = value;
                NotifyPropertyChanged();
            }
        }

        public bool RunInParallel { get; set; }
        public bool SaveFrames { get; set; }
        public ICommand CreateVideosCommand { get; }
        public ICommand ToggleConfigurationCommand { get; }
        public ICommand SaveConfigurationCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        private Visibility configurationPageVisibility;
        private AppConfiguration appConfig;
        private int progress;
        private string progressDetails;
        private ConcurrentDictionary<string, float> progresses;
        private string outputDirectory;
        private string configDirectory;
        private string pathToFfmpeg;

        // Something in skia's DrawText is not thread safe, as
        // when multithreading, occasionally a text glyph will be
        // replaced with an unknown character box. This lock is used
        // by all animators to ensure only one attempts to draw text
        // at a time.
        private readonly object skiaTypefaceLock = new object();
        private readonly object configFileLock = new object();
        private readonly string[] autoCommitConfigPropertyNames = new[] {nameof(PathToFfmpeg), nameof(ConfigDirectory), nameof(OutputDirectory)};

        public MainWindowViewModel()
        {
            appConfig = InitializeSystemConfig();

            pathToFfmpeg = appConfig.FfmpegPath;
            configDirectory = appConfig.SongConfigPath;
            outputDirectory = appConfig.OutputPath;

            ConfigurationPageVisibility = Visibility.Collapsed;

            CreateVideosCommand = new Command(async () =>
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

            string cachedAppJson = null;

            ToggleConfigurationCommand = new Command(() =>
            {
                ToggleConfigurationPageVisibility();

                if (ConfigurationPageVisibility == Visibility.Visible)
                {
                    cachedAppJson = SerializeConfiguration(appConfig);
                }
                else
                {
                    appConfig = DeserializeConfiguration(cachedAppJson);
                    NotifyPropertyChanged(nameof(Configuration));
                }
            });

            SaveConfigurationCommand = new Command(() =>
            {
                UpdateConfigurationFile();
                ToggleConfigurationPageVisibility();
            });

            ResetDefaultsCommand = new Command(() =>
            {
                Configuration = new AppConfiguration();
                NotifyPropertyChanged(nameof(Configuration));
            });
        }

        private void ToggleConfigurationPageVisibility()
        {
            ConfigurationPageVisibility = ConfigurationPageVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private static AppConfiguration InitializeSystemConfig()
        {
            if (!File.Exists(configFilePath))
            {
                return new AppConfiguration();
            }

            return JsonConvert.DeserializeObject<AppConfiguration>(File.ReadAllText(configFilePath));
        }

        private void ProcessConfigFile(string filePath, DirectoryInfo outputDir)
        {
            var config = SongConfiguration.LoadFromFile(filePath);
            var pngOutputDir = SaveFrames
                ? Directory.CreateDirectory(Path.Combine(outputDir.FullName, Path.Combine($"png_{config.OutputFilename}")))
                : null;
            new Animator(appConfig, skiaTypefaceLock).Animate(ProgressReporterFactory(config.OutputFilename), config, PathToFfmpeg, outputDir, pngOutputDir?.FullName);
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

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (autoCommitConfigPropertyNames.Contains(propertyName))
            {
                UpdateConfigurationFile();
            }
        }

        private void UpdateConfigurationFile()
        {
            appConfig.FfmpegPath = PathToFfmpeg;
            appConfig.SongConfigPath = ConfigDirectory;
            appConfig.OutputPath = OutputDirectory;

            lock (configFileLock)
            {
                File.WriteAllText(configFilePath, SerializeConfiguration(appConfig));
            }
        }

        private static string SerializeConfiguration(AppConfiguration config) => JsonConvert.SerializeObject(config, Formatting.Indented);

        private static AppConfiguration DeserializeConfiguration(string configJson) => JsonConvert.DeserializeObject<AppConfiguration>(configJson);
    }
}
