using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ZXing;
using ZXing.QrCode;

namespace ZennX.AbbottUpiExtractor
{
    public static class SampleQrGenerator
    {
        public static void GenerateSamplePack(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var samples = new[]
            {
                ("qr_01_valid_retailer_apollo.png", "upi://pay?pa=apollo.pharmacy@okaxis&pn=Apollo%20Pharmacy&mc=5912"),
                ("qr_02_valid_retailer_medplus.png", "upi://pay?pa=medplus99@icici&pn=Medplus%20Chemists"),
                ("qr_03_valid_retailer_sanjeevani.png", "upi://pay?pa=sanjeevani.med@hdfcbank&pn=Sanjeevani%20Medical"),
                ("qr_04_valid_retailer_netmeds.png", "upi://pay?pa=netmeds.outlet@sbi&pn=Netmeds%20Store"),
                ("qr_05_valid_raw_upi.png", "shree.ganesh.medical@paytm"),
                ("qr_06_non_upi_website.png", "https://www.abbott.co.in/corp-landing.html"),
                ("qr_07_invalid_malformed.png", "upi://pay?pn=NoPaParamOnly"),
                ("qr_08_duplicate_sample.png", "upi://pay?pa=apollo.pharmacy@okaxis&pn=Apollo%20Pharmacy") // duplicate of #1
            };

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 300,
                    Width = 300,
                    Margin = 1
                }
            };

            foreach (var (filename, payload) in samples)
            {
                var pixelData = writer.Write(payload);
                using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);

                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                bitmap.Save(Path.Combine(outputDir, filename), ImageFormat.Png);
            }
        }
    }
}
