using System;
using System.ComponentModel;
using System.Windows;

namespace Scraper
{
    public partial class MainWindow : Window
    {
        private readonly string PauseLabel = "Pause";
        private readonly string ResumeLabel = "Resume";

        private IFinder Finder { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeFinder();
            Closing += MainWindow_Closing;
        }

        #region Callbacks

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            CleanFinder();
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!IsValidParams())
            {
                MessageBox.Show("Enter the correct data (Url, Search texts and Threads and Urls count)", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                btnStart.IsEnabled = false;
                Finder.MaxThreadsCount = (int)slThreads.Value;
                Finder.MaxPagesCount = (int)slUrls.Value;
                Finder.Start(new Uri(tbUrl.Text.Trim()), tbSearch.Text.Trim());
                lvMain.ItemsSource = Finder.Pages;
            }
            catch (Exception ex)
            {
                ShowMessage($"Can't start. Error: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Finder.State == FinderState.Paused)
                {
                    Finder.Resume();
                }
                else if (Finder.State == FinderState.Work)
                {
                    Finder.Pause();
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Can't Paused\\Resumed. Error: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopFinder();
        }

        private void Finder_StateChanged(object sender, StateChangedEventArgs e)
        {
            tbState.Dispatcher.Invoke(() => tbState.Text = e.State.ToString());
            switch (e.State)
            {
                case FinderState.Work:
                    SetControlsState(true);
                    btnPause.Dispatcher.Invoke(() => btnPause.Content = PauseLabel);
                    break;
                case FinderState.Paused:
                    btnPause.Dispatcher.Invoke(() => btnPause.Content = ResumeLabel);
                    break;
                case FinderState.Stopped:
                    SetControlsState(false);
                    break;
                default:
                    break;
            }
        }

        private void Finder_ProgressChanged(object sender, double e)
        {
            pbProcess.Dispatcher.Invoke(() => pbProcess.Value = Math.Round(e));
        }

        private void lvMain_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lvMain.SelectedIndex != -1)
            {
                try
                {
                    var url = (lvMain.SelectedItem as WebPage)?.Uri.AbsoluteUri;
                    Clipboard.SetText(url);
                }
                catch { }
            }
        }

        #endregion

        #region Private Methods

        private bool IsValidParams()
        {
            return !String.IsNullOrEmpty(tbUrl.Text.Trim())
                && !String.IsNullOrEmpty(tbSearch.Text.Trim())
                && Uri.TryCreate(tbUrl.Text.Trim(), UriKind.Absolute, out Uri tmp) //little trick for validation
                && slThreads.Value <= slUrls.Value;
        }

        private void SetControlsState(bool isWork)
        {
            Dispatcher.Invoke(() => {
                tbUrl.IsReadOnly = isWork;
                tbSearch.IsReadOnly = isWork;
                slThreads.IsEnabled = !isWork;
                slUrls.IsEnabled = !isWork;
                btnPause.IsEnabled = isWork;
                btnStop.IsEnabled = isWork;
                btnStart.IsEnabled = !isWork;
            });
        }

        private void InitializeFinder()
        {
            Finder = new Finder();
            Finder.StateChanged += Finder_StateChanged;
            Finder.ProgressChanged += Finder_ProgressChanged;
            tbState.Text = Finder.State.ToString();
        }

        private void CleanFinder()
        {
            lvMain.ItemsSource = null;
            Finder.StateChanged -= Finder_StateChanged;
            Finder.ProgressChanged -= Finder_ProgressChanged;
            (Finder as IDisposable)?.Dispose();
            Finder = null;
        }

        private void StopFinder()
        {
            try
            {
                Finder.Stop();
            }
            catch (Exception ex)
            {
                ShowMessage($"Can't stop. Error: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowMessage(string message, string title, MessageBoxButton btn, MessageBoxImage img)
        {
            Dispatcher.Invoke(() => 
            {
                MessageBox.Show(message, title, btn, img);
            });
        }

        #endregion
    }
}