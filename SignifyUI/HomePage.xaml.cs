using Signify.Pages;
using SignifyUI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

// ═══════════════════════════════════════════════════════════════
//  HomePage.xaml.cs
//
//  Card hover: on MouseEnter we tint the card border with the
//  card's own accent color so the glow matches the accent strip.
//  On MouseLeave we restore the default border.
//
//  Navigation: Freehand and Learn are wired. Sentence Builder
//  and Settings show a placeholder MessageBox until those pages
//  are built.
//
//  Authentication Modal: Profile button triggers modal overlay
//  with blur background effect and centered login/register form.
// ═══════════════════════════════════════════════════════════════

namespace SignifyUI
{
    public partial class HomePage : Page
    {
        // Each card's accent color — matches the top strip in the XAML
        private static readonly SolidColorBrush AccentTeal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C2BB"));
        private static readonly SolidColorBrush AccentPurple = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7B61FF"));
        private static readonly SolidColorBrush AccentAmber = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        private static readonly SolidColorBrush AccentSlate = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        private static readonly SolidColorBrush DefaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E36"));

        public HomePage()
        {
            InitializeComponent();

            // Wire accent-color hover glow per card
            WireHoverGlow(btnCardFreehand, AccentTeal);
            WireHoverGlow(btnCardLearn, AccentPurple);
            WireHoverGlow(btnCardSentenceBuilder, AccentAmber);
            WireHoverGlow(btnCardSettings, AccentSlate);

            // Wire profile button to show authentication modal
            btnProfile.Click += BtnProfile_Click;

            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            AuthService.SessionChanged -= AuthService_SessionChanged;
            AuthService.SessionChanged += AuthService_SessionChanged;
            if (authModal != null)
            {
                authModal.RequestClose -= AuthModal_RequestClose;
                authModal.RequestClose += AuthModal_RequestClose;
            }

            RefreshWelcomeForSession();
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            AuthService.SessionChanged -= AuthService_SessionChanged;
            if (authModal != null)
            {
                authModal.RequestClose -= AuthModal_RequestClose;
            }
        }

        private void AuthService_SessionChanged(object? sender, EventArgs e) => RefreshWelcomeForSession();

        private void AuthModal_RequestClose(object? sender, EventArgs e) => HideAuthenticationModal();

        private void RefreshWelcomeForSession()
        {
            if (AuthService.IsLoggedIn && !string.IsNullOrWhiteSpace(AuthService.CurrentUsername))
            {
                runWelcome.Text = "Welcome back, ";
                runAppTitle.Text = $"{AuthService.CurrentUsername}!";
            }
            else
            {
                runWelcome.Text = "Welcome to ";
                runAppTitle.Text = "Signify!";
            }
        }

        // ── Hover glow helper ─────────────────────────────────────
        // On MouseEnter: sets the card's BorderBrush to its accent color
        // On MouseLeave: restores the default subtle border
        // This runs alongside the XAML trigger (which handles bg + thickness)
        private void WireHoverGlow(Button card, SolidColorBrush accentColor)
        {
            card.MouseEnter += (s, e) => card.BorderBrush = accentColor;
            card.MouseLeave += (s, e) => card.BorderBrush = DefaultBorder;
        }

        // ── CARD NAVIGATION ───────────────────────────────────────

        // Helper to check authentication
        private bool EnsureAuthenticated()
        {
            if (!AuthService.IsLoggedIn)
            {
                ShowAuthenticationModal();
                return false;
            }
            return true;
        }

        // Freehand Mode — navigates to FreehandPage
        private void btnCardFreehand_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureAuthenticated())
            {
                NavigationService.Navigate(new FreehandPage());
            }
        }

        // Learn Mode — navigates to LearnPage
        private void btnCardLearn_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureAuthenticated())
            {
                NavigationService.Navigate(new LearnPage());
            }
        }

        // Sentence Builder — navigates to SentenceBuilderPage
        private void btnCardSentenceBuilder_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureAuthenticated())
            {
                NavigationService.Navigate(new SentenceBuilderPage());
            }
        }

        // Settings — navigates to SettingsPage
        private void btnCardSettings_Click(object sender, RoutedEventArgs e)
        {
            if (EnsureAuthenticated())
            {
                NavigationService.Navigate(new SettingsPage());
            }
        }

        // ── AUTHENTICATION MODAL ──────────────────────────────────

        private void BtnProfile_Click(object sender, RoutedEventArgs e)
        {
            ShowAuthenticationModal();
        }

        private void ShowAuthenticationModal()
        {
            authModal?.RefreshForSession();

            // Show blur overlay
            modalOverlay.Visibility = Visibility.Visible;
            modalOverlay.Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));

            // Show modal container
            modalContainer.Visibility = Visibility.Visible;

            // Apply blur to main content area (optional enhancement)
            // This blurs everything behind the modal
            var grid = this.Content as Grid;
            if (grid != null)
            {
                var contentGrid = grid.Children[1] as Grid; // Row 1 (main content)
                if (contentGrid != null && contentGrid.Effect == null)
                {
                    contentGrid.Effect = new BlurEffect { Radius = 6 };
                }
            }
        }

        private void HideAuthenticationModal()
        {
            // Hide blur overlay
            modalOverlay.Visibility = Visibility.Collapsed;

            // Hide modal container
            modalContainer.Visibility = Visibility.Collapsed;

            // Remove blur from content
            var grid = this.Content as Grid;
            if (grid != null)
            {
                var contentGrid = grid.Children[1] as Grid;
                if (contentGrid != null)
                {
                    contentGrid.Effect = null;
                }
            }

            RefreshWelcomeForSession();
        }

        // Close modal when clicking outside (on the overlay)
        private void ModalOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only close if clicking directly on the overlay (not the modal)
            if (e.Source == modalOverlay)
            {
                HideAuthenticationModal();
            }
        }
    }
}
