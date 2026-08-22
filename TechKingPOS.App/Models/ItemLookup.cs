namespace TechKingPOS.App.Models
{
    public class ItemLookup
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Barcode { get; set; } = "";

        public decimal Quantity { get; set; }          // ✅ REQUIRED
        public decimal? TargetQuantity { get; set; }   // ✅ NULLABLE TARGET

        public decimal MarkedPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int BranchId { get; set; }
        public bool IsSelected { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? UnitValue { get; set; }


        public string UnitType { get; set; } = string.Empty;
        public string? Supplier { get; set; }
        public decimal Deficit =>
            TargetQuantity.HasValue
                ? Math.Max(0, TargetQuantity.Value - Quantity)
                : 0;
        // Used by Sales list
        public string GetDisplay(bool isWholesale)
        {
            decimal price = SellingPrice;

            if (isWholesale && WholesalePrice.HasValue && WholesalePrice > 0)
                price = WholesalePrice.Value;

            return $"{Name} ({UnitType}) - Sh. {price:0.00}";
        } 
        public string Display => $"{Name}";       
    }
}
