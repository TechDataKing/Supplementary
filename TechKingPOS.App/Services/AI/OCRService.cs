using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechKingPOS.App.Services.AI.OCR;

namespace TechKingPOS.App.Services.AI
{
    public class OCRService
    {
        private readonly List<IOCRProvider> _providers;

        public OCRService()
        {
            _providers = new List<IOCRProvider>
            {
                new TesseractOCRProvider(),
                new FallbackOCRProvider()
            };
        }

        public async Task<string> ExtractTextAsync(byte[] imageBytes)
        {
            Exception lastError = null;

            foreach (var provider in _providers)
            {
                try
                {
                    var text = await provider.ExtractAsync(imageBytes);

                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    OCRLogger.Log(provider.Name, "", ex);
                }
            }

            throw new Exception("All OCR engines failed", lastError);
        }
    }
}