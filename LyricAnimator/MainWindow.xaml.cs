using System;
using System.IO;
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

    internal sealed class MainWindowViewModel
    {
        public string PathToFfmpeg { get; set; }
        public string ConfigDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public ICommand Command { get; }

        public MainWindowViewModel()
        {
            PathToFfmpeg = @"c:\Users\mattw\bin\ffmpeg.exe";
            ConfigDirectory = @"C:\tmp\animations\config";
            OutputDirectory = @"C:\tmp\animations\output_new";

            Command = new Command(() =>
            {
                var outputDir = Directory.CreateDirectory(OutputDirectory);
                foreach (var filename in Directory.GetFiles(ConfigDirectory, "*.json"))
                {
                    new Animator().Animate(Configuration.LoadFromFile(filename), PathToFfmpeg, outputDir);
                }
            });
        }
    }

    internal sealed class Command : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action execute;

        public Command(Action execute)
        {
            this.execute = execute;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter) => execute();
    }
}
