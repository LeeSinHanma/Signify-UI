using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using SignifyUI.Services;

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

        public SettingsPage()
        {
            InitializeComponent();

            // Load persisted settings
            LoadSettings();

            // Wire up event handlers
            WireEventHandlers();

            // Subscribe to auth changes
            AuthService.SessionChanged += AuthService_SessionChanged;

            // Initialize the page with Vision & Input category
            ShowCategory("Vision");
            RefreshUserDisplay();
        }

        // ── USER DISPLAY ───────────────────────────────────────────────
        private void AuthService_SessionChanged(object? sender, EventArgs e) => RefreshUserDisplay();

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

        // ── CATEGORY NAVIGATION ───────────────────────────────────────
        private void WireEventHandlers()
        {
            btnCatVision.Click += (s, e) => ShowCategory("Vision");
            btnCatAudio.Click += (s, e) => ShowCategory("Audio");
            btnCatAppearance.Click += (s, e) => ShowCategory("Appearance");
            btnCatAccount.Click += (s, e) => ShowCategory("Account");

            sldHandSensitivity.ValueChanged += (s, e) => lblHandSensValue.Text = $"{sldHandSensitivity.Value:0}%";
            sldConfidenceThresh.ValueChanged += (s, e) => lblConfidenceThreshValue.Text = $"{sldConfidenceThresh.Value:0}%";

            btnApplyChanges.Click += BtnApplyChanges_Click;
            btnResetDefaults.Click += BtnResetDefaults_Click;
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
            pnlAppearanceSettings.Visibility = Visibility.Collapsed;
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
                case "Appearance":
                    _activeCategory = btnCatAppearance;
                    lblSectionTitle.Text = "Appearance";
                    lblSectionDesc.Text = "Customize the visual appearance of the application and select your preferred theme.";
                    pnlAppearanceSettings.Visibility = Visibility.Visible;
                    break;
                case "Account":
                    _activeCategory = btnCatAccount;
                    lblSectionTitle.Text = "Account";
                    lblSectionDesc.Text = "Manage your profile, sync preferences, and privacy settings.";
                    pnlAccountSettings.Visibility = Visibility.Visible;
                    break;
            }

            if (_activeCategory != null)
            {
                _activeCategory.Style = (Style)FindResource("CategoryBtn_Active");
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
                // Collect current control values
                _currentSettings["HandSensitivity"] = sldHandSensitivity.Value;
                _currentSettings["ConfidenceThreshold"] = sldConfidenceThresh.Value;
                _currentSettings["HapticFeedback"] = chkHapticFeedback.IsChecked ?? false;
                _currentSettings["DarkAtmosphere"] = chkDarkAtmosphere.IsChecked ?? false;

                // Serialize to JSON
                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);

                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private void BtnApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all settings to defaults? This cannot be undone.", "Confirm Reset",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ResetToDefaults();
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
Manage your profile and privacy settings.

TOGGLES
• Haptic Feedback: Enable vibration feedback on supported devices
• Dark Atmosphere: Use dark theme for reduced eye strain";

            MessageBox.Show(helpText, "Signify Settings Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── NAVIGATION ───────────────────────────────────────────────
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
