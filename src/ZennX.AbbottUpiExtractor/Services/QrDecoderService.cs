using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class QrDecoderService
    {
        private static readonly Regex UpiAddressRegex = new Regex(
            @"^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z0-9.\-_]{2,64}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public (ExtractionStatus Status, string UpiId, string ErrorMessage) DecodeBitmap(Bitmap bitmap)
        {
            try
            {
                // Multi-pass adaptive decoding engine:
                // Pass 1: Standard BarcodeReader
                var result = TryDecodeWithReader(bitmap, useGlobalHistogram: false);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    return ExtractUpiFromPayload(result.Text);
                }

                // Pass 2: GlobalHistogramBinarizer (proven to decode dark backgrounds & inverted QR codes like PhonePe / Paytm / GooglePay)
                result = TryDecodeWithReader(bitmap, useGlobalHistogram: true);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    return ExtractUpiFromPayload(result.Text);
                }

                // Pass 3: Invert colors (black-on-white vs white-on-black)
                using (var inverted = InvertBitmap(bitmap))
                {
                    result = TryDecodeWithReader(inverted, useGlobalHistogram: false) 
                          ?? TryDecodeWithReader(inverted, useGlobalHistogram: true);
                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        return ExtractUpiFromPayload(result.Text);
                    }
                }

                // Pass 4: Auto-crop center QR Region (standees/posters often have borders, headers, and footers like "Accepted Here" and "PhonePe")
                using (var centerCrop = CropCenterQrRegion(bitmap))
                {
                    result = TryDecodeWithReader(centerCrop, useGlobalHistogram: false)
                          ?? TryDecodeWithReader(centerCrop, useGlobalHistogram: true);
                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        return ExtractUpiFromPayload(result.Text);
                    }

                    // Inverted center crop
                    using (var centerInverted = InvertBitmap(centerCrop))
                    {
                        result = TryDecodeWithReader(centerInverted, useGlobalHistogram: false)
                              ?? TryDecodeWithReader(centerInverted, useGlobalHistogram: true);
                        if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                        {
                            return ExtractUpiFromPayload(result.Text);
                        }
                    }
                }

                // Pass 5: High-contrast threshold binarization
                using (var thresholdBmp = ApplyContrastAndGrayscale(bitmap))
                {
                    result = TryDecodeWithReader(thresholdBmp, useGlobalHistogram: false)
                          ?? TryDecodeWithReader(thresholdBmp, useGlobalHistogram: true);
                    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                    {
                        return ExtractUpiFromPayload(result.Text);
                    }
                }

                return (ExtractionStatus.Failed, string.Empty, "No QR code could be detected or decoded in the image.");
            }
            catch (Exception ex)
            {
                return (ExtractionStatus.Failed, string.Empty, $"QR decode exception: {ex.Message}");
            }
        }

        private static Result? TryDecodeWithReader(Bitmap bmp, bool useGlobalHistogram)
        {
            try
            {
                var options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE },
                    TryInverted = true
                };

                var reader = new ZXing.Windows.Compatibility.BarcodeReader
                {
                    AutoRotate = true,
                    Options = options
                };

                if (useGlobalHistogram)
                {
                    var luminanceSource = new BitmapLuminanceSource(bmp);
                    var binarizer = new GlobalHistogramBinarizer(luminanceSource);
                    var binaryBitmap = new BinaryBitmap(binarizer);
                    return new MultiFormatReader().decode(binaryBitmap, options.Hints);
                }

                return reader.Decode(bmp);
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap InvertBitmap(Bitmap original)
        {
            var inverted = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(inverted))
            {
                // Inversion ColorMatrix
                var colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {-1, 0, 0, 0, 0},
                    new float[] {0, -1, 0, 0, 0},
                    new float[] {0, 0, -1, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {1, 1, 1, 0, 1}
                });

                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return inverted;
        }

        private static Bitmap CropCenterQrRegion(Bitmap original)
        {
            // PhonePe, GooglePay, and Paytm standees typically have the QR code occupying
            // the central 60% - 85% of the height and 80% - 95% of the width.
            int cropX = (int)(original.Width * 0.05);
            int cropY = (int)(original.Height * 0.20);
            int cropWidth = Math.Max(10, (int)(original.Width * 0.90));
            int cropHeight = Math.Max(10, (int)(original.Height * 0.65));

            var cropped = new Bitmap(cropWidth, cropHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, new Rectangle(0, 0, cropWidth, cropHeight),
                    new Rectangle(cropX, cropY, cropWidth, cropHeight), GraphicsUnit.Pixel);
            }
            return cropped;
        }

        private static Bitmap ApplyContrastAndGrayscale(Bitmap original)
        {
            var result = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                // High contrast grayscale matrix
                var cm = new ColorMatrix(new float[][]
                {
                    new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                    new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                    new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {-0.1f, -0.1f, -0.1f, 0, 1}
                });

                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, ia);
                }
            }
            return result;
        }

        public (ExtractionStatus Status, string UpiId, string ErrorMessage) DecodeFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (ExtractionStatus.Failed, string.Empty, "File does not exist.");
            }

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var bitmap = new Bitmap(stream))
                {
                    return DecodeBitmap(bitmap);
                }
            }
            catch (Exception ex)
            {
                return (ExtractionStatus.Failed, string.Empty, $"Unable to load image file: {ex.Message}");
            }
        }

        public static (ExtractionStatus Status, string UpiId, string ErrorMessage) ExtractUpiFromPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return (ExtractionStatus.Failed, string.Empty, "QR payload is empty.");
            }

            string trimmed = payload.Trim();

            // Format 1: Direct upi:// URI (e.g., upi://pay?pa=example@ybl&pn=Shop)
            if (trimmed.StartsWith("upi://", StringComparison.OrdinalIgnoreCase))
            {
                string query = string.Empty;
                int queryIndex = trimmed.IndexOf('?');
                if (queryIndex >= 0 && queryIndex < trimmed.Length - 1)
                {
                    query = trimmed.Substring(queryIndex + 1);
                }
                else
                {
                    query = trimmed;
                }

                string? paValue = ExtractQueryParam(query, "pa");
                if (!string.IsNullOrWhiteSpace(paValue))
                {
                    string decodedPa = HttpUtility.UrlDecode(paValue).Trim();
                    if (IsValidUpiSyntax(decodedPa))
                    {
                        return (ExtractionStatus.Success, decodedPa, string.Empty);
                    }
                    return (ExtractionStatus.Failed, decodedPa, $"Extracted 'pa' parameter '{decodedPa}' does not match standard UPI syntax.");
                }

                return (ExtractionStatus.UnsupportedFormat, string.Empty, "UPI URI found, but required 'pa' parameter was missing.");
            }

            // Format 2: Other URL containing upi parameter or query string
            if (trimmed.Contains("pa=") && (trimmed.Contains("upi") || trimmed.Contains("pay") || trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            {
                string? paValue = ExtractQueryParam(trimmed, "pa");
                if (!string.IsNullOrWhiteSpace(paValue))
                {
                    string decodedPa = HttpUtility.UrlDecode(paValue).Trim();
                    if (IsValidUpiSyntax(decodedPa))
                    {
                        return (ExtractionStatus.Success, decodedPa, string.Empty);
                    }
                }
            }

            // Format 3: Raw UPI ID directly encoded into QR (e.g. 9676655430@ybl)
            if (IsValidUpiSyntax(trimmed))
            {
                return (ExtractionStatus.Success, trimmed, string.Empty);
            }

            return (ExtractionStatus.UnsupportedFormat, string.Empty, "QR code does not contain a recognized UPI payment payload or VPA.");
        }

        public static bool IsValidUpiSyntax(string upiId)
        {
            if (string.IsNullOrWhiteSpace(upiId)) return false;
            return UpiAddressRegex.IsMatch(upiId);
        }

        private static string? ExtractQueryParam(string queryString, string paramName)
        {
            int qIndex = queryString.IndexOf('?');
            if (qIndex >= 0)
            {
                queryString = queryString.Substring(qIndex + 1);
            }

            string[] parts = queryString.Split('&');
            foreach (var part in parts)
            {
                int eqIndex = part.IndexOf('=');
                if (eqIndex > 0)
                {
                    string key = part.Substring(0, eqIndex).Trim();
                    if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        return part.Substring(eqIndex + 1).Trim();
                    }
                }
            }

            return null;
        }
    }
}
