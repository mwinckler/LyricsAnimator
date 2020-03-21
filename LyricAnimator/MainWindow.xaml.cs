using System.Windows;

namespace LyricAnimator
{
    public partial class MainWindow : Window
    {
        private Configuration config;

        public MainWindow()
        {
            InitializeComponent();

            config = Configuration.LoadFromFile(@"c:\tmp\animations\config\config.json");

            new Animator().Animate(config);

            Application.Current.Shutdown();
        }
    }
}
