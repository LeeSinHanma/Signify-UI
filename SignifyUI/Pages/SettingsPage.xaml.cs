using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SignifyUI;

// ═══════════════════════════════════════════════════════════════
//  SettingsPage.xaml.cs  —  Code-Behind
//
//  Manages all settings interactions:
//  - Category tab navigation
//  - Live slider value updates
//  - Camera enumeration and selection
//  - Toggle state persistence
//  - Settings serialization (save/load/reset)
//  - Help dialog display
// ═══════════════════════════════════════════════════════════════

namespace Signify.Pages
{
    public partial class SettingsPage : Page
    {
        private const string SettingsFilePath = "settings.json";
        private Dictionary<string, object> _currentSettings = new();
        private Button? _activeCategory;

        private VideoCapture? _capture;
        private CancellationTokenSource? _cameraCts;

        private CalibrationHandler _calibrationHandler;
        private BitmapSource? _latestCalibrationFrame;

        public SettingsPage()
        {
            InitializeComponent();

            // Initialization
            _calibrationHandler = new CalibrationHandler();
            _calibrationHandler.OnSampleCaptured += CalibrationHandler_OnSampleCaptured;
            _calibrationHandler.OnCalibrationComplete += CalibrationHandler_OnCalibrationComplete;
            _calibrationHandler.OnError += CalibrationHandler_OnError;

            // Load persisted settings
            LoadSettings();

            // Wire up event handlers
            WireEventHandlers();

            // Initialize the page with Vision & Input category
            ShowCategory("Vision");

            _ = DetectCamerasAsync();

            Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCamera();
            if (_calibrationHandler != null)
            {
                _calibrationHandler.OnSampleCaptured -= CalibrationHandler_OnSampleCaptured;
                _calibrationHandler.OnCalibrationComplete -= CalibrationHandler_OnCalibrationComplete;
                _calibrationHandler.OnError -= CalibrationHandler_OnError;
            }
        }

        // ── CATEGORY NAVIGATION ───────────────────────────────────────
        private void WireEventHandlers()
        {
            btnCatVision.Click += (s, e) => ShowCategory("Vision");
            btnCatAudio.Click += (s, e) => ShowCategory("Audio");
            btnCatCalibration.Click += (s, e) => ShowCategory("Calibration");
            btnCatAccount.Click += (s, e) => ShowCategory("Account");

            sldHandSensitivity.ValueChanged += (s, e) => lblHandSensValue.Text = $"{sldHandSensitivity.Value:0}%";
            sldConfidenceThresh.ValueChanged += (s, e) => lblConfidenceThreshValue.Text = $"{sldConfidenceThresh.Value:0}%";

            btnHelp.Click += BtnHelp_Click;
        }

        private void ShowCategory(string category)
        {
            // Deactivate previous category button
            if (_activeCategory != null)
            {
                _activeCategory.Style = (Style)FindResource("CategoryBtn");
            }

            // Hide all panels first
            pnlVisionSettings.Visibility = Visibility.Collapsed;
            pnlAudioSettings.Visibility = Visibility.Collapsed;
            pnlCalibrationSettings.Visibility = Visibility.Collapsed;
            pnlAccountSettings.Visibility = Visibility.Collapsed;

            // Activate new category button, update title/description, and show corresponding panel
            switch (category)
            {
                case "Vision":
                    _activeCategory = btnCatVision;
                    lblSectionTitle.Text = "Vision & Input";
                    lblSectionDesc.Text = "Configure your camera and gesture recognition sensitivity for the best gameplay experience.";
                    pnlVisionSettings.Visibility = Visibility.Visible;
                    break;
                case "Audio":
                    _activeCategory = btnCatAudio;
                    lblSectionTitle.Text = "Audio & Sfx";
                    lblSectionDesc.Text = "Adjust sound levels, enable haptic feedback, and manage audio preferences.";
                    pnlAudioSettings.Visibility = Visibility.Visible;
                    break;
                case "Calibration":
                    _activeCategory = btnCatCalibration;
                    lblSectionTitle.Text = "Calibration";
                    lblSectionDesc.Text = "Train and capture custom hand signs to improve recognition accuracy.";
                    pnlCalibrationSettings.Visibility = Visibility.Visible;
                    break;
                case "Account":
                    _activeCategory = btnCatAccount;
                    lblSectionTitle.Text = "Account";
                    lblSectionDesc.Text = "Manage your profile, sync preferences, and privacy settings.";
                    pnlAccountSettings.Visibility = Visibility.Visible;
                    RefreshAccountPanel();
                    break;
            }

            if (_activeCategory != null)
            {
                _activeCategory.Style = (Style)FindResource("CategoryBtn_Active");
            }

            if (category == "Calibration")
            {
                StartCamera();
            }
            else
            {
                StopCamera();
            }
        }

        private void RefreshAccountPanel()
        {
            var (success, _, account) = SignifyUI.Services.AuthService.GetCurrentAccount();
            if (success && account != null)
            {
                lblAccountUsername.Text = $"@{account.Username}";
                lblAccountName.Text = account.Name;
                lblAccountMastery.Text = account.MasteryLevel;

                string imagePath = account.MasteryLevel.ToLowerInvariant() switch
                {
                    "beginner" => "pack://application:,,,/RankImages/beginner.png",
                    "intermediate" => "pack://application:,,,/RankImages/intermediate.png",
                    "advanced" => "pack://application:,,,/RankImages/advanced.png",
                    "expert" => "pack://application:,,,/RankImages/expert.png",
                    "mastery" => "pack://application:,,,/RankImages/mastery.png",
                    _ => "pack://application:,,,/RankImages/beginner.png"
                };

                try
                {
                    imgRank.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
                }
                catch
                {
                    // Fallback or ignore if image not found
                    imgRank.Source = null;
                }
            }
            else
            {
                lblAccountUsername.Text = "(@not_logged_in)";
                lblAccountName.Text = "Not Logged In";
                lblAccountMastery.Text = "-";
                imgRank.Source = null;
            }
        }

        // ── SETTINGS PERSISTENCE ───────────────────────────────────────
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    _currentSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                }
                else
                {
                    _currentSettings = new();
                }

                // Apply loaded settings to controls
                ApplySettingsToControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveSettings()
        {
            try
            {
                SaveSettingsSilent();
                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettingsSilent()
        {
            // Collect current control values
            _currentSettings["HandSensitivity"] = sldHandSensitivity.Value;
            _currentSettings["ConfidenceThreshold"] = sldConfidenceThresh.Value;
            _currentSettings["HapticFeedback"] = chkHapticFeedback.IsChecked ?? false;
            _currentSettings["DarkAtmosphere"] = chkDarkAtmosphere.IsChecked ?? false;
            if (cmbCamera.SelectedIndex >= 0 && cmbCamera.ItemsSource is List<string> cams && cams.Count > cmbCamera.SelectedIndex)
            {
                var text = cams[cmbCamera.SelectedIndex];
                if (text.StartsWith("Camera ") && int.TryParse(text.Substring(7), out int idx))
                {
                    _currentSettings["ActiveCameraIndex"] = idx;
                }
            }

            // Serialize to JSON
            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        private void ApplySettingsToControls()
        {
            if (_currentSettings.ContainsKey("HandSensitivity"))
            {
                if (_currentSettings["HandSensitivity"] is JsonElement elem1)
                {
                    sldHandSensitivity.Value = elem1.GetDouble();
                }
                else if (_currentSettings["HandSensitivity"] is double d1)
                {
                    sldHandSensitivity.Value = d1;
                }
            }

            if (_currentSettings.ContainsKey("ConfidenceThreshold"))
            {
                if (_currentSettings["ConfidenceThreshold"] is JsonElement elem2)
                {
                    sldConfidenceThresh.Value = elem2.GetDouble();
                }
                else if (_currentSettings["ConfidenceThreshold"] is double d2)
                {
                    sldConfidenceThresh.Value = d2;
                }
            }

            if (_currentSettings.ContainsKey("HapticFeedback"))
            {
                if (_currentSettings["HapticFeedback"] is JsonElement elem3)
                {
                    chkHapticFeedback.IsChecked = elem3.GetBoolean();
                }
                else if (_currentSettings["HapticFeedback"] is bool b1)
                {
                    chkHapticFeedback.IsChecked = b1;
                }
            }

            if (_currentSettings.ContainsKey("DarkAtmosphere"))
            {
                if (_currentSettings["DarkAtmosphere"] is JsonElement elem4)
                {
                    chkDarkAtmosphere.IsChecked = elem4.GetBoolean();
                }
                else if (_currentSettings["DarkAtmosphere"] is bool b2)
                {
                    chkDarkAtmosphere.IsChecked = b2;
                }
            }

            // Camera will be selected after detection finishes
        }

        private async Task DetectCamerasAsync()
        {
            var activeCameras = new List<string>();

            await Task.Run(() =>
            {
                for (int i = 0; i < 4; i++)
                {
                    using var cap = new VideoCapture(i);
                    if (cap.IsOpened())
                    {
                        activeCameras.Add($"Camera {i}");
                    }
                }
            });

            if (activeCameras.Count == 0)
            {
                activeCameras.Add("Camera 0"); // Fallback
            }

            cmbCamera.ItemsSource = activeCameras;

            int savedIndex = LoadCameraIndexFromSettingsFileOrDefault();
            string targetText = $"Camera {savedIndex}";
            int indexToSelect = activeCameras.IndexOf(targetText);

            cmbCamera.SelectedIndex = indexToSelect >= 0 ? indexToSelect : 0;
        }

        private void cmbCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCamera.SelectedIndex >= 0)
            {
                SaveSettingsSilent();
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

        private void ResetToDefaults()
        {
            // Reset controls to defaults
            sldHandSensitivity.Value = 85;
            sldConfidenceThresh.Value = 60;
            
            // Clear settings file
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }

            _currentSettings = new();
            MessageBox.Show("Settings reset to defaults!", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── BUTTON HANDLERS ───────────────────────────────────────────
        private bool _isSyncingOldPassword = false;
        private void txtOldPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingOldPassword)
            {
                _isSyncingOldPassword = true;
                txtOldPasswordVisible.Text = txtOldPassword.Password;
                _isSyncingOldPassword = false;
            }
        }
        private void txtOldPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingOldPassword)
            {
                _isSyncingOldPassword = true;
                txtOldPassword.Password = txtOldPasswordVisible.Text;
                _isSyncingOldPassword = false;
            }
        }

        private bool _isSyncingNewPassword = false;
        private void txtNewPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingNewPassword)
            {
                _isSyncingNewPassword = true;
                txtNewPasswordVisible.Text = txtNewPassword.Password;
                _isSyncingNewPassword = false;
            }
        }
        private void txtNewPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingNewPassword)
            {
                _isSyncingNewPassword = true;
                txtNewPassword.Password = txtNewPasswordVisible.Text;
                _isSyncingNewPassword = false;
            }
        }

        private bool _isSyncingConfirmPassword = false;
        private void txtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncingConfirmPassword)
            {
                _isSyncingConfirmPassword = true;
                txtConfirmPasswordVisible.Text = txtConfirmPassword.Password;
                _isSyncingConfirmPassword = false;
            }
        }
        private void txtConfirmPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncingConfirmPassword)
            {
                _isSyncingConfirmPassword = true;
                txtConfirmPassword.Password = txtConfirmPasswordVisible.Text;
                _isSyncingConfirmPassword = false;
            }
        }

        private void chkShowPasswords_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool show = chkShowPasswords.IsChecked ?? false;

            txtOldPassword.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            txtOldPasswordVisible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            txtNewPassword.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            txtNewPasswordVisible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            txtConfirmPassword.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            txtConfirmPasswordVisible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!SignifyUI.Services.AuthService.IsLoggedIn || string.IsNullOrWhiteSpace(SignifyUI.Services.AuthService.CurrentUsername))
            {
                MessageBox.Show("You must be logged in to change your password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string oldPassword = txtOldPassword.Password;
            string newPassword = txtNewPassword.Password;
            string confirmPassword = txtConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("Please fill out all password fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("New passwords do not match. Please try again.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnChangePassword.IsEnabled = false;

            var (success, message) = SignifyUI.Services.AuthService.ChangePassword(
                SignifyUI.Services.AuthService.CurrentUsername,
                oldPassword,
                newPassword);

            if (success)
            {
                MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                txtOldPassword.Password = string.Empty;
                txtNewPassword.Password = string.Empty;
                txtConfirmPassword.Password = string.Empty;
                chkShowPasswords.IsChecked = false;
            }
            else
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            btnChangePassword.IsEnabled = true;
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout off your account?", 
                "Confirm Logout",
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SignifyUI.Services.AuthService.Logout();

                // Go completely back to homepage wrapper where they will be prompted to login again 
                // by nature of freehand navigation blocking or topbar auth button.
                var mainWindow = Application.Current.MainWindow as SignifyUI.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.MainFrame.Navigate(new SignifyUI.HomePage());
                }
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            string helpText = @"Settings Help

VISION & INPUT
• Camera Source: Select which camera to use for hand gesture recognition
• Hand Sensitivity: Higher values detect faster movements; lower values are more forgiving
• Confidence Threshold: Higher values require more precise signing; lower values are more lenient

AUDIO & SFX
Configure sound levels and feedback preferences.

APPEARANCE
Customize the visual theme and interface appearance.

ACCOUNT
View your profile attributes, sign-in username, and global mastery rank.

TOGGLES
• Haptic Feedback: Enable vibration feedback on supported devices
• Dark Atmosphere: Use dark theme for reduced eye strain";

            MessageBox.Show(helpText, "Signify Settings Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── CALIBRATION CAMERA & ACTIONS ─────────────────────────────
        private void StartCamera()
        {
            if (_capture != null) return;
            
            int activeCameraIndex = LoadCameraIndexFromSettingsFileOrDefault();
            _capture = new VideoCapture(activeCameraIndex);
            if (!_capture.IsOpened())
            {
                txtCalibrationPlaceholder.Text = "[ Unable to open camera ]";
                txtCalibrationPlaceholder.Visibility = Visibility.Visible;
                _capture.Dispose();
                _capture = null;
                return;
            }
            _capture.FrameWidth = 640;
            _capture.FrameHeight = 480;

            _cameraCts = new CancellationTokenSource();
            var token = _cameraCts.Token;
            _ = Task.Run(() => CaptureLoop(token), token);
        }

        private void StopCamera()
        {
            _cameraCts?.Cancel();
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

                        _latestCalibrationFrame = bmp;
                        imgCalibrationCamera.Source = bmp;
                        txtCalibrationPlaceholder.Visibility = Visibility.Collapsed;
                    }, DispatcherPriority.Render);
                }
                Thread.Sleep(33);
            }
        }

        // ── CALIBRATION HANDLER EVENTS ───────────────────────────────
        private void CalibrationHandler_OnSampleCaptured(object? sender, CalibrateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lblCalibrationStatus.Text = $"Capturing: {e.SamplesCount} samples processed for '{e.Letter}'... (Hand Detected: {e.HandDetected})";
                lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xFF, 0xA0)); // Green
            });
        }

        private void CalibrationHandler_OnCalibrationComplete(object? sender, CalibrateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lblCalibrationStatus.Text = $"Done! Successfully captured {e.SamplesCount} frames for '{e.Letter}'. Press 'Retrain' to apply.";
                lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xC5, 0x18)); // Gold
                btnCaptureCalibration.IsEnabled = true;
            });
        }

        private void CalibrationHandler_OnError(object? sender, ErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lblCalibrationStatus.Text = $"Error: {e.GetException().Message}";
                lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x88)); // Red
                btnCaptureCalibration.IsEnabled = true;
            });
        }

        // ── CALIBRATION UI BUTTONS ───────────────────────────────────
        private async void btnCaptureCalibration_Click(object sender, RoutedEventArgs e)
        {
            string letter = txtLetterToTrain.Text.Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(letter))
            {
                MessageBox.Show("Please enter a letter to train.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_latestCalibrationFrame == null)
            {
                MessageBox.Show("Waiting for camera frame. Try again in a moment.", "Wait", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnCaptureCalibration.IsEnabled = false;
            lblCalibrationStatus.Text = "Initializing capture sequence...";
            lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0xAA, 0xFF));

            _calibrationHandler.SelectLetter(letter);

            // Execute the capture loop block off-thread relying on async dispatch
            await _calibrationHandler.CaptureBurstAsync(_latestCalibrationFrame);
        }

        private async void btnRetrainCalibration_Click(object sender, RoutedEventArgs e)
        {
            btnRetrainCalibration.IsEnabled = false;
            lblCalibrationStatus.Text = "Retraining in progress... Please wait.";
            lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0xAA, 0xFF));

            var result = await _calibrationHandler.RetrainModelAsync();

            Dispatcher.Invoke(() =>
            {
                if (result.Success)
                {
                    lblCalibrationStatus.Text = $"Retrain successful! Accuracy: {result.Accuracy * 100:0.0}% ({result.SamplesUsed} samples used)";
                    lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xFF, 0xA0));
                    txtLetterToTrain.Text = "";
                }
                else
                {
                    lblCalibrationStatus.Text = $"Retrain failed: {result.Message}";
                    lblCalibrationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x88));
                }

                btnRetrainCalibration.IsEnabled = true;
            });
        }

        // ── NAVIGATION ───────────────────────────────────────────────
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
