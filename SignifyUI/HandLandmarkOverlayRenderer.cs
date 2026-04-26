using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SignifyUI
{
    public static class HandLandmarkOverlayRenderer
    {
        public static void ApplyLandmarks(Canvas overlay, List<HandLandmarkPoint>? landmarks, double width, double height)
        {
            overlay.Children.Clear();
            overlay.Width = Math.Max(0, width);
            overlay.Height = Math.Max(0, height);

            if (landmarks == null || landmarks.Count == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            const double radius = 4;

            foreach (var point in landmarks)
            {
                var x = Clamp01(point.X) * width;
                var y = Clamp01(point.Y) * height;

                var marker = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = Brushes.LimeGreen,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(marker, x - radius);
                Canvas.SetTop(marker, y - radius);
                overlay.Children.Add(marker);
            }
        }

        public static Rect GetDisplayedImageRect(Image image)
        {
            if (image.Source is not System.Windows.Media.Imaging.BitmapSource source
                || image.ActualWidth <= 0
                || image.ActualHeight <= 0
                || source.PixelWidth <= 0
                || source.PixelHeight <= 0)
            {
                return Rect.Empty;
            }

            var hostWidth = image.ActualWidth;
            var hostHeight = image.ActualHeight;
            var imageAspect = (double)source.PixelWidth / source.PixelHeight;
            var hostAspect = hostWidth / hostHeight;

            if (hostAspect > imageAspect)
            {
                var renderedHeight = hostHeight;
                var renderedWidth = renderedHeight * imageAspect;
                var x = (hostWidth - renderedWidth) / 2.0;
                return new Rect(x, 0, renderedWidth, renderedHeight);
            }
            else
            {
                var renderedWidth = hostWidth;
                var renderedHeight = renderedWidth / imageAspect;
                var y = (hostHeight - renderedHeight) / 2.0;
                return new Rect(0, y, renderedWidth, renderedHeight);
            }
        }

        private static double Clamp01(float value)
        {
            return Math.Max(0, Math.Min(1, value));
        }
    }
}
