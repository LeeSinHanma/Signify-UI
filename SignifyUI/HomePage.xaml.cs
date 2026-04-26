using System.Windows;
using System.Windows.Controls;

namespace SignifyUI
{
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
        }

        private void Freehand_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Freehand clicked");
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new LearnPage());
        }

        private void Sentence_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Sentence Builder clicked");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new CameraTest());
        }
    }
}