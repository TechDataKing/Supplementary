using System.Threading.Tasks;

using TechKingPOS.App.Services.AI;
using TechKingPOS.App.Services.Import;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services.Import
{
    public class PipelineService
    {
        private readonly OCRService _ocrService;
        private readonly TextCleaner _textCleaner;
        private readonly ImageClassifier _classifier;
        private readonly ImportRuleEngine _importEngine;

        public PipelineService(
            OCRService ocrService,
            TextCleaner textCleaner,
            ImageClassifier classifier,
            ImportRuleEngine importEngine)
        {
            _ocrService = ocrService;
            _textCleaner = textCleaner;
            _classifier = classifier;
            _importEngine = importEngine;
        }

        public async Task ProcessAsync(byte[] imageBytes)
        {
            var rawText = await _ocrService.ExtractTextAsync(imageBytes);

            var cleaned = _textCleaner.Clean(rawText);

            var type = _classifier.Classify(cleaned);

            var results = _importEngine.Process(cleaned, type);

            // 👉 OPEN YOUR EXISTING PREVIEW WINDOW HERE
            var win = new ImportPreviewWindow(results);
            win.ShowDialog();
        }
    }
}