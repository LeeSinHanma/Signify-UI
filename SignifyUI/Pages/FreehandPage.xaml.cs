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
        private const string SettingsFilePath = "settings.json";
        private static readonly TimeSpan PredictionInterval = TimeSpan.FromMilliseconds(250);

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

        public FreehandPage()
        {
            InitializeComponent();
            this.Loaded += FreehandPage_Loaded;
            this.Unloaded += FreehandPage_Unloaded;

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

                    private void FreehandPage_Loaded(object sender, RoutedEventArgs e)
                    {
                        _threshold = LoadThresholdFromSettingsFileOrDefault();
                        ResetPredictionUi();
                        StartCamera();
                    }

                    private void FreehandPage_Unloaded(object sender, RoutedEventArgs e)
                    {
                        StopCamera();
                    }

                    private void StartCamera()
                    {
                        if (_capture != null)
                        {
                            return;
                        }

                        _predictionClient ??= new HandPredictionClient();

                        int activeCameraIndex = LoadCameraIndexFromSettingsFileOrDefault();
                        _capture = new VideoCapture(activeCameraIndex);
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

                        // Run the capture loop on a background thread
                        Task.Run(() => CaptureLoop(token), token);
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
                    var prediction = await _predictionClient.PredictFromBytesAsync(
                        bytesForPrediction,
                        threshold: _threshold,
                        includeLandmarks: false,
                        smoothWindow: null,
                        cancellationToken: token);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdatePredictionUi(prediction);
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblPredictedLetter.Text = "-";
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

        private void UpdatePredictionUi(HandPredictionResponse prediction)
        {
            if (!prediction.HandDetected)
            {
                lblPredictedLetter.Text = "-";
                lblConfidence.Text = "CONFIDENCE: --";
                dotConfidence.Fill = ConfidenceUnknownBrush;
                lblConfidence.Foreground = ConfidenceUnknownBrush;
                HideSuggestedLetters();
                return;
            }

            var (bestLabel, bestProbability) = GetTopProbability(prediction.Probabilities);

            string displayedLabel = NormalizeDisplayedLabel(prediction.Label, bestLabel);

            // Prefer probabilities for the displayed confidence when available.
            // Many backends compute a "confidence" field differently (or as 0/1),
            // while probabilities reflect the actual distribution.
            float displayedConfidence = bestProbability > 0f ? bestProbability : NormalizeDisplayedConfidence(prediction.Confidence, 0f);

            lblPredictedLetter.Text = displayedLabel;

            var confidencePercent = displayedConfidence * 100f;
            lblConfidence.Text = $"CONFIDENCE: {confidencePercent:0.0}%";

            var brush = GetConfidenceBrush(displayedConfidence);
            dotConfidence.Fill = brush;
            lblConfidence.Foreground = brush;

            UpdateSuggestedLetters(prediction.Probabilities);
        }

        private void ResetPredictionUi()
        {
            lblPredictedLetter.Text = "-";
            lblConfidence.Text = "CONFIDENCE: --";
            dotConfidence.Fill = ConfidenceUnknownBrush;
            lblConfidence.Foreground = ConfidenceUnknownBrush;
            HideSuggestedLetters();
        }

        private void UpdateSuggestedLetters(Dictionary<string, float>? probabilities)
        {
            if (probabilities == null || probabilities.Count == 0)
            {
                HideSuggestedLetters();
                return;
            }

            var top = probabilities
                .OrderByDescending(kv => kv.Value)
                .Take(4)
                .ToArray();

            UpdateSuggestedButton(btnSuggestA, top, 0);
            UpdateSuggestedButton(btnSuggestE, top, 1);
            UpdateSuggestedButton(btnSuggestI, top, 2);
            UpdateSuggestedButton(btnSuggestO, top, 3);
        }

        private void HideSuggestedLetters()
        {
            btnSuggestA.Visibility = Visibility.Collapsed;
            btnSuggestE.Visibility = Visibility.Collapsed;
            btnSuggestI.Visibility = Visibility.Collapsed;
            btnSuggestO.Visibility = Visibility.Collapsed;
        }

        private static (string Label, float Probability) GetTopProbability(Dictionary<string, float>? probabilities)
        {
            if (probabilities == null || probabilities.Count == 0)
            {
                return ("-", 0f);
            }

            var best = probabilities.OrderByDescending(kv => kv.Value).First();
            var label = string.IsNullOrWhiteSpace(best.Key) ? "-" : best.Key.Trim();
            return (label, best.Value);
        }

        private static string NormalizeDisplayedLabel(string labelFromApi, string fallbackLabel)
        {
            var label = (labelFromApi ?? string.Empty).Trim();

            // Expect single-letter labels (A-Z). If backend returns multiple letters
            // (e.g., "ALYZ"), show the top-1 probability label instead.
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

            // Some backends omit confidence or send 0 while still providing probabilities.
            if (confidence <= 0f)
            {
                confidence = fallbackProbability;
            }

            // If a backend returns percent (0-100), normalize to 0-1.
            if (confidence > 1f && confidence <= 100f)
            {
                confidence /= 100f;
            }

            return Math.Clamp(confidence, 0f, 1f);
        }

        private static void UpdateSuggestedButton(Button button, KeyValuePair<string, float>[] items, int index)
        {
            if (index >= items.Length)
            {
                button.Visibility = Visibility.Collapsed;
                return;
            }

            button.Visibility = Visibility.Visible;
            button.Content = items[index].Key;
            button.ToolTip = $"{items[index].Key}: {(items[index].Value * 100f):0.0}%";
        }

        private static SolidColorBrush GetConfidenceBrush(float confidence)
        {
            if (confidence <= 0f)
            {
                return ConfidenceUnknownBrush;
            }

            if (confidence >= 0.85f)
            {
                return ConfidenceHighBrush;
            }

            if (confidence >= 0.65f)
            {
                return ConfidenceMidBrush;
            }

            return ConfidenceLowBrush;
        }

        private static float LoadThresholdFromSettingsFileOrDefault(float defaultThreshold = 0.60f)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return defaultThreshold;
                }

                string json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("ConfidenceThreshold", out var thresholdElement))
                {
                    return defaultThreshold;
                }

                double thresholdPercent = thresholdElement.ValueKind switch
                {
                    JsonValueKind.Number => thresholdElement.GetDouble(),
                    JsonValueKind.String when double.TryParse(thresholdElement.GetString(), out var parsed) => parsed,
                    _ => defaultThreshold * 100d
                };

                thresholdPercent = Math.Clamp(thresholdPercent, 0d, 100d);
                return (float)(thresholdPercent / 100d);
            }
            catch
            {
                return defaultThreshold;
            }
        }

        private static int LoadCameraIndexFromSettingsFileOrDefault(int defaultIndex = 0)
        {
            try
            {
                if (!File.Exists(SettingsFilePath)) return defaultIndex;

                string json = File.ReadAllText(SettingsFilePath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("ActiveCameraIndex", out var indexElement))
                {
                    if (indexElement.ValueKind == JsonValueKind.Number)
                    {
                        return indexElement.GetInt32();
                    }
                }
                return defaultIndex;
            }
            catch
            {
                return defaultIndex;
            }
        }
    }
}
