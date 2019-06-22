using System.Configuration;
using System.Windows;

namespace WoWExportTools
{
    /// <summary>
    /// Interaction logic for Splash.xaml
    /// </summary>
    public partial class Splash : Window
    {
        public Splash()
        {
            InitializeComponent();

            CheckFirstRun();

            SplashVersion.Content = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MainWindow win = new MainWindow(this);
            win.Visibility = Visibility.Hidden;
            win.Show();
        }

        private void CheckFirstRun()
        {
            if (IsFirstRun())
            {
                ConfigurationWindow cfg = new ConfigurationWindow();
                cfg.ShowDialog();

                ConfigurationManager.RefreshSection("appSettings");
            }

            if (IsFirstRun())
            {
                Close();
            }
        }

        private bool IsFirstRun()
        {
            return bool.Parse(ConfigurationManager.AppSettings["firstrun"]) == true;
        }

        public void SetLoadingStatus(string text, int percent)
        {
            if (text != null)
                SplashProgressText.Content = text;

            SplashProgressBar.Value = percent;
        }
    }
}
