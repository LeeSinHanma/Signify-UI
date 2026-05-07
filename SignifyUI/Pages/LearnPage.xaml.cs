using System.Windows.Controls;

// ═══════════════════════════════════════════════════════════════
//  LearnPage.xaml.cs  —  Code-Behind
//
//  The BACK BUTTON is the only wired interaction right now.
//  It uses WPF's built-in NavigationService to go back in the
//  Frame's history — same Frame your MainWindow uses to host pages.
//
//  All other named controls are documented below for when
//  backend/logic work begins.
// ═══════════════════════════════════════════════════════════════

namespace Signify.Pages
{
    public partial class LearnPage : Page
    {
        public LearnPage()
        {
            InitializeComponent();

            // ── NAMED CONTROLS REFERENCE ─────────────────────────────
            //
            // HEADER:
            //   btnBack             → calls NavigationService.GoBack() (already wired)
            //   lblAppName          → "SignQuest" brand text
            //   btnTabPractice      → Practice tab (currently active)
            //   btnTabQuest         → Quest tab
            //   btnTabStore         → Store tab
            //   btnHelp             → opens help overlay
            //   btnSettings         → navigates to Settings page
            //
            // LEFT — ALPHABET LIBRARY:
            //   lblMastery          → e.g. "MASTERY: 12 / 26" — update Text
            //   gridAlphabet        → UniformGrid holding all letter buttons
            //   btnLetterA ... Z    → individual letter tiles
            //                         Change Style to LetterBtn_Active / Unlocked / Locked
            //
            // CENTER — LETTER DETAIL:
            //   lblLetterTitle      → "Letter A" — update Text per selection
            //   lblDifficulty       → "BEGINNER" badge text
            //   imgHandSign         → set Source to load hand sign image asset
            //   btnPrevLetter       → navigate to previous letter
            //   btnNextLetter       → navigate to next letter
            //   lblProTipText       → tip text below the image card
            //   btnPlayTipAudio     → trigger audio playback
            //
            // RIGHT — CAMERA + AI + STREAK:
            //   rectCameraFeed      → replace with WritableBitmap / Image source
            //   dotLive             → red pulsing dot; animate Fill in code-behind
            //   btnCameraScreenshot → capture camera frame
            //   lblMatchPct         → "Matching: 89%" — update from model output
            //   lblDetectedLetter   → detected ASL letter — update Text
            //   lblMatchCheck       → "✔" — toggle Visibility on match
            //   lblXpEarned         → "124" XP value — update Text
            //   btnStreakMon/Tue/Wed/Thu/Fri → day streak chips
        }

        // ── BACK BUTTON — FULLY WORKING ──────────────────────────────
        // Navigates back to the previous page (your main menu) using the
        // same Frame that hosts this page. No other logic needed here.
        private void btnBack_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // CanGoBack guards against pressing Back on the very first page,
            // which would cause a NavigationService exception.
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }
    }
}
