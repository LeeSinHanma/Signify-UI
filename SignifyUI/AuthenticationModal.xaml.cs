using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SignifyUI.Services;

namespace SignifyUI
{
    public partial class AuthenticationModal : UserControl
    {
        public AuthenticationModal()
        {
            InitializeComponent();

            // Wire up tab buttons
            btnTabLogin.Click += BtnTabLogin_Click;
            btnTabRegister.Click += BtnTabRegister_Click;
            btnClose.Click += BtnClose_Click;
            btnLogout.Click += btnLogout_Click;

            // Wire up register/login hyperlinks (different event pattern)
            linkSignUp.Click += (s, e) => { LinkSignUp_Click(s, e); e.Handled = true; };
            linkLogin.Click += (s, e) => { LinkLogin_Click(s, e); e.Handled = true; };
        }

        /// <summary>Call when opening the modal so guest vs signed-in layout is correct.</summary>
        public void RefreshForSession()
        {
            if (AuthService.IsLoggedIn)
            {
                borderAuthTabs.Visibility = Visibility.Collapsed;
                guestFormsPanel.Visibility = Visibility.Collapsed;
                accountPanel.Visibility = Visibility.Visible;
                lblModalTitle.Text = "Account";
                lblAccountUsername.Text = AuthService.CurrentUsername ?? string.Empty;
            }
            else
            {
                borderAuthTabs.Visibility = Visibility.Visible;
                guestFormsPanel.Visibility = Visibility.Visible;
                accountPanel.Visibility = Visibility.Collapsed;
                ShowLoginForm();
            }
        }

        // ── TAB SWITCHING ──────────────────────────────────────

        private void BtnTabLogin_Click(object sender, RoutedEventArgs e)
        {
            ShowLoginForm();
        }

        private void BtnTabRegister_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterForm();
        }

        private void ShowLoginForm()
        {
            loginForm.Visibility = Visibility.Visible;
            registerForm.Visibility = Visibility.Collapsed;

            // Update tab indicators
            btnTabLogin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"));
            btnTabRegister.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8888AA"));

            // Update indicator lines
            rectLoginIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B61FF"));
            rectRegisterIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Transparent"));

            lblModalTitle.Text = "Login to Signify";
        }

        private void ShowRegisterForm()
        {
            loginForm.Visibility = Visibility.Collapsed;
            registerForm.Visibility = Visibility.Visible;

            // Update tab indicators
            btnTabLogin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8888AA"));
            btnTabRegister.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"));

            // Update indicator lines
            rectLoginIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Transparent"));
            rectRegisterIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B61FF"));

            lblModalTitle.Text = "Create Account";
        }

        // ── QUICK LINK NAVIGATION ──────────────────────────────

        private void LinkSignUp_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterForm();
        }

        private void LinkLogin_Click(object sender, RoutedEventArgs e)
        {
            ShowLoginForm();
        }

        // ── CLOSE BUTTON ───────────────────────────────────────

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Trigger the RequestClose event so parent can hide the modal
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        // Event to notify parent (HomePage) to close the modal
        public event EventHandler? RequestClose;

        // ── FORM SUBMISSIONS (placeholder) ────────────────────

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtLoginUsername.Text;
            string password = pwdLoginPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Use AuthService to authenticate
            var (success, message) = AuthService.LoginUser(username, password);

            if (success)
            {
                // Clear fields after successful login (session is set in AuthService)
                txtLoginUsername.Clear();
                pwdLoginPassword.Clear();
                RequestClose?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(message, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            string username = txtRegisterUsername.Text;
            string password = pwdRegisterPassword.Password;
            string confirm = pwdRegisterConfirm.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirm))
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("Passwords do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Use AuthService to register
            var (success, message) = AuthService.RegisterUser(username, password);

            if (success)
            {
                MessageBox.Show(message, "Registration Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                // Clear fields after successful registration
                txtRegisterUsername.Clear();
                pwdRegisterPassword.Clear();
                pwdRegisterConfirm.Clear();
                // Switch to login tab
                ShowLoginForm();
            }
            else
            {
                MessageBox.Show(message, "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            AuthService.Logout();
            RefreshForSession();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
