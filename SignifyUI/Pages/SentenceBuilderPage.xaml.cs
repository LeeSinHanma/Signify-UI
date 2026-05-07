using System.Windows.Controls;

// ═══════════════════════════════════════════════════════════════
//  SentenceBuilderPage.xaml.cs  —  Code-Behind
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
    public partial class SentenceBuilderPage : Page
    {
        public SentenceBuilderPage()
        {
            InitializeComponent();

            // ── NAMED CONTROLS REFERENCE ─────────────────────────────
            //
            // HEADER:
            //   btnBack             → calls NavigationService.GoBack() (already wired)
            //   lblAppName          → "Signify" brand text
            //   lblModeTitle        → "Sentence Builder" label
            //   lblLevel            → "LVL 12" badge — update Text when level changes
            //   btnHelp             → opens help overlay
            //   btnSettings         → navigates to Settings page
            //
            // LEFT SIDEBAR:
            //   btnClear            → clears the entire sentence
            //   btnDelete           → removes the last letter/word
            //   btnSpace            → adds a space between words
            //
            // CENTER — TEXT INPUT:
            //   lblSentence         → displays the sentence being built
            //                        Update Text in code-behind as letters are added
            //
            // CENTER — CAMERA + RECOGNITION:
            //   rectCameraFeed      → replace with WritableBitmap / Image source
            //   lblDetectedLetter   → update Text with detected ASL letter
            //   lblConfidence       → update Text with confidence % (e.g. "98% Match")
            //   dotConfidence       → change Fill color based on confidence level
            //                        (green = high, yellow = medium, red = low)
            //
            // CENTER — LETTER TILES:
            //   pnlLetterTiles      → StackPanel holding the letter tiles
            //                        Dynamically add/remove Button children per letter
        }

        // ── NAVIGATION ───────────────────────────────────────────────
        // Wired in XAML: Click="btnBack_Click"
        private void btnBack_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
