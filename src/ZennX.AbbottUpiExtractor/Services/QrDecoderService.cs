using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Web;
using ZXing;
using ZXing.Windows.Compatibility;
using ZennX.AbbottUpiExtractor.Models;

namespace ZennX.AbbottUpiExtractor.Services
{
    public class QrDecoderService
    {
        private static readonly Regex UpiAddressRegex = new Regex(
            @"^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z]{2,64}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly BarcodeReader _reader;

        public QrDecoderService()
        {
            _reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                }
            };
        }

        public (ExtractionStatus Status, string UpiId, string ErrorMessage) DecodeBitmap(Bitmap bitmap)
        {
            try
            {
                var result = _reader.Decode(bitmap);
                if (result == null || string.IsNullOrWhiteSpace(result.Text))
                {
                    return (ExtractionStatus.Failed, string.Empty, "No QR code could be detected or decoded in the image.");
                }

                return ExtractUpiFromPayload(result.Text);
            }
            catch (Exception ex)
            {
                return (ExtractionStatus.Failed, string.Empty, $"QR decode exception: {ex.Message}");
            }
        }

        public (ExtractionStatus Status, string UpiId, string ErrorMessage) DecodeFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (ExtractionStatus.Failed, string.Empty, "File does not exist.");
            }

            try
            {
                // Open stream with file sharing to prevent file lock
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

            // Format 1: Direct upi:// URI (e.g., upi://pay?pa=retailer@bank&pn=ShopName)
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
                    // Case where upi://pay/pa=retailer@bank
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

                return (ExtractionStatus.UnsupportedFormat, string.Empty, "UPI URI found, but required 'pa' (Payee Address) parameter was missing.");
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

            // Format 3: Raw UPI ID directly encoded into QR (e.g. "9876543210@paytm" or "merchant@okaxis")
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
            // Normalize
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
