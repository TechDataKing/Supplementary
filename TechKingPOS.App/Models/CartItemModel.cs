public class CartItemModel
{   
    public int ItemId { get; set; }
    public string Name { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Total => Quantity * Price;
    public int BranchId { get; set; }
    public string Barcode { get; set; }

}
