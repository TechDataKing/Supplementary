public class SaleSourceItem
{
    public int ItemId { get; set; }

    public string Name { get; set; }

    public string UnitType { get; set; }

    public decimal AvailableQty { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal MarkedPrice { get; set; }

    public bool IsRepack { get; set; }

    public int? RepackId { get; set; } // for repack rows

    public string Display =>
        IsRepack
        ? $"{Name} (Repack) → {AvailableQty} {UnitType}"
        : $"{Name} (Full) → {AvailableQty} {UnitType}";
}