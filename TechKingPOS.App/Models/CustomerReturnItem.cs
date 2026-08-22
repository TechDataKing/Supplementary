public class CustomerReturnItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; }

    public decimal SoldQty { get; set; }
    public decimal AlreadyReturned { get; set; }

    public bool Selected { get; set; } = true; // 🔥 AUTO CHECKED

    public decimal ReturnQty { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MarkedPrice { get; set; }
}