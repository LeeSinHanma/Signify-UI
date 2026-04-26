using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SignifyUI
{
    public sealed class HandPredictionResponse
    {
        [JsonPropertyName("hand_detected")]
        public bool HandDetected { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("raw_label")]
        public string? RawLabel { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("probabilities")]
        public Dictionary<string, float> Probabilities { get; set; } = new();

        [JsonPropertyName("handedness")]
        public string? Handedness { get; set; }

        [JsonPropertyName("handedness_score")]
        public float? HandednessScore { get; set; }

        [JsonPropertyName("landmarks")]
        public List<HandLandmarkPoint>? Landmarks { get; set; }
    }

    public sealed class HandLandmarkPoint
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }
    }

    public sealed class HandPredictionClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        public HandPredictionClient(string baseUrl = "http://127.0.0.1:8000", HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl));
            }

            if (httpClient is null)
            {
                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
                };
                _ownsHttpClient = true;
            }
            else
            {
                _httpClient = httpClient;
                if (_httpClient.BaseAddress is null)
                {
                    _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                }
            }

            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<HandPredictionResponse> PredictFromBytesAsync(
            byte[] imageBytes,
            float threshold = 0.65f,
            int smoothWindow = 6,
            bool includeLandmarks = false,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
            }

            using var form = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            form.Add(imageContent, "file", "frame.jpg");
            form.Add(new StringContent(threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)), "threshold");
            form.Add(new StringContent(smoothWindow.ToString(System.Globalization.CultureInfo.InvariantCulture)), "smooth_window");
            form.Add(new StringContent(includeLandmarks ? "true" : "false"), "include_landmarks");

            using HttpResponseMessage response = await _httpClient.PostAsync("predict", form, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            HandPredictionResponse? result = JsonSerializer.Deserialize<HandPredictionResponse>(json, JsonOptions);

            if (result is null)
            {
                throw new InvalidOperationException("Could not deserialize the prediction response.");
            }

            return result;
        }

        public async Task<HandPredictionResponse> PredictFromBitmapSourceAsync(
            BitmapSource bitmapSource,
            float threshold = 0.65f,
            int smoothWindow = 6,
            bool includeLandmarks = false,
            int jpegQuality = 80,
            CancellationToken cancellationToken = default)
        {
            if (bitmapSource is null)
            {
                throw new ArgumentNullException(nameof(bitmapSource));
            }

            byte[] imageBytes = ConvertBitmapSourceToJpegBytes(bitmapSource, jpegQuality);
            return await PredictFromBytesAsync(imageBytes, threshold, smoothWindow, includeLandmarks, cancellationToken).ConfigureAwait(false);
        }

        public static byte[] ConvertBitmapSourceToJpegBytes(BitmapSource bitmapSource, int jpegQuality = 80)
        {
            if (bitmapSource is null)
            {
                throw new ArgumentNullException(nameof(bitmapSource));
            }

            jpegQuality = Math.Clamp(jpegQuality, 1, 100);

            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = jpegQuality
            };
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);
            return memoryStream.ToArray();
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}