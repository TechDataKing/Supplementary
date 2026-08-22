using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services.Import
{
    public class ImportRuleEngine
    {
        public string DetectDocumentType(string text)
        {
            var lines = SplitLines(text);

            int invoiceScore = 0;
            int catalogScore = 0;

            foreach (var line in lines)
            {
                var numbers = ExtractNumbers(line);
                if (numbers.Count == 0) continue;

                // invoice pattern: number near beginning (qty-like)
                if (Regex.IsMatch(line, @"^\D*\d+(\.\d+)?\s"))
                    invoiceScore++;

                // catalog pattern: 2–3 numbers per line
                if (numbers.Count >= 2)
                    catalogScore++;
            }

            return invoiceScore > catalogScore ? "Invoice" : "Catalog";
        }
        public List<ItemImportResult> Process(string cleanedText, string type)
        {
            if (string.IsNullOrWhiteSpace(type) || type == "Auto")
                type = DetectDocumentType(cleanedText);

            return type switch
            {
                "SingleItem" => new List<ItemImportResult> { ParseSingleItem(cleanedText) },
                "Catalog" => ParseCatalog(cleanedText),
                "Invoice" => ParseInvoice(cleanedText),
                _ => new List<ItemImportResult>()
            };
        }
        private ItemImportResult ParseSingleItem(string text)
        {
            var lines = SplitLines(text);

            // take first meaningful line
            var line = lines.FirstOrDefault(l => !IsHeader(l));

            if (string.IsNullOrWhiteSpace(line))
                return new ItemImportResult
                {
                    IsValid = false,
                    Error = "No readable data"
                };

            var numbers = ExtractNumbers(line);

            decimal qty = 1;
            decimal price = 0;

            if (numbers.Count == 1)
            {
                price = numbers[0];
            }
            else if (numbers.Count >= 2)
            {
                qty = numbers[0];
                price = numbers[1];
            }

            return new ItemImportResult
            {
                Name = CleanName(line),
                Quantity = qty,
                SellingPrice = price,
                MarkedPrice = price,
                WholesalePrice = null,
                UnitType = "pieces",
                UnitValue = 1,
                Supplier = null,
                IsValid = true,
                Error = "OK",
                Confidence = 0.9
            };
        }

        // =========================
        // 🔹 CATALOG
        // =========================
        private List<ItemImportResult> ParseCatalog(string text)
        {
            var results = new List<ItemImportResult>();
            var lines = SplitLines(text);

            foreach (var line in lines)
            {
                if (IsHeader(line))
                    continue;

                var numbers = ExtractNumbers(line);
                if (numbers.Count < 2)
                    continue;

                // =========================
                // STEP 1: PRESERVE ORDER
                // =========================
                var ordered = numbers.ToList(); // DO NOT SORT

                // =========================
                // STEP 2: SAFE QTY DETECTION
                // =========================
                decimal qty = DetectQuantity(ordered);

                var priceCandidates = ordered
                    .Where(n => n != qty)
                    .ToList();

                // fallback safety
                if (priceCandidates.Count == 0)
                    continue;

                // =========================
                // STEP 3: POSITION-BASED ASSIGNMENT (NOT SORTING)
                // =========================
                decimal marked = 0;
                decimal selling = 0;
                decimal wholesale = 0;

                if (priceCandidates.Count == 1)
                {
                    marked = priceCandidates[0];
                    selling = priceCandidates[0];
                    wholesale = priceCandidates[0];
                }
                else if (priceCandidates.Count == 2)
                {
                    // assume: marked first, selling second
                    marked = priceCandidates[0];
                    selling = priceCandidates[1];
                    wholesale = (marked + selling) / 2m;
                }
                else if (priceCandidates.Count >= 3)
                {
                    // IMPORTANT: DO NOT SORT
                    // we trust OCR order instead

                    marked = priceCandidates[0];
                    wholesale = priceCandidates[1];
                    selling = priceCandidates[2];
                }

                results.Add(new ItemImportResult
                {
                    Name = CleanName(line),

                    MarkedPrice = marked,
                    SellingPrice = selling,
                    WholesalePrice = wholesale,

                    Quantity = qty,

                    UnitType = "pieces",
                    UnitValue = 1,

                    IsValid = true,
                    Error = "OK",
                    Confidence = 0.92
                });
            }

            return results;
        }
        // =========================
        // 🔹 INVOICE
        // =========================
        private List<ItemImportResult> ParseInvoice(string text)
        {
            var results = new List<ItemImportResult>();
            var lines = SplitLines(text);

            foreach (var line in lines)
            {
                if (IsHeader(line))
                    continue;

                // try to detect qty with units first
                var qtyMatch = Regex.Match(line, @"(\d+(\.\d+)?)\s*(kg|g|pcs|piece|pack)?", RegexOptions.IgnoreCase);
                var numbers = ExtractNumbers(line);

                if (numbers.Count < 2)
                    continue;

                decimal qty = 1;
                decimal price;

                if (qtyMatch.Success)
                {
                    qty = decimal.Parse(qtyMatch.Groups[1].Value);
                }

                // fallback price = last number (more reliable in OCR)
                price = numbers.Last();

                results.Add(new ItemImportResult
                {
                    Name = CleanName(line),
                    Quantity = qty,
                    SellingPrice = price,
                    MarkedPrice = price,
                    WholesalePrice = null,
                    UnitType = "pieces",
                    UnitValue = 1,
                    IsValid = true,
                    Error = "OK",
                    Confidence = 0.85
                });
            }

            return results;
        }

        private decimal DetectQuantity(List<decimal> numbers)
        {
            // rule 1: qty is usually the largest integer-like value
            var candidates = numbers
                .Where(n => n % 1 == 0) // integers only
                .ToList();

            if (candidates.Count == 0)
                return numbers.Max();

            // pick largest integer but ONLY if it's significantly larger
            var max = candidates.Max();

            var secondMax = candidates
                .Where(n => n != max)
                .DefaultIfEmpty(max)
                .Max();

            // sanity check: qty should be much larger than price
            if (max > secondMax * 3)
                return max;

            // fallback: last number is often qty in OCR tables
            return numbers.Last();
        }

        // =========================
        // 🔍 HELPERS
        // =========================

        private List<string> SplitLines(string text)
        {
            return text
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private bool IsHeader(string line)
        {
            var l = line.ToLower();

            return l.Contains("name") &&
                   l.Contains("price");
        }

        private string CleanName(string line)
        {
            // remove numbers
            var name = Regex.Replace(line, @"[\d]+", "");

            // remove units
            name = Regex.Replace(name, @"\b(kg|g|l|ml|pieces|pcs)\b", "", RegexOptions.IgnoreCase);

            // remove extra symbols
            name = Regex.Replace(name, @"[^\w\s]", "");

            return name.Trim();
        }

        private List<decimal> ExtractNumbers(string line)
        {
            var matches = Regex.Matches(line, @"\d+(\.\d+)?");

            return matches
                .Select(m => decimal.TryParse(m.Value, out var v) ? v : 0)
                .Where(v => v > 0)
                .ToList();
        }
    }
}