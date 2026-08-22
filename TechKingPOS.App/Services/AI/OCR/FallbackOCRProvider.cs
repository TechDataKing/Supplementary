using System;
using System.Threading.Tasks;

namespace TechKingPOS.App.Services.AI.OCR
{
    public class FallbackOCRProvider : IOCRProvider
    {
        public string Name => "FallbackOCR";

        public Task<string> ExtractAsync(byte[] imageBytes)
        {
            // last resort: return empty but logged
            string text = "";

            OCRLogger.Log(Name, text);

            return Task.FromResult(text);
        }
    }
}