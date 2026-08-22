using System.Linq;

namespace TechKingPOS.App.Services.AI
{
    public class ImageClassifier
    {
        public string Classify(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Unknown";

            var lines = text.Split('\n').Length;

            // SIMPLE RULE-BASED CLASSIFICATION (offline AI logic)

            bool hasNumbers = text.Any(char.IsDigit);

            if (lines <= 2 && hasNumbers)
                return "SingleItem";

            if (lines > 2 && text.Contains("x"))
                return "Invoice";

            if (lines > 2)
                return "Catalog";

            return "SingleItem";
        }
    }
}