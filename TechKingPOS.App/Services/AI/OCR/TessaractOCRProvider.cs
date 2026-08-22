using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

namespace TechKingPOS.App.Services.AI.OCR
{
    public class TesseractOCRProvider : IOCRProvider
    {
        public string Name => "Tesseract";

        public async Task<string> ExtractAsync(byte[] imageBytes)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);

                    using var img = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(img);

                    var text = page.GetText();

                    OCRLogger.Log(Name, text);

                    return text;
                });
            }
            catch (Exception ex)
            {
                OCRLogger.Log(Name, "", ex);
                throw;
            }
        }
    }
}