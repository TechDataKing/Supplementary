using System.Text;
using System.Text.RegularExpressions;

namespace TechKingPOS.App.Services.AI
{
    public class TextCleaner
    {
        public string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove weird symbols
            string cleaned = Regex.Replace(input, @"[^\w\s\.\,\n]", " ");

            // Normalize spaces
            cleaned = Regex.Replace(cleaned, @"[ ]{2,}", " ");

            return cleaned.Trim();
        }
    }
}