using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Signify.Pages
{
    public partial class LearnPage : Page
    {
        // ── All 26 letter buttons in order ──────────────────────────────
        private List<Button> _letterButtons;

        // Track which button is currently active
        private Button _activeLetterBtn;

        // Styles looked up once at load time
        private Style _styleActive;
        private Style _styleUnlocked;

        // Letter order for Prev / Next navigation
        private static readonly string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private int _currentIndex = 0; // starts on A

        // ────────────────────────────────────────────────────────────────
        public LearnPage()
        {
            InitializeComponent();
            Loaded += LearnPage_Loaded;
        }

        private void LearnPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Cache styles
            _styleActive = (Style)Resources["LetterBtn_Active"];
            _styleUnlocked = (Style)Resources["LetterBtn_Unlocked"];

            // Build ordered list matching the UniformGrid sequence
            _letterButtons = new List<Button>
            {
                btnLetterA, btnLetterB, btnLetterC, btnLetterD, btnLetterE,
                btnLetterF, btnLetterG, btnLetterH, btnLetterI, btnLetterJ,
                btnLetterK, btnLetterL, btnLetterM, btnLetterN, btnLetterO,
                btnLetterP, btnLetterQ, btnLetterR, btnLetterS, btnLetterT,
                btnLetterU, btnLetterV, btnLetterW, btnLetterX, btnLetterY,
                btnLetterZ
            };

            // Set initial active state (A)
            _activeLetterBtn = btnLetterA;
            SelectLetter(0);
        }

        // ── Shared click handler wired to every letter button ────────────
        private void btnLetter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string letter)
            {
                int index = Letters.IndexOf(letter);
                if (index >= 0)
                    SelectLetter(index);
            }
        }

        // ── Previous / Next wired in XAML ────────────────────────────────
        private void btnPrevLetter_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
                SelectLetter(_currentIndex - 1);
        }

        private void btnNextLetter_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < Letters.Length - 1)
                SelectLetter(_currentIndex + 1);
        }

        // ── Core selection logic ─────────────────────────────────────────
        /// <summary>
        /// Activates the letter at <paramref name="index"/>, updating the
        /// sidebar tile styles, the header labels, and any other UI that
        /// depends on the current letter.
        /// </summary>
        private void SelectLetter(int index)
        {
            if (_letterButtons == null || index < 0 || index >= _letterButtons.Count)
                return;

            // Deactivate the previously active button
            if (_activeLetterBtn != null)
                _activeLetterBtn.Style = _styleUnlocked;

            // Activate the new button
            _currentIndex = index;
            _activeLetterBtn = _letterButtons[index];
            _activeLetterBtn.Style = _styleActive;

            char letter = Letters[index];

            // ── Update center-panel labels ──────────────────────────────
            lblLetterTitle.Text = $"Letter {letter}";
            lblDetectedLetter.Text = letter.ToString();

            // ── Difficulty label (simple example mapping) ───────────────
            lblDifficulty.Text = index < 9 ? "BEGINNER"
                               : index < 18 ? "INTERMEDIATE"
                               : "ADVANCED";

            // ── Pro Tip — replace with a real dictionary later ──────────
            lblProTipText.Text = GetProTip(letter);

            // ── Hand-sign image — set Source per letter ──────────────────
            // imgHandSign.Source = new BitmapImage(
            //     new Uri($"pack://application:,,,/Assets/Signs/{letter}.png"));

            // ── Prev / Next button dim logic ────────────────────────────
            btnPrevLetter.Opacity = index == 0 ? 0.35 : 1.0;
            btnNextLetter.Opacity = index == Letters.Length - 1 ? 0.35 : 1.0;
        }

        // ── Placeholder tip dictionary ────────────────────────────────────
        private static string GetProTip(char letter) => letter switch
        {
            'A' => "Keep your thumb pressed against the side of your index finger.",
            'B' => "Hold four fingers straight up and tuck your thumb across your palm.",
            'C' => "Curve your fingers and thumb to form a 'C' shape.",
            'D' => "Touch your middle, ring and pinky to your thumb; point your index up.",
            'E' => "Curl all four fingers down and tuck your thumb underneath.",
            'F' => "Connect your index finger and thumb in a circle; other fingers point up.",
            'G' => "Point your index finger sideways and your thumb outward.",
            'H' => "Extend your index and middle fingers horizontally, side by side.",
            'I' => "Raise only your pinky finger straight up.",
            'J' => "Make the I handshape, then trace a 'J' in the air with your pinky.",
            'K' => "Point index and middle fingers up with your thumb between them.",
            'L' => "Extend your index finger up and thumb out — like an 'L' shape.",
            'M' => "Tuck three fingers over your thumb.",
            'N' => "Tuck two fingers over your thumb.",
            'O' => "Curve all fingers and thumb to form a round 'O'.",
            'P' => "Point your index down with your middle finger extended.",
            'Q' => "Point your index and thumb downward.",
            'R' => "Cross your index and middle fingers.",
            'S' => "Make a fist with your thumb resting over your fingers.",
            'T' => "Place your thumb between your index and middle fingers.",
            'U' => "Hold your index and middle fingers together, pointing up.",
            'V' => "Extend your index and middle fingers in a 'V' or peace sign.",
            'W' => "Extend and spread your index, middle, and ring fingers.",
            'X' => "Hook your index finger like a beckoning gesture.",
            'Y' => "Extend your thumb and pinky finger outward.",
            'Z' => "Use your index finger to trace a 'Z' in the air.",
            _ => "Practice in front of a mirror for best results."
        };

        // ── Back button ───────────────────────────────────────────────────
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
