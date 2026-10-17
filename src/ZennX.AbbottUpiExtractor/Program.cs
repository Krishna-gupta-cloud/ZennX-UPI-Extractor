using System;
using System.Windows.Forms;
using ZennX.AbbottUpiExtractor.UI;

namespace ZennX.AbbottUpiExtractor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Set up unhandled exception handling for rock-solid stability
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"An unexpected error occurred:\n{ex?.Message}\n\nStack trace:\n{ex?.StackTrace}",
                    "Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(
                    $"An unexpected UI error occurred:\n{e.Exception.Message}",
                    "Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}