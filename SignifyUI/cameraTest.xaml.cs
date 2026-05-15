using OpenCvSharp;
using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SignifyUI
{
    public partial class CameraTest : Page
    {
        private static readonly TimeSpan PredictionInterval = TimeSpan.FromMilliseconds(250);

        private VideoCapture? _capture;
        private HandPredictionClient? _predictionClient;
        private CancellationTokenSource? _cameraLoopCts;
        private Task? _cameraLoopTask;
        private DateTime _lastPredictionAtUtc = DateTime.MinValue;
        private int _predictionInFlight;

        public CameraTest()
        {
            InitializeComponent();
            Loaded += CameraTest_Loaded;
            Unloaded += CameraTest_Unloaded;
        }

        private void CameraTest_Loaded(object sender, RoutedEventArgs e)
        {
            StartCamera();
        }

        private void CameraTest_Unloaded(object sender, RoutedEventArgs e)
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
            if (!_capture.IsOpened())
            {
                CameraStatusText.Text = "Unable to open camera.";
                _capture.Dispose();
                _capture = null;
                return;
            }

            _capture.FrameWidth = 640;
            _capture.FrameHeight = 480;

            _cameraLoopCts = new CancellationTokenSource();
            _cameraLoopTask = Task.Run(() => CameraLoop(_cameraLoopCts.Token));
        }

        private void StopCamera()
        {
            _cameraLoopCts?.Cancel();

            try
            {
                _cameraLoopTask?.Wait(500);
            }
            catch
            {
            }

            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _predictionClient?.Dispose();
            _predictionClient = null;

            _cameraLoopCts?.Dispose();
            _cameraLoopCts = null;
            _cameraLoopTask = null;
        }

        private async Task CameraLoop(CancellationToken cancellationToken)
        {
            using var frame = new Mat();

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_capture == null)
                {
                    break;
                }

                if (!_capture.Read(frame) || frame.Empty())
                {
                    await Task.Delay(33, cancellationToken);
                    continue;
                }

                Cv2.Flip(frame, frame, FlipMode.Y);
                var imageSource = ToBitmapSource(frame);

                await Dispatcher.InvokeAsync(() =>
                {
                    CameraImage.Source = imageSource;
                    CameraStatusText.Visibility = Visibility.Collapsed;
                    LandmarkOverlay.Children.Clear();
                });

                var nowUtc = DateTime.UtcNow;
                if (_predictionClient != null
                    && nowUtc - _lastPredictionAtUtc >= PredictionInterval
                    && Interlocked.CompareExchange(ref _predictionInFlight, 1, 0) == 0)
                {
                    _lastPredictionAtUtc = nowUtc;
                    var predictionFrame = frame.Clone();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await PredictAndRenderAsync(predictionFrame, cancellationToken);
                        }
                        finally
                        {
                            predictionFrame.Dispose();
                            Interlocked.Exchange(ref _predictionInFlight, 0);
                        }
                    }, cancellationToken);
                }

                await Task.Delay(33, cancellationToken);
            }
        }

        private async Task PredictAndRenderAsync(Mat frame, CancellationToken cancellationToken)
        {
            if (_predictionClient == null)
            {
                return;
            }

            try
            {
                Cv2.ImEncode(".jpg", frame, out var jpegBytes);
                var prediction = await _predictionClient.PredictFromBytesAsync(
                    jpegBytes,
                    threshold: 0.65f,
                    smoothWindow: 6,
                    includeLandmarks: true,
                    cancellationToken: cancellationToken);

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdatePredictionUi(prediction);
                    SetText("PredictionStatusText", "Status: prediction updated");
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SetText("PredictionStatusText", $"Status: prediction error - {ex.Message}");
                });
            }
        }

        private void UpdatePredictionUi(HandPredictionResponse prediction)
        {
            SetText("HandDetectedValueText", $"Value: {prediction.HandDetected.ToString().ToLowerInvariant()}");
            SetText("LabelValueText", $"Value: {prediction.Label}");
            SetText("RawLabelValueText", $"Value: {prediction.RawLabel ?? "null"}");
            SetText("ConfidenceValueText", $"Value: {prediction.Confidence:0.000}");

            var probabilities = prediction.Probabilities
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value:0.000}");
            SetText("ProbabilitiesValueText", "Value: " + string.Join(Environment.NewLine, probabilities));

            SetText("HandednessValueText", $"Value: {prediction.Handedness ?? "null"}");
            SetText("HandednessScoreValueText", prediction.HandednessScore.HasValue
                ? $"Value: {prediction.HandednessScore.Value:0.000}"
                : "Value: null");

            if (prediction.Landmarks == null || prediction.Landmarks.Count == 0)
            {
                SetText("LandmarksValueText", "Value: null");
            }
            else
            {
                var topLandmarks = prediction.Landmarks
                    .Take(3)
                    .Select(p => $"{{ x: {p.X:0.###}, y: {p.Y:0.###}, z: {p.Z:0.###} }}");
                SetText("LandmarksValueText", "Value: " + string.Join(Environment.NewLine, topLandmarks)
                    + (prediction.Landmarks.Count > 3 ? Environment.NewLine + "..." : string.Empty));
            }
        }

        private void SetText(string elementName, string value)
        {
            if (FindName(elementName) is TextBlock textBlock)
            {
                textBlock.Text = value;
            }
        }

        private static BitmapSource ToBitmapSource(Mat frame)
        {
            using var rgbFrame = new Mat();
            Cv2.CvtColor(frame, rgbFrame, ColorConversionCodes.BGR2RGB);

            var stride = rgbFrame.Cols * rgbFrame.ElemSize();
            var length = stride * rgbFrame.Rows;
            var buffer = new byte[length];
            Marshal.Copy(rgbFrame.Data, buffer, 0, length);

            var bitmap = BitmapSource.Create(
                rgbFrame.Cols,
                rgbFrame.Rows,
                96,
                96,
                PixelFormats.Rgb24,
                null,
                buffer,
                stride);

            bitmap.Freeze();
            return bitmap;
        }

        private static int LoadCameraIndexFromSettingsFileOrDefault(int defaultIndex = 0)
        {
            try
            {
                if (!File.Exists("settings.json")) return defaultIndex;

                string json = File.ReadAllText("settings.json");
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
