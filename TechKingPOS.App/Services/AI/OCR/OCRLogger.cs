using System;
using System.IO;

namespace TechKingPOS.App.Services.AI.OCR
{
    public static class OCRLogger
    {
        private static readonly string logFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocr_logs.txt");

        public static void Log(string engine, string text, Exception error = null)
        {
            var log =
                $"[{DateTime.Now}] ENGINE: {engine}\n" +
                $"RESULT:\n{text}\n" +
                (error != null ? $"ERROR: {error.Message}\n" : "") +
                "-----------------------------------\n";

            File.AppendAllText(logFile, log);
        }
    }
}