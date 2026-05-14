using System;
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

namespace Signify.Pages
{
    public partial class SentenceBuilderPage : Page
    {
        private const string SettingsFilePath = "settings.json";
        private static readonly TimeSpan PredictionInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan DwellDuration = TimeSpan.FromSeconds(1.5);

        private static readonly SolidColorBrush ConfidenceHighBrush = new((Color)ColorConverter.ConvertFromString("#00C2BB"));
        private static readonly SolidColorBrush ConfidenceMidBrush = new((Color)ColorConverter.ConvertFromString("#7B61FF"));
        private static readonly SolidColorBrush ConfidenceLowBrush = new((Color)ColorConverter.ConvertFromString("#F5C518"));
        private static readonly SolidColorBrush ConfidenceUnknownBrush = new((Color)ColorConverter.ConvertFromString("#9090AA"));

        private VideoCapture? _capture;
        private CancellationTokenSource? _cancellationTokenSource;
        private HandPredictionClient? _predictionClient;
        private DateTime _lastPredictionAtUtc = DateTime.MinValue;
        private int _predictionInFlight;
        private float _threshold = 0.60f;

        // Sentence state
        private string _currentSentence = string.Empty;

        // Dwell timer state
        private string _dwellCandidate = string.Empty;
        private DateTime _dwellStartUtc = DateTime.MinValue;

        // Commit lock — prevents repeated prints
        private bool _awaitingReset = false;

        public SentenceBuilderPage()
        {
            InitializeComponent();
            this.Loaded += SentenceBuilderPage_Loaded;
            this.Unloaded += SentenceBuilderPage_Unloaded;
        }

        // ── Lifecycle ────────────────────────────────────────────────

        private void SentenceBuilderPage_Loaded(object sender, RoutedEventArgs e)
        {
            _threshold = LoadThresholdFromSettingsFileOrDefault();
            ResetPredictionUi();
            StartCamera();
        }

        private void SentenceBuilderPage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        // ── Navigation ───────────────────────────────────────────────

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        // ── Camera ───────────────────────────────────────────────────

        private void StartCamera()
        {
            if (_capture != null) return;

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
            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token),
                           _cancellationTokenSource.Token);
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
                    Cv2.Flip(frame, frame, FlipMode.Y);
                    Cv2.ImEncode(".jpg", frame, out byte[] imageBytes);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = new MemoryStream(imageBytes);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        imgCameraFeed.Source = bmp;
                        txtCameraPlaceholder.Visibility = Visibility.Collapsed;

                    }, DispatcherPriority.Render);

                    MaybeStartPrediction(imageBytes, token);
                }

                Thread.Sleep(33); // ~30 FPS
            }
        }

        // ── Prediction ───────────────────────────────────────────────

        private void MaybeStartPrediction(byte[] jpegBytes, CancellationToken token)
        {
            if (_predictionClient == null) return;
            if (DateTime.UtcNow - _lastPredictionAtUtc < PredictionInterval) return;
            if (Interlocked.CompareExchange(ref _predictionInFlight, 1, 0) != 0) return;

            _lastPredictionAtUtc = DateTime.UtcNow;

            _ = Task.Run(async () =>
            {
                try
                {
                    var prediction = await _predictionClient.PredictFromBytesAsync(
                        jpegBytes,
                        threshold: _threshold,
                        includeLandmarks: false,
                        smoothWindow: null,
                        cancellationToken: token);

                    await Dispatcher.InvokeAsync(() => UpdatePredictionUi(prediction));
                }
                catch (OperationCanceledException) { }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblDetectedLetter.Text = "-";
                        lblConfidence.Text = "CONFIDENCE: --";
                        dotConfidence.Fill = ConfidenceUnknownBrush;
                        lblConfidence.Foreground = ConfidenceUnknownBrush;
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref _predictionInFlight, 0);
                }
            }, token);
        }

        // ── UI Updates ───────────────────────────────────────────────

        private void UpdatePredictionUi(HandPredictionResponse prediction)
        {
            if (!prediction.HandDetected)
            {
                ResetPredictionUi();
                return;
            }

            // ── These declarations must come first ──
            var (bestLabel, bestProbability) = GetTopProbability(prediction.Probabilities);
            string displayedLabel = NormalizeDisplayedLabel(prediction.Label, bestLabel);
            float displayedConfidence = bestProbability > 0f
                                          ? bestProbability
                                          : NormalizeDisplayedConfidence(prediction.Confidence, 0f);

            lblDetectedLetter.Text = displayedLabel;
            lblConfidence.Text = $"CONFIDENCE: {displayedConfidence * 100f:0.0}%";
            var brush = GetConfidenceBrush(displayedConfidence);
            dotConfidence.Fill = brush;
            lblConfidence.Foreground = brush;

            // ── Locked out — waiting for confidence to drop ──
            if (_awaitingReset)
            {
                if (displayedConfidence < _threshold)
                    _awaitingReset = false;  // 🔓 unlock

                UpdateDwellProgressUi(0d);
                return;
            }

            // ── Dwell timer: commit only after holding the same letter ──
            if (displayedConfidence >= _threshold
                && displayedLabel.Length == 1
                && char.IsLetter(displayedLabel[0]))
            {
                string letter = displayedLabel.ToUpperInvariant();

                if (letter != _dwellCandidate)
                {
                    // New letter — restart the dwell clock
                    _dwellCandidate = letter;
                    _dwellStartUtc = DateTime.UtcNow;
                    UpdateDwellProgressUi(0d);
                }
                else
                {
                    // Same letter — check if dwell period has elapsed
                    double elapsed = (DateTime.UtcNow - _dwellStartUtc).TotalSeconds;
                    double progress = Math.Clamp(elapsed / DwellDuration.TotalSeconds, 0d, 1d);
                    UpdateDwellProgressUi(progress);

                    if (elapsed >= DwellDuration.TotalSeconds)
                    {
                        // ✅ Commit the letter
                        _currentSentence += letter;
                        lblSentence.Text = _currentSentence;
                        RefreshLetterTiles();

                        // 🔒 Lock out until confidence drops
                        _awaitingReset = true;
                        _dwellCandidate = string.Empty;
                        _dwellStartUtc = DateTime.MinValue;
                        UpdateDwellProgressUi(0d);
                    }
                }
            }
            else
            {
                // Confidence too low or invalid label — reset dwell
                _dwellCandidate = string.Empty;
                _dwellStartUtc = DateTime.MinValue;
                UpdateDwellProgressUi(0d);
            }
        }

        private void ResetPredictionUi()
        {
            lblDetectedLetter.Text = "-";
            lblConfidence.Text = "CONFIDENCE: --";
            dotConfidence.Fill = ConfidenceUnknownBrush;
            lblConfidence.Foreground = ConfidenceUnknownBrush;

            // Reset dwell and unlock
            _awaitingReset = false;
            _dwellCandidate = string.Empty;
            _dwellStartUtc = DateTime.MinValue;
            dotConfidence.Width = 12;
            dotConfidence.Height = 12;
            UpdateDwellProgressUi(0d);
        }

        private void UpdateDwellProgressUi(double progress)
        {
            double size = 12 + (progress * 12);
            dotConfidence.Width = size;
            dotConfidence.Height = size;

            lblDetectedLetter.Foreground = progress >= 1d
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(
                    217,
                    (byte)(123 + (progress * 132)),
                    (byte)(32 + (progress * 223))));
        }

        private void RefreshLetterTiles()
        {
            pnlLetterTiles.Children.Clear();

            string currentWord = _currentSentence.Length == 0
                ? string.Empty
                : _currentSentence.Split(' ').Last();

            foreach (char ch in currentWord)
            {
                var tile = new Button
                {
                    Style = (Style)FindResource("LetterTile_Active"),
                    Content = new TextBlock
                    {
                        Text = ch.ToString(),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                pnlLetterTiles.Children.Add(tile);
            }

            for (int i = 0; i < 2; i++)
            {
                var empty = new Button { Style = (Style)FindResource("LetterTile_Empty") };
                pnlLetterTiles.Children.Add(empty);
            }
        }

        // ── Sentence Controls ────────────────────────────────────────

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            _currentSentence = string.Empty;
            lblSentence.Text = string.Empty;
            _awaitingReset = false;
            _dwellCandidate = string.Empty;
            _dwellStartUtc = DateTime.MinValue;
            RefreshLetterTiles();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSentence.Length == 0) return;
            _currentSentence = _currentSentence[..^1];
            lblSentence.Text = _currentSentence;
            RefreshLetterTiles();
        }

        private void btnSpace_Click(object sender, RoutedEventArgs e)
        {
            _currentSentence += " ";
            lblSentence.Text = _currentSentence;
            RefreshLetterTiles();
        }

        // ── Static Helpers ───────────────────────────────────────────

        private static (string Label, float Probability) GetTopProbability(
            Dictionary<string, float>? probabilities)
        {
            if (probabilities == null || probabilities.Count == 0)
                return ("-", 0f);

            var best = probabilities.OrderByDescending(kv => kv.Value).First();
            return (string.IsNullOrWhiteSpace(best.Key) ? "-" : best.Key.Trim(), best.Value);
        }

        private static string NormalizeDisplayedLabel(string labelFromApi, string fallbackLabel)
        {
            var label = (labelFromApi ?? string.Empty).Trim();
            if (label.Length == 1 && char.IsLetter(label[0]))
                return label.ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(fallbackLabel) && fallbackLabel != "-")
                return fallbackLabel.Length == 1 ? fallbackLabel.ToUpperInvariant() : fallbackLabel;

            return "-";
        }

        private static float NormalizeDisplayedConfidence(float confidenceFromApi, float fallback)
        {
            float c = confidenceFromApi <= 0f ? fallback : confidenceFromApi;
            if (c > 1f && c <= 100f) c /= 100f;
            return Math.Clamp(c, 0f, 1f);
        }

        private static SolidColorBrush GetConfidenceBrush(float confidence) => confidence switch
        {
            <= 0f => ConfidenceUnknownBrush,
            >= 0.85f => ConfidenceHighBrush,
            >= 0.65f => ConfidenceMidBrush,
            _ => ConfidenceLowBrush
        };

        private static float LoadThresholdFromSettingsFileOrDefault(float def = 0.60f)
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return def;

                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFilePath));
                if (!doc.RootElement.TryGetProperty("ConfidenceThreshold", out var el))
                    return def;

                double pct = el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetDouble(),
                    JsonValueKind.String when double.TryParse(el.GetString(), out var p) => p,
                    _ => def * 100d
                };

                return (float)(Math.Clamp(pct, 0d, 100d) / 100d);
            }
            catch { return def; }
        }
    }
}