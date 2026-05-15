using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading;

namespace SignifyUI
{
    public class CalibrationHandler
    {
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;

        public string CurrentLetter { get; set; } = "";
        public int SamplesCollected { get; set; } = 0;
        public bool IsCapturing { get; set; } = false;
        public bool HandDetected { get; set; } = false;

        // Events for UI binding
        public event EventHandler<CalibrateEventArgs> OnSampleCaptured;
        public event EventHandler<CalibrateEventArgs> OnCalibrationComplete;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<string> OnRetrained;

        private const int SAMPLES_PER_BURST = 30;
        private List<string> MOTION_LETTERS = new List<string> { "J", "Z" };

        public CalibrationHandler(string apiBaseUrl = "http://127.0.0.1:8000")
        {
            _apiBaseUrl = apiBaseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Select a target letter for calibration
        /// </summary>
        public void SelectLetter(string letter)
        {
            CurrentLetter = letter.ToUpper();
            SamplesCollected = 0;
            Console.WriteLine($"[Calibration] Selected target letter: {CurrentLetter}");
        }

        /// <summary>
        /// Capture a single calibration sample from a BitmapSource
        /// </summary>
        public async Task<bool> CaptureSampleAsync(BitmapSource frame)
        {
            if (string.IsNullOrEmpty(CurrentLetter))
            {
                OnError?.Invoke(this, new ErrorEventArgs(new Exception("No letter selected. Select a letter first.")));
                return false;
            }

            if (frame == null)
            {
                OnError?.Invoke(this, new ErrorEventArgs(new Exception("Invalid frame provided.")));
                return false;
            }

            try
            {
                byte[] imageBytes = BitmapToBytes(frame);

                using (var content = new MultipartFormDataContent())
                {
                    // Add image file
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    content.Add(imageContent, "file", "frame.jpg");

                    // Add label
                    content.Add(new StringContent(CurrentLetter), "label");

                    // Send POST request to /calibrate
                    var response = await _httpClient.PostAsync($"{_apiBaseUrl}/calibrate", content);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // Parse response
                        dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);

                        if (jsonResponse["success"] == true && jsonResponse["hand_detected"] == true)
                        {
                            SamplesCollected++;
                            HandDetected = true;
                            OnSampleCaptured?.Invoke(this, new CalibrateEventArgs
                            {
                                Letter = CurrentLetter,
                                SamplesCount = SamplesCollected,
                                HandDetected = true
                            });
                            return true;
                        }
                        else
                        {
                            HandDetected = false;
                            OnError?.Invoke(this, new ErrorEventArgs(new Exception("Hand not detected in frame.")));
                            return false;
                        }
                    }
                    else
                    {
                        OnError?.Invoke(this, new ErrorEventArgs(new Exception($"API error: {response.StatusCode} - {responseContent}")));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new ErrorEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// Automatically capture a burst of samples at specified intervals
        /// </summary>
        public async Task CaptureBurstAsync(BitmapSource frame, int burstSize = SAMPLES_PER_BURST, int delayMs = 50)
        {
            if (string.IsNullOrEmpty(CurrentLetter))
            {
                OnError?.Invoke(this, new ErrorEventArgs(new Exception("No letter selected.")));
                return;
            }

            IsCapturing = true;
            int initialSamples = SamplesCollected;

            try
            {
                for (int i = 0; i < burstSize; i++)
                {
                    if (!IsCapturing) break; // Allow cancellation

                    bool success = await CaptureSampleAsync(frame);

                    if (!success)
                    {
                        // Continue capturing even if one frame fails (hand might not be detected momentarily)
                    }

                    // Delay between captures
                    if (i < burstSize - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                }

                int capturedInBurst = SamplesCollected - initialSamples;
                OnCalibrationComplete?.Invoke(this, new CalibrateEventArgs
                {
                    Letter = CurrentLetter,
                    SamplesCount = capturedInBurst,
                    HandDetected = true
                });
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new ErrorEventArgs(ex));
            }
            finally
            {
                IsCapturing = false;
            }
        }

        /// <summary>
        /// Cancel the current capture burst
        /// </summary>
        public void CancelCapture()
        {
            IsCapturing = false;
            Console.WriteLine("[Calibration] Capture cancelled.");
        }

        /// <summary>
        /// Retrain the model with all collected calibration samples
        /// </summary>
        public async Task<RetrainResult> RetrainModelAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/retrain", null);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);

                    var result = new RetrainResult
                    {
                        Success = jsonResponse["success"] == true,
                        Message = jsonResponse["message"]?.ToString() ?? "Unknown",
                        Accuracy = (float)jsonResponse["accuracy"],
                        SamplesUsed = (int)jsonResponse["calibration_samples_used"],
                        Labels = new List<string>(jsonResponse["labels"].ToObject<List<string>>())
                    };

                    OnRetrained?.Invoke(this, $"Model retrained! Accuracy: {result.Accuracy:P2}");
                    SamplesCollected = 0; // Reset counter after retrain
                    return result;
                }
                else
                {
                    throw new Exception($"Retrain failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, new ErrorEventArgs(ex));
                return new RetrainResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Reset calibration progress
        /// </summary>
        public void Reset()
        {
            CurrentLetter = "";
            SamplesCollected = 0;
            IsCapturing = false;
            HandDetected = false;
            Console.WriteLine("[Calibration] Reset to initial state.");
        }

        /// <summary>
        /// Convert BitmapSource to byte array (JPEG format)
        /// </summary>
        private byte[] BitmapToBytes(BitmapSource bitmap)
        {
            byte[] data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0);

            // Use JpegBitmapEncoder for compression
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            byte[] jpeg;
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                jpeg = stream.ToArray();
            }

            return jpeg;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Custom event args for calibration events
    /// </summary>
    public class CalibrateEventArgs : EventArgs
    {
        public string Letter { get; set; }
        public int SamplesCount { get; set; }
        public bool HandDetected { get; set; }
    }

    /// <summary>
    /// Result from model retraining
    /// </summary>
    public class RetrainResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public float Accuracy { get; set; }
        public int SamplesUsed { get; set; }
        public List<string> Labels { get; set; }
    }
}
