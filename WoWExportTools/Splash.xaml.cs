using System.Configuration;
using System.Windows;

namespace OBJExporterUI
{
    /// <summary>
    /// Interaction logic for Splash.xaml
    /// </summary>
    public partial class Splash : Window
    {
        //private readonly BackgroundWorker loadWorker = new BackgroundWorker();

        public Splash()
        {
            InitializeComponent();

            CheckFirstRun();

            SplashVersion.Content = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MainWindow win = new MainWindow(this);
            win.Visibility = Visibility.Hidden;
            win.Show();

            //loadWorker.DoWork += LoadWorker_DoWork;
            //loadWorker.ProgressChanged += LoadWorker_ProgressChanged;
        }

        /*private void LoadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            throw new NotImplementedException();
        }*/

        /*private void LoadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
        }*/

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

        /*private void SplashLoad_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string state = (string)e.UserState;
            if (!string.IsNullOrEmpty(state))
                SplashProgressText.Content = state;

            SplashProgressBar.Value = e.ProgressPercentage;
        }*/
    }
}
