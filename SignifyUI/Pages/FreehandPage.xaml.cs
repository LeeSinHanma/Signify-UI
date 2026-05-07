using System.Windows.Controls;

// ═══════════════════════════════════════════════════════════════
//  FreehandPage.xaml.cs  —  Code-Behind  (UI scaffold only)
//
//  All controls are named in the XAML with the "x:Name" attribute.
//  No event handlers or backend logic have been added yet.
//  Add your recognition logic, camera feed, and data binding here
//  once the backend is ready.
// ═══════════════════════════════════════════════════════════════

namespace Signify.Pages
{
    public partial class FreehandPage : Page
    {
        public FreehandPage()
        {
            InitializeComponent();

            // ── PLACEHOLDER: UI control references are all ready to use ──
            //
            // Header controls:
            //   lblAppName         → "SignQuest" brand text
            //   lblModeTitle       → "FREEHAND MODE" label
            //   btnHelp            → Opens help dialog
            //   btnSettings        → Navigates to Settings page
            //
            // Left sidebar:
            //   btnBack            → Go back to main menu
            //   btnCameraToggle    → Enable / disable camera
            //   btnScreenshot      → Capture current frame
            //   btnBrightness      → Open brightness slider
            //
            // Camera & detection:
            //   rectCameraFeed     → Replace with WritableBitmap or Image source
            //   pnlDetectionBox    → Glowing bounding box overlay on camera feed
            //
            // Predicted letter panel:
            //   lblPredictedLetter → Update Text to the detected ASL letter
            //   lblConfidence      → Update Text e.g. "CONFIDENCE: 98.4%"
            //   dotConfidence      → Change Fill color based on confidence level
            //                        (green = high, yellow = medium, red = low)
            //
            // New record badge:
            //   pnlNewRecord       → Toggle Visibility = Visible / Collapsed
            //
            // Session recap:
            //   lblSignsSent       → Update Text with signs count
            //   lblAccuracy        → Update Text with accuracy percentage
            //   lblSessionTime     → Update Text with elapsed time (MM:SS)
            //
            // Suggested letters:
            //   pnlSuggestedLetters  → WrapPanel; add/remove LetterChipBtn children
            //   btnSuggestA/E/I/O    → Pre-loaded example chips
            //
            // Bottom action buttons:
            //   btnClear           → Reset detection state
            //   btnSubmitSign      → Confirm and log the detected sign
            //   btnLog             → Open sign history / log panel
        }
            private void btnBack_Click(object sender, System.Windows.RoutedEventArgs e)
            {
            // CanGoBack guards against pressing Back on the very first page,
            // which would cause a NavigationService exception.
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
            }
           
    }
    
}
