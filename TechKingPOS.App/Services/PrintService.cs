using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

using GdiBrushes = System.Drawing.Brushes;
using GdiPointF = System.Drawing.PointF;
using GdiFont = System.Drawing.Font;
using GdiImage = System.Drawing.Image;

namespace TechKingPOS.App.Services
{
    public static class PrintService
    {
        // =========================================
        // ✅ EXISTING TEXT PRINT (UNCHANGED)
        // =========================================
public static void PrintReceiptText(
    string receiptText,
    string receiptTitle,
    int copies)
{
    try
    {
        var printers = PrinterSettings.InstalledPrinters;

        if (printers.Count == 0)
        {
            MessageBox.Show("No printer installed.");
            return;
        }

        string printerName = new PrinterSettings().PrinterName;

        if (string.IsNullOrWhiteSpace(printerName))
        {
            MessageBox.Show("No default printer selected.");
            return;
        }

        for (int i = 0; i < copies; i++)
        {
            var pd = new PrintDocument();

            pd.PrintController = new StandardPrintController();

            pd.PrinterSettings.PrinterName = printerName;

            if (!pd.PrinterSettings.IsValid)
            {
                MessageBox.Show("Printer is invalid.");
                return;
            }

            pd.PrintPage += (sender, e) =>
            {
                using var font = new Font("Consolas", 10);

                e.Graphics.DrawString(
                    receiptText,
                    font,
                    GdiBrushes.Black,
                    new GdiPointF(0, 0)
                );
            };

            pd.Print();
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Printing failed: {ex.Message}");
    }
}
        // =========================================
        // ✅ NEW: PRINT WITH LOGO
        // =========================================
        public static void PrintReceiptWithLogo(
            string receiptText,
            string receiptTitle,
            int copies,
            byte[] logoBytes)
        {
            try
            {
                var printers = PrinterSettings.InstalledPrinters;

                if (printers.Count == 0)
                {
                    ShowReceiptPreview(receiptText, receiptTitle);
                    return;
                }

                string printerName = printers[0];

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    ShowReceiptPreview(receiptText, receiptTitle);
                    return;
                }

                if (printerName.Contains("PDF") || printerName.Contains("OneNote"))
                {
                    ShowReceiptPreview(receiptText, receiptTitle);
                    return;
                }

                for (int i = 0; i < copies; i++)
                {
                    var pd = new PrintDocument();
                    pd.PrintController = new StandardPrintController();
                    pd.PrinterSettings.PrinterName = printerName;

                    if (!pd.PrinterSettings.IsValid)
                    {
                        ShowReceiptPreview(receiptText, receiptTitle);
                        return;
                    }

                    pd.PrintPage += (sender, e) =>
                    {
                        float y = 0;

                        // 🔹 DRAW LOGO FIRST
                        if (logoBytes != null && logoBytes.Length > 0)
                        {
                            using var ms = new MemoryStream(logoBytes);
                            using var img = GdiImage.FromStream(ms);

                            // Resize to fit receipt width
                            int maxWidth = 250;
                            int newHeight = (int)(img.Height * (maxWidth / (double)img.Width));

                            e.Graphics.DrawImage(img, 0, y, maxWidth, newHeight);

                            y += newHeight + 10; // spacing
                        }

                        // 🔹 DRAW TEXT BELOW LOGO
                        using var font = new Font("Consolas", 10);

                        e.Graphics.DrawString(
                            receiptText,
                            font,
                            GdiBrushes.Black,
                            new RectangleF(0, y, e.MarginBounds.Width, e.MarginBounds.Height)
                        );
                    };

                    pd.Print();
                }
            }
            catch
            {
                ShowReceiptPreview(receiptText, receiptTitle);
            }
        }

        // =========================================
        // ✅ PREVIEW (UNCHANGED)
        // =========================================
        public static void ShowReceiptPreview(string receiptText, string title = "Receipt Preview")
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = receiptText,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            var window = new Window
            {
                Title = title,
                Content = textBlock,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
        }
    }
}