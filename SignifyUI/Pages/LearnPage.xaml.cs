using System.Windows;
using System.Windows.Controls;

namespace SignifyUI
{
    public partial class LearnPage : Page
    {
        public LearnPage()
        {
            InitializeComponent();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new HomePage());
        }

        private void TopSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Top settings clicked");
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Profile clicked");
        }

        private void LetterA_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Letter A clicked");
        }
    }
}