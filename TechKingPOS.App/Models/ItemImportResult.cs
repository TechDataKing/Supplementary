namespace TechKingPOS.App.Models
{
    public class ItemImportResult
    {
        public string Name { get; set; }
        public string Alias { get; set; }

        // Prices (keep numeric for logic)
        public decimal MarkedPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal? WholesalePrice { get; set; }

        // Quantity + unit
        public decimal Quantity { get; set; }
        public string UnitType { get; set; }
        public decimal? UnitValue { get; set; }

        public string Supplier { get; set; }

        // Validation / system
        public bool IsValid { get; set; }
        public string Error { get; set; }

        public int BranchId { get; set; }

        // AI / matching
        public double Confidence { get; set; }
        public bool IsMatched { get; set; }
        public int? ExistingItemId { get; set; }
    }
}