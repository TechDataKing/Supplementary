public class RepackStockModel
{
    public int Id { get; set; }

    // SOURCE
    public int ItemId { get; set; }
    public string ItemName { get; set; }
    public decimal SourceQuantity { get; set; }
    public string SourceUnit { get; set; }

    // OUTPUT
    public string BaseUnit { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }

    // COST
    public decimal SourceCost { get; set; }
    public decimal CostPerUnit { get; set; }

    // STATUS
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // TRACE
    public string CreatedBy { get; set; }
    public int BranchId { get; set; }

    public string Display =>
        $"{ItemName} → {RemainingQuantity} {BaseUnit}";
}