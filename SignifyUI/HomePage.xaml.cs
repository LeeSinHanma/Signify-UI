using Signify.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        // Freehand Mode — navigates to FreehandPage
        private void btnCardFreehand_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new FreehandPage());
        }

        // Learn Mode — navigates to LearnPage
        private void btnCardLearn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new LearnPage());
        }

        // Sentence Builder — navigates to SentenceBuilderPage
        private void btnCardSentenceBuilder_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new SentenceBuilderPage());
        }

        // Settings — navigates to SettingsPage
        private void btnCardSettings_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new SettingsPage());
        }
    }
}
