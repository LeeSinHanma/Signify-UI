using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using SignifyUI;
using SignifyUI.Services;

namespace Signify.Pages
{
    public partial class LearnPage : Page
    {
        private const int MaxLetterProgress = 10;
        private const int LetterCount = 26;
        private static readonly TimeSpan PredictionInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan LetterProgressIncrementCooldown = TimeSpan.FromSeconds(2.0);
        private const float LetterProgressMinConfidence = 0.65f;

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

        /// <summary>Per-letter progress 0–10. 0 means no qualifying successful match yet.</summary>
        private readonly int[] _letterProgress = new int[LetterCount];

        private readonly DateTime[] _lastLetterProgressIncrementUtc = new DateTime[LetterCount];
        private TextBlock[]? _letterTileProgressLabels;
        private static readonly SolidColorBrush TileProgressBrush = new(Color.FromRgb(0x88, 0x88, 0xAA));

        // Camera and prediction fields
        private VideoCapture? _capture;
        private CancellationTokenSource? _cancellationTokenSource;
        private HandPredictionClient? _predictionClient;
        private DateTime _lastPredictionAtUtc = DateTime.MinValue;
        private int _predictionInFlight;
        private float _threshold = 0.60f;

        // ────────────────────────────────────────────────────────────────
        public LearnPage()
        {
            InitializeComponent();
            Loaded += LearnPage_Loaded;
            Unloaded += LearnPage_Unloaded;
        }

        private void AuthService_SessionChanged(object? sender, EventArgs e)
        {
            if (_letterButtons == null)
            {
                return;
            }

            _ = Dispatcher.InvokeAsync(() =>
            {
                LoadLetterProgress();
                RefreshLetterProgressUi();
                UpdateLetterProgressDetailLabel(_currentIndex);
            });
        }

        private void LearnPage_Loaded(object sender, RoutedEventArgs e)
        {
            AuthService.SessionChanged -= AuthService_SessionChanged;
            AuthService.SessionChanged += AuthService_SessionChanged;

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

            LoadLetterProgress();
            EnsureLetterTileProgressLabels();
            RefreshLetterProgressUi();

            // Set initial active state (A)
            _activeLetterBtn = btnLetterA;
            SelectLetter(0);

            RefreshUserDisplay();
            ResetAiRecognitionPanel();
            StartCamera();
        }

        private void LearnPage_Unloaded(object sender, RoutedEventArgs e)
        {
            AuthService.SessionChanged -= AuthService_SessionChanged;
            SaveLetterProgress();
            StopCamera();
        }

        private void RefreshUserDisplay()
        {
            if (AuthService.IsLoggedIn && !string.IsNullOrWhiteSpace(AuthService.CurrentUsername))
            {
                txtUserDisplay.Text = $"User: {AuthService.CurrentUsername}";
            }
            else
            {
                txtUserDisplay.Text = "";
            }
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

            UpdateLetterProgressDetailLabel(index);
            ResetAiRecognitionPanel();

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

        private void StartCamera()
        {
            if (_capture != null)
                return;

            _predictionClient ??= new HandPredictionClient();

            _capture = new VideoCapture(0);
            _capture.Set(VideoCaptureProperties.FrameWidth, 640);
            _capture.Set(VideoCaptureProperties.FrameHeight, 480);

            if (!_capture.IsOpened())
            {
                Dispatcher.Invoke(() =>
                {
                    txtCameraPlaceholder.Text = "[ Unable to open camera ]";
                    txtCameraPlaceholder.Visibility = Visibility.Visible;
                });

                _capture.Dispose();
                _capture = null;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            _ = Task.Run(() => CaptureLoop(token), token);
        }

        private void StopCamera()
        {
            _cancellationTokenSource?.Cancel();
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _predictionClient?.Dispose();
            _predictionClient = null;
        }

        private void CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();

            while (!token.IsCancellationRequested && _capture != null && _capture.IsOpened())
            {
                if (_capture.Read(frame) && !frame.Empty())
                {
                    Cv2.ImEncode(".jpg", frame, out byte[] imageBytes);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = new MemoryStream(imageBytes);
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        imgCameraFeed.Source = bitmapImage;
                        txtCameraPlaceholder.Visibility = Visibility.Collapsed;
                    }, DispatcherPriority.Render);

                    MaybeStartPrediction(imageBytes, token);
                }

                Thread.Sleep(33);
            }
        }

        private void MaybeStartPrediction(byte[] jpegBytes, CancellationToken token)
        {
            if (_predictionClient == null)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastPredictionAtUtc < PredictionInterval)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _predictionInFlight, 1, 0) != 0)
            {
                return;
            }

            _lastPredictionAtUtc = nowUtc;
            var bytesForPrediction = jpegBytes;

            _ = Task.Run(async () =>
            {
                try
                {
                    HandPredictionResponse prediction = await _predictionClient.PredictFromBytesAsync(
                        bytesForPrediction,
                        threshold: _threshold,
                        includeLandmarks: false,
                        smoothWindow: null,
                        cancellationToken: token);

                    await Dispatcher.InvokeAsync(() => UpdateAiRecognitionFromPrediction(prediction));
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblMatchPct.Text = "Recognition: unavailable";
                        lblMatchCheck.Text = "—";
                        lblMatchCheck.Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xAA));
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref _predictionInFlight, 0);
                }
            }, token);
        }

        private void ResetAiRecognitionPanel()
        {
            lblMatchPct.Text = "Confidence: —";
            lblMatchCheck.Text = "·";
            lblMatchCheck.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88));
        }

        private void UpdateAiRecognitionFromPrediction(HandPredictionResponse prediction)
        {
            char target = Letters[_currentIndex];

            if (!prediction.HandDetected)
            {
                lblMatchPct.Text = "Show your hand to the camera";
                lblMatchCheck.Text = "·";
                lblMatchCheck.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88));
                return;
            }

            (string bestLabel, float bestProbability) = GetTopProbability(prediction.Probabilities);
            string displayed = NormalizeDisplayedLabel(prediction.Label, bestLabel);
            float confidence = bestProbability > 0f
                ? bestProbability
                : NormalizeDisplayedConfidence(prediction.Confidence, 0f);

            lblMatchPct.Text = $"Confidence: {confidence * 100f:0.0}%";

            bool matchesTarget = displayed.Length == 1
                && char.ToUpperInvariant(displayed[0]) == target;

            if (matchesTarget)
            {
                lblMatchCheck.Text = "✔";
                lblMatchCheck.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xFF, 0xA0));
                MaybeIncrementLetterProgress(_currentIndex, confidence);
            }
            else
            {
                lblMatchCheck.Text = displayed == "-" ? "?" : "✗";
                lblMatchCheck.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x88));
            }
        }

        private void MaybeIncrementLetterProgress(int letterIndex, float confidence)
        {
            if (!AuthService.IsLoggedIn)
            {
                return;
            }

            if (letterIndex < 0 || letterIndex >= LetterCount)
            {
                return;
            }

            if (confidence < LetterProgressMinConfidence || _letterProgress[letterIndex] >= MaxLetterProgress)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (now - _lastLetterProgressIncrementUtc[letterIndex] < LetterProgressIncrementCooldown)
            {
                return;
            }

            _letterProgress[letterIndex]++;
            _lastLetterProgressIncrementUtc[letterIndex] = now;
            RefreshLetterProgressUi(letterIndex);
            SaveLetterProgress();
        }

        private void EnsureLetterTileProgressLabels()
        {
            if (_letterTileProgressLabels != null || _letterButtons == null)
            {
                return;
            }

            _letterTileProgressLabels = new TextBlock[LetterCount];
            for (int i = 0; i < LetterCount; i++)
            {
                if (_letterButtons[i].Content is not StackPanel stack)
                {
                    continue;
                }

                var label = new TextBlock
                {
                    FontSize = 9,
                    Foreground = TileProgressBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                };
                stack.Children.Add(label);
                _letterTileProgressLabels[i] = label;
            }
        }

        private void RefreshLetterProgressUi(int? changedIndexOnly = null)
        {
            if (_letterTileProgressLabels != null)
            {
                if (changedIndexOnly.HasValue)
                {
                    int i = changedIndexOnly.Value;
                    if (i >= 0 && i < LetterCount && i < _letterTileProgressLabels.Length)
                    {
                        _letterTileProgressLabels[i].Text = FormatTileProgress(_letterProgress[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < LetterCount && i < _letterTileProgressLabels.Length; i++)
                    {
                        _letterTileProgressLabels[i].Text = FormatTileProgress(_letterProgress[i]);
                    }
                }
            }

            for (int i = 0; i < LetterCount && i < _letterButtons.Count; i++)
            {
                int p = _letterProgress[i];
                _letterButtons[i].ToolTip = $"Letter {Letters[i]} — progress {p} / {MaxLetterProgress}";
            }

            int mastered = _letterProgress.Count(p => p >= MaxLetterProgress);
            lblMastery.Text = $"MASTERY: {mastered} / {LetterCount} at level {MaxLetterProgress}";

            UpdateLetterProgressDetailLabel(_currentIndex);
        }

        private static string FormatTileProgress(int level)
        {
            return $"{Math.Clamp(level, 0, MaxLetterProgress)}/{MaxLetterProgress}";
        }

        private void UpdateLetterProgressDetailLabel(int letterIndex)
        {
            int p = _letterProgress[Math.Clamp(letterIndex, 0, LetterCount - 1)];
            string status = p == 0 ? "not started" : p >= MaxLetterProgress ? "complete" : "in progress";
            lblLetterProgressDetail.Text = $"Progress: {p} / {MaxLetterProgress} ({status})";
        }

        private void LoadLetterProgress()
        {
            Array.Clear(_letterProgress, 0, LetterCount);
            for (int i = 0; i < LetterCount; i++)
            {
                _lastLetterProgressIncrementUtc[i] = DateTime.MinValue;
            }

            string? path = AuthService.GetLearnProgressFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Levels", out JsonElement levels) ||
                    levels.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                int i = 0;
                foreach (JsonElement el in levels.EnumerateArray())
                {
                    if (i >= LetterCount)
                    {
                        break;
                    }

                    int v = el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;
                    _letterProgress[i] = Math.Clamp(v, 0, MaxLetterProgress);
                    i++;
                }
            }
            catch
            {
                // Keep cleared defaults.
            }
        }

        private void SaveLetterProgress()
        {
            string? path = AuthService.GetLearnProgressFilePath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var payload = new { Levels = _letterProgress.ToArray() };
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignore disk errors.
            }
        }

        private static (string Label, float Probability) GetTopProbability(Dictionary<string, float>? probabilities)
        {
            if (probabilities == null || probabilities.Count == 0)
            {
                return ("-", 0f);
            }

            KeyValuePair<string, float> best = probabilities.OrderByDescending(kv => kv.Value).First();
            string label = string.IsNullOrWhiteSpace(best.Key) ? "-" : best.Key.Trim();
            return (label, best.Value);
        }

        private static string NormalizeDisplayedLabel(string labelFromApi, string fallbackLabel)
        {
            string label = (labelFromApi ?? string.Empty).Trim();

            if (label.Length == 1 && char.IsLetter(label[0]))
            {
                return label.ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(fallbackLabel) && fallbackLabel != "-")
            {
                return fallbackLabel.Length == 1 ? fallbackLabel.ToUpperInvariant() : fallbackLabel;
            }

            return "-";
        }

        private static float NormalizeDisplayedConfidence(float confidenceFromApi, float fallbackProbability)
        {
            float confidence = confidenceFromApi;

            if (confidence <= 0f)
            {
                confidence = fallbackProbability;
            }

            if (confidence > 1f && confidence <= 100f)
            {
                confidence /= 100f;
            }

            return Math.Clamp(confidence, 0f, 1f);
        }
    }
}
