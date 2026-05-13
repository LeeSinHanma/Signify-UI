using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;

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
        private VideoCapture? _capture;
        private CancellationTokenSource? _cancellationTokenSource;

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
                        StartCamera();
                    }

                    private void FreehandPage_Unloaded(object sender, RoutedEventArgs e)
                    {
                        StopCamera();
                    }

                    private void StartCamera()
                    {
                        _capture = new VideoCapture(0); // 0 is default camera index
                        _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                        _capture.Set(VideoCaptureProperties.FrameHeight, 480);

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
                    }

                    private void CaptureLoop(CancellationToken token)
                    {
                        using var frame = new Mat();
                        while (!token.IsCancellationRequested && _capture != null && _capture.IsOpened())
                        {
                            if (_capture.Read(frame) && !frame.Empty())
                            {
                                // Encode frame as JPG to display in WPF
                                Cv2.ImEncode(".jpg", frame, out byte[] imageBytes);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (token.IsCancellationRequested) return;

                                    var bitmapImage = new BitmapImage();
                                    bitmapImage.BeginInit();
                                    bitmapImage.StreamSource = new MemoryStream(imageBytes);
                                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmapImage.EndInit();
                                    imgCameraFeed.Source = bitmapImage;
                                    txtCameraPlaceholder.Visibility = Visibility.Collapsed;
                                }, DispatcherPriority.Render);
                            }

                            // Cap frame rate to ~30 FPS
                            Thread.Sleep(33);
                        }
                    }
                }
            }
